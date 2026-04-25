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
            var reservationPaths = ReservationPaths(request);

            for (var i = 0; i < request.Agents.Count; i++)
            {
                rootPaths[i] = WithExistingPrefix(_sipp.FindPath(request.Graph, request.Agents[i], Array.Empty<Constraint>(), request.Settings), request.ExistingPlans);
                if (rootPaths[i].IsEmpty)
                    return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), $"No path for agent {request.Agents[i].AgentId}.");
            }

            var prioritized = PlanPrioritized(request, rootPaths, reservationPaths, cancellationToken);
            if (prioritized.Success)
                return prioritized;

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

                var conflict = _conflicts.FindFirstConflict(WithReservations(node.Paths, reservationPaths), request.Settings);
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
            var fixedPaths = request.ExistingPlans
                .Where(p => p.AgentId != affectedId)
                .Concat(ReservationPaths(request))
                .ToArray();
            var constraints = new List<Constraint>();

            for (var i = 0; i < request.Settings.MaxLocalRepairIterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = WithExistingPrefix(_sipp.FindPath(request.Graph, agent, constraints, request.Settings), request.ExistingPlans);
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

        private MapfPlanningResult PlanPrioritized(
            MapfPlanningRequest request,
            IReadOnlyList<TimedPath> rootPaths,
            IReadOnlyList<TimedPath> reservationPaths,
            CancellationToken cancellationToken)
        {
            var independentByAgent = rootPaths.ToDictionary(path => path.AgentId, path => path);
            var orders = BuildPriorityOrders(request.Agents, independentByAgent);
            TimedPath[] bestCandidate = null;

            foreach (var order in orders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var planned = new Dictionary<int, TimedPath>();
                var failed = false;

                foreach (var agent in order)
                {
                    var constraints = new List<Constraint>();
                    TimedPath path = null;

                    for (var i = 0; i < request.Settings.MaxLocalRepairIterations; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        path = WithExistingPrefix(_sipp.FindPath(request.Graph, agent, constraints, request.Settings), request.ExistingPlans);
                        if (path == null || path.IsEmpty)
                        {
                            failed = true;
                            break;
                        }

                        var fixedPaths = planned.Values.Concat(reservationPaths).ToArray();
                        if (!_conflicts.HasConflict(path, fixedPaths, request.Settings, out var conflict))
                            break;

                        var own = conflict.AgentA == agent.AgentId ? conflict.MoveA : conflict.MoveB;
                        var other = conflict.AgentA == agent.AgentId ? conflict.MoveB : conflict.MoveA;
                        constraints.Add(ConstraintGenerator.ForAgent(agent.AgentId, own, other, request.Settings));
                        path = null;
                    }

                    if (failed || path == null || path.IsEmpty)
                    {
                        failed = true;
                        break;
                    }

                    planned[agent.AgentId] = path;
                }

                if (!failed && planned.Count == request.Agents.Count)
                {
                    var candidate = planned.Values.OrderBy(path => path.AgentId).ToArray();
                    bestCandidate = ChooseBetter(bestCandidate, candidate);
                }
            }

            return bestCandidate != null
                ? new MapfPlanningResult(PlannerStatus.Success, bestCandidate)
                : new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Prioritized planning failed.");
        }

        private static IReadOnlyList<IReadOnlyList<AgentState>> BuildPriorityOrders(
            IReadOnlyList<AgentState> agents,
            IReadOnlyDictionary<int, TimedPath> independentByAgent)
        {
            var original = agents.ToArray();
            var shortestFirst = agents
                .OrderBy(agent => independentByAgent.TryGetValue(agent.AgentId, out var path) ? path.Cost : double.PositiveInfinity)
                .ThenBy(agent => agent.AgentId)
                .ToArray();
            var longestFirst = shortestFirst.Reverse().ToArray();

            return new[] { shortestFirst, longestFirst, original };
        }

        private static TimedPath[] ChooseBetter(TimedPath[] currentBest, TimedPath[] candidate)
        {
            if (currentBest == null)
                return candidate;

            var currentScore = PlanQualityScore(currentBest);
            var candidateScore = PlanQualityScore(candidate);
            return candidateScore < currentScore - 1e-6 ? candidate : currentBest;
        }

        private static double PlanQualityScore(IEnumerable<TimedPath> paths)
        {
            var score = 0.0;
            foreach (var path in paths)
            {
                score += path.Cost;
                score += TravelDistance(path) * 10.0;
                score += DirectionChanges(path) * 2.0;
            }

            return score;
        }

        private static double TravelDistance(TimedPath path)
        {
            var distance = 0.0;
            for (var i = 0; i + 1 < path.Points.Count; i++)
            {
                var a = path.Points[i];
                var b = path.Points[i + 1];
                if (a.NodeId != b.NodeId)
                    distance += MapfVector2.Distance(a.Position, b.Position);
            }

            return distance;
        }

        private static int DirectionChanges(TimedPath path)
        {
            var changes = 0;
            MapfVector2? previous = null;
            for (var i = 0; i + 1 < path.Points.Count; i++)
            {
                var a = path.Points[i];
                var b = path.Points[i + 1];
                if (a.NodeId == b.NodeId)
                    continue;

                var direction = b.Position - a.Position;
                if (previous.HasValue && MapfVector2.Dot(previous.Value, direction) < -1e-6)
                    changes++;
                previous = direction;
            }

            return changes;
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

            var newPath = WithExistingPrefix(_sipp.FindPath(request.Graph, request.Agents[index], constraints, request.Settings), request.ExistingPlans);
            if (newPath.IsEmpty || double.IsInfinity(newPath.Cost) || double.IsNaN(newPath.Cost))
                return;

            var paths = parent.Paths.ToArray();
            paths[index] = newPath;
            AddOpen(open, new CbsNode(paths, constraints, Flowtime(paths)));
        }

        private static TimedPath WithExistingPrefix(TimedPath suffix, IReadOnlyList<TimedPath> existingPlans)
        {
            if (suffix == null || suffix.IsEmpty || existingPlans == null || existingPlans.Count == 0)
                return suffix;

            var existing = existingPlans.FirstOrDefault(path => path.AgentId == suffix.AgentId);
            if (existing == null || existing.IsEmpty)
                return suffix;

            var switchPoint = suffix.Points[0];
            var points = new List<TimedPathPoint>();
            foreach (var point in existing.Points)
            {
                if (point.Time < switchPoint.Time - 1e-6)
                    points.Add(point);
            }

            if (points.Count == 0)
                return suffix;

            if (points[points.Count - 1].NodeId != switchPoint.NodeId || Math.Abs(points[points.Count - 1].Time - switchPoint.Time) > 1e-6)
                points.Add(switchPoint);

            for (var i = 1; i < suffix.Points.Count; i++)
                points.Add(suffix.Points[i]);

            return new TimedPath(suffix.AgentId, points);
        }

        private static IReadOnlyList<TimedPath> ReservationPaths(MapfPlanningRequest request)
        {
            if (request.Reservations.Count == 0)
                return Array.Empty<TimedPath>();

            return request.Reservations
                .Where(reservation => reservation.Path != null && !reservation.Path.IsEmpty)
                .Select(reservation => reservation.Path)
                .ToArray();
        }

        private static IReadOnlyList<TimedPath> WithReservations(IReadOnlyList<TimedPath> paths, IReadOnlyList<TimedPath> reservations)
        {
            if (reservations == null || reservations.Count == 0)
                return paths;

            return paths.Concat(reservations).ToArray();
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
