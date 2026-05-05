using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mapf.Core.Model;
using Mapf.Core.Planning;

namespace Mapf.Core.CCBS
{
    /// <summary>
    /// Pure C# continuous-time CBS-style planner for roadmap MAPF.
    /// Uses SIPP-like low-level planning, continuous conflict detection, prioritized pre-planning,
    /// and CBS high-level branching when conflicts remain.
    /// </summary>
    public sealed class CcbsPlanner
    {
        private const double HighLevelFocalWeight = 1.1;

        private readonly SippPlanner _sipp = new();
        private readonly ConflictDetector _conflicts = new();

        /// <summary>
        /// Plans according to the request strategy. Affected-agent replans are tried locally before global fallback.
        /// </summary>
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

        /// <summary>
        /// Computes a full multi-agent plan from the request agent states, reservations, and settings.
        /// </summary>
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

            var prioritized = PlanPrioritized(request, rootPaths, reservationPaths, cancellationToken, deadline);
            if (prioritized.Success)
                return prioritized;

            var open = new List<CbsNode>();
            var seen = new HashSet<string>();
            AddOpen(open, CreateNode(rootPaths, new List<Constraint>(), reservationPaths, request.Settings));
            seen.Add(string.Empty);
            var expanded = 0;

            while (open.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline)
                    return new MapfPlanningResult(PlannerStatus.Timeout, Array.Empty<TimedPath>(), "MAPF planning timed out.");
                if (++expanded > request.Settings.MaxHighLevelNodes)
                    return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "High-level node limit reached.");

                var node = PopBestNode(open);

                if (node.ConflictCount == 0)
                    return new MapfPlanningResult(PlannerStatus.Success, node.Paths.OrderBy(p => p.AgentId).ToArray());

