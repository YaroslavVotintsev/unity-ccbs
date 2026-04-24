using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mapf.Core.Model;
using Mapf.Core.Planning;

namespace Mapf.Core.CCBS
{
    public sealed class CcbsPlanner
    {
        private readonly SippPlanner _sipp = new();
        private readonly ConflictDetector _conflicts = new();

        public MapfPlanningResult Plan(MapfPlanningRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Settings.ReplanStrategy == ReplanStrategy.AffectedAgentWithGlobalFallback &&
                request.AffectedAgentId.HasValue &&
                request.ExistingPlans.Count > 0)
            {
                var local = PlanAffectedAgent(request, cancellationToken);
                if (local.Success)
                    return local;
            }

            return PlanGlobal(request, cancellationToken);
        }

        public MapfPlanningResult PlanGlobal(MapfPlanningRequest request, CancellationToken cancellationToken = default)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(0.01, request.Settings.TimeLimitSeconds));
            var rootPaths = new TimedPath[request.Agents.Count];
            var agentIndex = BuildAgentIndex(request.Agents);

            for (var i = 0; i < request.Agents.Count; i++)
            {
                rootPaths[i] = _sipp.FindPath(request.Graph, request.Agents[i], Array.Empty<Constraint>(), request.Settings);
                if (rootPaths[i].IsEmpty)
                    return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), $"No path for agent {request.Agents[i].AgentId}.");
            }

            var open = new List<CbsNode>();
            var seen = new HashSet<string>();
            AddOpen(open, new CbsNode(rootPaths, new List<Constraint>(), Flowtime(rootPaths)));
            seen.Add(string.Empty);
            var expanded = 0;

            while (open.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline)
                    return new MapfPlanningResult(PlannerStatus.Timeout, Array.Empty<TimedPath>(), "MAPF planning timed out.");
                if (++expanded > request.Settings.MaxHighLevelNodes)
                    return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "High-level node limit reached.");

                var node = open[0];
                open.RemoveAt(0);

                var conflict = _conflicts.FindFirstConflict(node.Paths, request.Settings);
                if (!conflict.IsValid)
                    return new MapfPlanningResult(PlannerStatus.Success, node.Paths.OrderBy(p => p.AgentId).ToArray());

                Branch(request, agentIndex, open, seen, node, conflict.AgentA, conflict.MoveA, conflict.MoveB);
                Branch(request, agentIndex, open, seen, node, conflict.AgentB, conflict.MoveB, conflict.MoveA);
            }

            return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Open set exhausted.");
        }

        private MapfPlanningResult PlanAffectedAgent(MapfPlanningRequest request, CancellationToken cancellationToken)
        {
            var affectedId = request.AffectedAgentId.Value;
            var agent = request.Agents.FirstOrDefault(a => a.AgentId == affectedId);
            if (agent.AgentId != affectedId)
                return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), $"Affected agent {affectedId} is not present.");

            var paths = request.ExistingPlans.ToDictionary(p => p.AgentId, p => p);
            var fixedPaths = request.ExistingPlans.Where(p => p.AgentId != affectedId).ToArray();
            var constraints = new List<Constraint>();

            for (var i = 0; i < request.Settings.MaxLocalRepairIterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = _sipp.FindPath(request.Graph, agent, constraints, request.Settings);
                if (path.IsEmpty)
                    return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Local repair failed.");

                if (!_conflicts.HasConflict(path, fixedPaths, request.Settings, out var conflict))
                {
                    paths[affectedId] = path;
                    return new MapfPlanningResult(PlannerStatus.Success, paths.Values.OrderBy(p => p.AgentId).ToArray());
                }

                var own = conflict.AgentA == affectedId ? conflict.MoveA : conflict.MoveB;
                var other = conflict.AgentA == affectedId ? conflict.MoveB : conflict.MoveA;
                constraints.Add(ConstraintGenerator.ForAgent(affectedId, own, other, request.Settings));
            }

            return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Local repair iteration limit reached.");
        }

        private void Branch(
            MapfPlanningRequest request,
            IReadOnlyDictionary<int, int> agentIndex,
            List<CbsNode> open,
            HashSet<string> seen,
            CbsNode parent,
            int constrainedAgentId,
            TimedMove ownMove,
            TimedMove otherMove)
        {
            if (!agentIndex.TryGetValue(constrainedAgentId, out var index))
                return;

            var constraints = new List<Constraint>(parent.Constraints)
            {
                ConstraintGenerator.ForAgent(constrainedAgentId, ownMove, otherMove, request.Settings)
            };
            var signature = Signature(constraints);
            if (!seen.Add(signature))
                return;

            var newPath = _sipp.FindPath(request.Graph, request.Agents[index], constraints, request.Settings);
            if (newPath.IsEmpty || double.IsInfinity(newPath.Cost) || double.IsNaN(newPath.Cost))
                return;

            var paths = parent.Paths.ToArray();
            paths[index] = newPath;
            AddOpen(open, new CbsNode(paths, constraints, Flowtime(paths)));
        }

        private static string Signature(IEnumerable<Constraint> constraints)
        {
            return string.Join("|", constraints
                .OrderBy(c => c.AgentId)
                .ThenBy(c => c.FromNodeId)
                .ThenBy(c => c.ToNodeId)
                .ThenBy(c => c.StartTime)
                .ThenBy(c => c.EndTime)
                .Select(c => $"{c.AgentId}:{c.FromNodeId}>{c.ToNodeId}:{c.StartTime:0.######}-{c.EndTime:0.######}"));
        }

        private static Dictionary<int, int> BuildAgentIndex(IReadOnlyList<AgentState> agents)
        {
            var result = new Dictionary<int, int>();
            for (var i = 0; i < agents.Count; i++)
                result[agents[i].AgentId] = i;
            return result;
        }

        private static double Flowtime(IEnumerable<TimedPath> paths)
        {
            var total = 0.0;
            foreach (var path in paths)
                total += path.Cost;
            return total;
        }

        private static void AddOpen(List<CbsNode> open, CbsNode node)
        {
            var index = open.FindIndex(n => n.Cost > node.Cost);
            if (index < 0)
                open.Add(node);
            else
                open.Insert(index, node);
        }

        private sealed class CbsNode
        {
            public readonly TimedPath[] Paths;
            public readonly IReadOnlyList<Constraint> Constraints;
            public readonly double Cost;

            public CbsNode(TimedPath[] paths, IReadOnlyList<Constraint> constraints, double cost)
            {
                Paths = paths;
                Constraints = constraints;
                Cost = cost;
            }
        }
    }
}
