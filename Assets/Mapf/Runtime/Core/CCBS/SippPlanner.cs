using System;
using System.Collections.Generic;
using System.Linq;
using Mapf.Core.Graph;
using Mapf.Core.Model;
using Mapf.Core.Planning;

namespace Mapf.Core.CCBS
{
    internal sealed class SippPlanner
    {
        private readonly HeuristicTable _heuristics = new();

        public TimedPath FindPath(RoadmapGraph graph, AgentState agent, IEnumerable<Constraint> constraints, MapfPlannerSettings settings)
        {
            var cons = constraints.Where(c => c.AgentId == agent.AgentId).ToArray();
            var open = new List<SearchState>();
            var visited = new HashSet<StateKey>();
            var start = new SearchState(agent.StartNodeId, agent.EarliestStartTime, 0, 0, null);
            AddOpen(open, start, Estimate(graph, agent.StartNodeId, agent.GoalNodeId, settings));
            visited.Add(StateKey.From(start.NodeId, start.Time, settings.Epsilon));
            var expanded = 0;

            while (open.Count > 0)
            {
                if (++expanded > settings.MaxLowLevelNodes)
                    break;

                var current = open[0];
                open.RemoveAt(0);

                if (current.NodeId == agent.GoalNodeId && !ViolatesNodeConstraint(cons, current.NodeId, current.Time, double.PositiveInfinity, settings.Epsilon))
                    return BuildPath(graph, agent.AgentId, current);

                TryAddWaitSuccessor(open, visited, current, graph, agent.GoalNodeId, cons, settings);

                foreach (var edge in graph.GetNeighbors(current.NodeId))
                {
                    if (!TryScheduleMove(graph, current.NodeId, edge.To, current.Time, cons, settings, out var depart, out var arrive))
                        continue;

                    var key = StateKey.From(edge.To, arrive, settings.Epsilon);
                    if (!visited.Add(key))
                        continue;

                    var next = new SearchState(edge.To, arrive, depart, current.TravelDistance + edge.Length, current);
                    AddOpen(open, next, SearchScore(next, graph, agent.GoalNodeId, settings));
                }
            }

            return new TimedPath(agent.AgentId, Array.Empty<TimedPathPoint>());
        }

        private static void TryAddWaitSuccessor(
            List<SearchState> open,
            HashSet<StateKey> visited,
            SearchState current,
            RoadmapGraph graph,
            int goalNodeId,
            IReadOnlyList<Constraint> constraints,
            MapfPlannerSettings settings)
        {
            var waitUntil = NextWaitTime(current.Time, constraints, settings);
            if (waitUntil <= current.Time + settings.Epsilon)
                return;

            if (ViolatesNodeConstraint(constraints, current.NodeId, current.Time, waitUntil, settings.Epsilon))
                return;

            var key = StateKey.From(current.NodeId, waitUntil, settings.Epsilon);
            if (!visited.Add(key))
                return;

            var next = new SearchState(current.NodeId, waitUntil, waitUntil, current.TravelDistance, current);
            AddOpen(open, next, SearchScore(next, graph, goalNodeId, settings));
        }

        private static double NextWaitTime(double currentTime, IReadOnlyList<Constraint> constraints, MapfPlannerSettings settings)
        {
            var best = double.PositiveInfinity;
            foreach (var constraint in constraints)
            {
                if (constraint.EndTime > currentTime + settings.Epsilon && constraint.EndTime < best - settings.Epsilon)
                    best = constraint.EndTime;
            }

            return best;
        }

        private double Estimate(RoadmapGraph graph, int nodeId, int goalId, MapfPlannerSettings settings)
        {
            var distance = _heuristics.Get(graph, nodeId, goalId);
            return double.IsPositiveInfinity(distance) ? distance : distance / settings.AgentSpeed;
        }

        private static bool TryScheduleMove(
            RoadmapGraph graph,
            int from,
            int to,
            double earliestDepart,
            IReadOnlyList<Constraint> constraints,
            MapfPlannerSettings settings,
            out double depart,
            out double arrive)
        {
            var travel = graph.TravelTime(from, to, settings.AgentSpeed);
            depart = earliestDepart;

            for (var guard = 0; guard < 256; guard++)
            {
                if (ViolatesNodeConstraint(constraints, from, earliestDepart, depart, settings.Epsilon))
                {
                    arrive = 0;
                    return false;
                }

                var changed = false;
                foreach (var constraint in constraints)
                {
                    if (constraint.FromNodeId == from && constraint.ToNodeId == to &&
                        depart + settings.Epsilon >= constraint.StartTime &&
                        depart < constraint.EndTime - settings.Epsilon)
                    {
                        depart = constraint.EndTime;
                        changed = true;
                    }
                }

                arrive = depart + travel;
                if (double.IsPositiveInfinity(arrive) || double.IsNaN(arrive))
                    return false;

                foreach (var constraint in constraints)
                {
                    if (!constraint.IsNodeConstraint || constraint.FromNodeId != to)
                        continue;

                    if (arrive + settings.Epsilon >= constraint.StartTime &&
                        arrive < constraint.EndTime - settings.Epsilon)
                    {
                        depart = constraint.EndTime - travel;
                        if (depart < earliestDepart - settings.Epsilon)
                        {
                            arrive = 0;
                            return false;
                        }

                        changed = true;
                    }
                }

                if (!changed)
                    return true;
            }

            arrive = 0;
            return false;
        }