                var conflict = SelectConflict(node.Conflicts);
                Branch(request, agentIndex, open, seen, node, conflict.AgentA, conflict.MoveA, conflict.MoveB);
                Branch(request, agentIndex, open, seen, node, conflict.AgentB, conflict.MoveB, conflict.MoveA);
            }

            return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Open set exhausted.");
        }

        /// <summary>
        /// Attempts to repair only one affected agent while treating all other current plans as fixed.
        /// </summary>
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
            var constraintKeys = new HashSet<string>();

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
                if (!TryAddConstraint(constraints, constraintKeys, ConstraintGenerator.ForAgent(affectedId, own, other, request.Settings)))
                    return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Local repair repeated a constraint without progress.");
            }

            return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Local repair iteration limit reached.");
        }

        /// <summary>
        /// Tries bounded prioritized planning orders before entering the more expensive CBS search.
        /// </summary>
        private MapfPlanningResult PlanPrioritized(
            MapfPlanningRequest request,
            IReadOnlyList<TimedPath> rootPaths,
            IReadOnlyList<TimedPath> reservationPaths,
            CancellationToken cancellationToken,
            DateTime deadline)
        {
            var independentByAgent = rootPaths.ToDictionary(path => path.AgentId, path => path);
            var orders = BuildPriorityOrders(request.Agents, independentByAgent, request.Graph);
            var prioritizedDeadline = PrioritizedDeadline(deadline);
            var repairLimit = PrioritizedRepairLimit(request);

            foreach (var order in orders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > prioritizedDeadline)
                    return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Prioritized planning budget exhausted.");

                var planned = new Dictionary<int, TimedPath>();
                var failed = false;

                foreach (var agent in order)
                {
                    if (DateTime.UtcNow > prioritizedDeadline)
                        return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Prioritized planning budget exhausted.");

                    var constraints = new List<Constraint>();
                    var constraintKeys = new HashSet<string>();
                    TimedPath path = null;

                    for (var i = 0; i < repairLimit; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (DateTime.UtcNow > prioritizedDeadline)
                            return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Prioritized planning budget exhausted.");

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
                        if (!TryAddConstraint(constraints, constraintKeys, ConstraintGenerator.ForAgent(agent.AgentId, own, other, request.Settings)))
                        {
                            failed = true;
                            break;
                        }

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
                    return new MapfPlanningResult(PlannerStatus.Success, planned.Values.OrderBy(path => path.AgentId).ToArray());
                }
            }

            return new MapfPlanningResult(PlannerStatus.NoSolution, Array.Empty<TimedPath>(), "Prioritized planning failed.");
        }

        private static DateTime PrioritizedDeadline(DateTime globalDeadline)
        {
            var now = DateTime.UtcNow;
            var remaining = globalDeadline - now;
            if (remaining <= TimeSpan.Zero)
                return now;

            var budgetSeconds = Math.Min(2.0, Math.Max(0.05, remaining.TotalSeconds * 0.25));
            return now + TimeSpan.FromSeconds(budgetSeconds);
        }

        private static int PrioritizedRepairLimit(MapfPlanningRequest request)
        {
            var graphScaledLimit = Math.Max(32, request.Graph.Count * 2);
            return Math.Min(request.Settings.MaxLocalRepairIterations, graphScaledLimit);
        }

        /// <summary>
        /// Builds deterministic priority orders useful for corridors, side bays, and compact graphs.
        /// </summary>
        private static IReadOnlyList<IReadOnlyList<AgentState>> BuildPriorityOrders(
            IReadOnlyList<AgentState> agents,
            IReadOnlyDictionary<int, TimedPath> independentByAgent,
            Graph.RoadmapGraph graph)
        {
            var orders = new List<IReadOnlyList<AgentState>>();
            var original = agents.ToArray();
            var shortestFirst = agents
                .OrderBy(agent => independentByAgent.TryGetValue(agent.AgentId, out var path) ? path.Cost : double.PositiveInfinity)
                .ThenBy(agent => agent.AgentId)
                .ToArray();
            var longestFirst = shortestFirst.Reverse().ToArray();

            AddUniqueOrder(orders, agents
                .OrderBy(agent => IsStationary(agent) ? 1 : 0)
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, agents
                .OrderBy(agent => PrimaryAxisDelta(graph, agent))
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, agents
                .OrderByDescending(agent => PrimaryAxisDelta(graph, agent))
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, agents
                .OrderBy(agent => StartX(graph, agent))
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, agents
                .OrderByDescending(agent => StartX(graph, agent))
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, agents
                .OrderBy(agent => GoalX(graph, agent))
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, agents
                .OrderByDescending(agent => GoalX(graph, agent))
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, agents
                .OrderBy(agent => StartY(graph, agent))
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, agents
                .OrderByDescending(agent => StartY(graph, agent))
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, agents
                .OrderByDescending(agent => Math.Abs(PrimaryAxisDelta(graph, agent)))
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, shortestFirst);
            AddUniqueOrder(orders, longestFirst);
            AddUniqueOrder(orders, agents
                .OrderBy(agent => IsStationary(agent) ? 0 : 1)
                .ThenBy(agent => agent.AgentId)
                .ToArray());
            AddUniqueOrder(orders, original);

            if (agents.Count <= 4 || agents.Count <= 5 && graph.Count <= 20)
            {
                foreach (var permutation in Permute(agents.ToArray(), 0))
                    AddUniqueOrder(orders, permutation.ToArray());
            }

            return orders;
        }

        private static bool IsStationary(AgentState agent)
        {
            return agent.StartNodeId == agent.GoalNodeId;
        }

        private static double StartX(Graph.RoadmapGraph graph, AgentState agent)
        {
            return graph.GetNode(agent.StartNodeId).Position.X;
        }

        private static double GoalX(Graph.RoadmapGraph graph, AgentState agent)
        {
            return graph.GetNode(agent.GoalNodeId).Position.X;
        }

        private static double StartY(Graph.RoadmapGraph graph, AgentState agent)
        {
            return graph.GetNode(agent.StartNodeId).Position.Y;
        }

        private static double PrimaryAxisDelta(Graph.RoadmapGraph graph, AgentState agent)
        {
            var start = graph.GetNode(agent.StartNodeId).Position;
            var goal = graph.GetNode(agent.GoalNodeId).Position;
            var dx = goal.X - start.X;
            var dy = goal.Y - start.Y;
            return Math.Abs(dx) >= Math.Abs(dy) ? dx : dy;
        }

        private static void AddUniqueOrder(List<IReadOnlyList<AgentState>> orders, AgentState[] candidate)
        {
            var signature = string.Join(",", candidate.Select(agent => agent.AgentId));
            if (orders.Any(order => string.Join(",", order.Select(agent => agent.AgentId)) == signature))
                return;

            orders.Add(candidate);
        }

        private static IEnumerable<AgentState[]> Permute(AgentState[] agents, int index)
        {
            if (index == agents.Length)
            {
                yield return agents.ToArray();
                yield break;
            }

            for (var i = index; i < agents.Length; i++)
            {
                Swap(agents, index, i);
                foreach (var permutation in Permute(agents, index + 1))
                    yield return permutation;
                Swap(agents, index, i);
            }
        }

        private static void Swap(AgentState[] agents, int a, int b)
        {
            if (a == b)
                return;

            var temp = agents[a];
            agents[a] = agents[b];
            agents[b] = temp;
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

        /// <summary>
        /// Adds a constraint branch for one agent, replans that agent, and inserts the new CBS node if valid.
        /// </summary>
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
            AddOpen(open, CreateNode(paths, constraints, ReservationPaths(request), request.Settings));
        }

        /// <summary>
        /// Creates a CBS node and precomputes its conflicts for focal-style node selection.
        /// </summary>
        private CbsNode CreateNode(
            TimedPath[] paths,
            IReadOnlyList<Constraint> constraints,
            IReadOnlyList<TimedPath> reservations,
            MapfPlannerSettings settings)
        {
            var conflicts = _conflicts.FindAllConflicts(WithReservations(paths, reservations), settings);
            return new CbsNode(paths, constraints, Flowtime(paths), conflicts);
        }

        private static Conflict SelectConflict(IReadOnlyList<Conflict> conflicts)
        {
            return conflicts.Count == 0 ? default : conflicts[0];
        }

        /// <summary>
        /// Merges a newly planned suffix with an existing committed prefix during replanning.
        /// </summary>
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

        private static bool TryAddConstraint(List<Constraint> constraints, HashSet<string> keys, Constraint constraint)
        {
            var key = ConstraintKey(constraint);
            if (!keys.Add(key))
                return false;

            constraints.Add(constraint);
            return true;
        }

        private static string ConstraintKey(Constraint constraint)
        {
            return $"{constraint.AgentId}:{constraint.FromNodeId}>{constraint.ToNodeId}:{constraint.StartTime:0.######}-{constraint.EndTime:0.######}";
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
            var index = open.FindIndex(n =>
                n.Cost > node.Cost ||
                Math.Abs(n.Cost - node.Cost) < 1e-6 && n.ConflictCount > node.ConflictCount ||
                Math.Abs(n.Cost - node.Cost) < 1e-6 && n.ConflictCount == node.ConflictCount && n.ConstraintCount > node.ConstraintCount);
            if (index < 0)
                open.Add(node);
            else
                open.Insert(index, node);
        }

        private static CbsNode PopBestNode(List<CbsNode> open)
        {
            var minCost = open[0].Cost;
            var costBound = minCost * HighLevelFocalWeight + 1e-6;
            var bestIndex = 0;
            for (var i = 1; i < open.Count; i++)
            {
                var candidate = open[i];
                if (candidate.Cost > costBound)
                    break;

                var best = open[bestIndex];
                if (candidate.ConflictCount < best.ConflictCount ||
                    candidate.ConflictCount == best.ConflictCount && candidate.ConstraintCount < best.ConstraintCount ||
                    candidate.ConflictCount == best.ConflictCount && candidate.ConstraintCount == best.ConstraintCount && candidate.Cost < best.Cost)
                {
                    bestIndex = i;
                }
            }

            var node = open[bestIndex];
            open.RemoveAt(bestIndex);
            return node;
        }

        /// <summary>
        /// Immutable high-level CBS search node containing one path per agent and the active constraints.
        /// </summary>
        private sealed class CbsNode
        {
            public readonly TimedPath[] Paths;
            public readonly IReadOnlyList<Constraint> Constraints;
            public readonly double Cost;
            public readonly IReadOnlyList<Conflict> Conflicts;
            public int ConflictCount => Conflicts.Count;
            public int ConstraintCount => Constraints.Count;

            public CbsNode(TimedPath[] paths, IReadOnlyList<Constraint> constraints, double cost, IReadOnlyList<Conflict> conflicts)
            {
                Paths = paths;
                Constraints = constraints;
                Cost = cost;
                Conflicts = conflicts;
            }
        }
    }
}