        private static bool ViolatesNodeConstraint(IReadOnlyList<Constraint> constraints, int nodeId, double waitStart, double waitEnd, double eps)
        {
            foreach (var constraint in constraints)
            {
                if (!constraint.IsNodeConstraint || constraint.FromNodeId != nodeId)
                    continue;

                var overlapStart = Math.Max(waitStart, constraint.StartTime);
                var overlapEnd = Math.Min(waitEnd, constraint.EndTime);
                if (overlapStart < overlapEnd - eps)
                    return true;
            }

            return false;
        }

        private static TimedPath BuildPath(RoadmapGraph graph, int agentId, SearchState goal)
        {
            var states = new List<SearchState>();
            for (var state = goal; state != null; state = state.Parent)
                states.Add(state);
            states.Reverse();

            var points = new List<TimedPathPoint>();
            points.Add(new TimedPathPoint(states[0].NodeId, graph.GetNode(states[0].NodeId).Position, states[0].Time));
            for (var i = 1; i < states.Count; i++)
            {
                var prev = states[i - 1];
                var cur = states[i];
                if (cur.NodeId == prev.NodeId)
                {
                    AddPoint(points, new TimedPathPoint(cur.NodeId, graph.GetNode(cur.NodeId).Position, cur.Time));
                    continue;
                }

                if (cur.DepartTime > prev.Time + 1e-6)
                    AddPoint(points, new TimedPathPoint(prev.NodeId, graph.GetNode(prev.NodeId).Position, cur.DepartTime));
                AddPoint(points, new TimedPathPoint(cur.NodeId, graph.GetNode(cur.NodeId).Position, cur.Time));
            }

            return new TimedPath(agentId, CompressConsecutiveWaits(points));
        }

        private static IReadOnlyList<TimedPathPoint> CompressConsecutiveWaits(IReadOnlyList<TimedPathPoint> points)
        {
            if (points.Count < 3)
                return points;

            var compressed = new List<TimedPathPoint> { points[0] };
            for (var i = 1; i < points.Count; i++)
            {
                var point = points[i];
                var last = compressed[compressed.Count - 1];
                if (point.NodeId == last.NodeId)
                {
                    if (compressed.Count == 1 || compressed[compressed.Count - 2].NodeId != point.NodeId)
                        compressed.Add(point);
                    else
                        compressed[compressed.Count - 1] = point;
                }
                else
                {
                    compressed.Add(point);
                }
            }

            return compressed;
        }

        private static void AddPoint(List<TimedPathPoint> points, TimedPathPoint point)
        {
            if (points.Count > 0)
            {
                var last = points[points.Count - 1];
                if (last.NodeId == point.NodeId && Math.Abs(last.Time - point.Time) < 1e-6)
                    return;

                points.Add(point);
                return;
            }

            points.Add(point);
        }

        private static double SearchScore(SearchState state, RoadmapGraph graph, int goalNodeId, MapfPlannerSettings settings)
        {
            return state.Time + EstimateStatic(graph, state.NodeId, goalNodeId, settings) + state.TravelDistance;
        }

        private static double EstimateStatic(RoadmapGraph graph, int nodeId, int goalId, MapfPlannerSettings settings)
        {
            var distance = MapfVector2.Distance(graph.GetNode(nodeId).Position, graph.GetNode(goalId).Position);
            return distance / settings.AgentSpeed;
        }

        private static void AddOpen(List<SearchState> open, SearchState state, double f)
        {
            state.F = f;
            var index = open.FindIndex(s => s.F > f || Math.Abs(s.F - f) < 1e-9 && s.Time < state.Time);
            if (index < 0)
                open.Add(state);
            else
                open.Insert(index, state);
        }

        private sealed class SearchState
        {
            public readonly int NodeId;
            public readonly double Time;
            public readonly double DepartTime;
            public readonly double TravelDistance;
            public readonly SearchState Parent;
            public double F;

            public SearchState(int nodeId, double time, double departTime, double travelDistance, SearchState parent)
            {
                NodeId = nodeId;
                Time = time;
                DepartTime = departTime;
                TravelDistance = travelDistance;
                Parent = parent;
            }
        }

        private readonly struct StateKey : IEquatable<StateKey>
        {
            private readonly int _nodeId;
            private readonly long _timeBucket;

            private StateKey(int nodeId, long timeBucket)
            {
                _nodeId = nodeId;
                _timeBucket = timeBucket;
            }

            public static StateKey From(int nodeId, double time, double epsilon)
            {
                var bucketSize = Math.Max(epsilon * 1000, 1e-4);
                return new StateKey(nodeId, (long)Math.Round(time / bucketSize));
            }

            public bool Equals(StateKey other) => _nodeId == other._nodeId && _timeBucket == other._timeBucket;
            public override bool Equals(object obj) => obj is StateKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_nodeId, _timeBucket);
        }
    }
}
