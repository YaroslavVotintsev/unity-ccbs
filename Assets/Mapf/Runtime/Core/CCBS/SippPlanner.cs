using System;
using System.Collections.Generic;
using System.Linq;
using Mapf.Core.Graph;
using Mapf.Core.Model;
using Mapf.Core.Planning;

namespace Mapf.Core.CCBS
{
    /// <summary>
    /// Low-level single-agent planner using safe intervals derived from node constraints and edge-start constraints.
    /// </summary>
    internal sealed class SippPlanner
    {
        private readonly HeuristicTable _heuristics = new();

        /// <summary>
        /// Finds a timed path for one agent that satisfies that agent's constraints.
        /// </summary>
        public TimedPath FindPath(RoadmapGraph graph, AgentState agent, IEnumerable<Constraint> constraints, MapfPlannerSettings settings)
        {
            var cons = constraints.Where(c => c.AgentId == agent.AgentId).ToArray();
            var safeIntervals = BuildSafeIntervals(graph.Count, cons, settings.Epsilon);
            var open = new List<SearchState>();
            var bestArrival = new Dictionary<StateKey, double>();
            var expanded = 0;

            foreach (var interval in safeIntervals[agent.StartNodeId])
            {
                if (!interval.Contains(agent.EarliestStartTime, settings.Epsilon))
                    continue;

                var start = new SearchState(
                    agent.StartNodeId,
                    agent.EarliestStartTime,
                    agent.EarliestStartTime,
                    interval.Start,
                    interval.End,
                    interval.Id,
                    0,
                    null);
                AddOrUpdate(open, bestArrival, start, SearchScore(graph, start, agent.GoalNodeId, settings), settings);
                break;
            }

            while (open.Count > 0)
            {
                if (++expanded > settings.MaxLowLevelNodes)
                    break;

                var current = open[0];
                open.RemoveAt(0);

                if (current.NodeId == agent.GoalNodeId && double.IsPositiveInfinity(current.IntervalEnd))
                    return BuildPath(graph, agent.AgentId, current);

                foreach (var edge in graph.GetNeighbors(current.NodeId))
                {
                    if (double.IsPositiveInfinity(current.Time))
                        continue;

                    var travel = graph.TravelTime(current.NodeId, edge.To, settings.AgentSpeed);
                    foreach (var targetInterval in safeIntervals[edge.To])
                    {
                        if (!TryScheduleMove(
                                current,
                                edge.To,
                                travel,
                                targetInterval,
                                cons,
                                settings,
                                out var depart,
                                out var arrive))
                        {
                            continue;
                        }

                        var next = new SearchState(
                            edge.To,
                            arrive,
                            depart,
                            targetInterval.Start,
                            targetInterval.End,
                            targetInterval.Id,
                            current.TravelDistance + edge.Length,
                            current);
                        AddOrUpdate(open, bestArrival, next, SearchScore(graph, next, agent.GoalNodeId, settings), settings);
                    }
                }
            }

            return new TimedPath(agent.AgentId, Array.Empty<TimedPathPoint>());
        }

        private static bool TryScheduleMove(
            SearchState current,
            int to,
            double travel,
            SafeInterval targetInterval,
            IReadOnlyList<Constraint> constraints,
            MapfPlannerSettings settings,
            out double depart,
            out double arrive)
        {
            depart = Math.Max(current.Time, targetInterval.Start - travel);
            if (depart < current.Time)
                depart = current.Time;
            if (double.IsPositiveInfinity(depart) || double.IsNaN(depart))
            {
                arrive = 0;
                return false;
            }

            for (var guard = 0; guard < 256; guard++)
            {
                arrive = depart + travel;
                if (depart > current.IntervalEnd + settings.Epsilon || arrive > targetInterval.End + settings.Epsilon)
                    return false;

                if (arrive < targetInterval.Start - settings.Epsilon)
                {
                    depart = targetInterval.Start - travel;
                    continue;
                }

                var changed = false;
                foreach (var constraint in constraints)
                {
                    if (constraint.IsNodeConstraint)
                        continue;

                    if (constraint.FromNodeId != current.NodeId || constraint.ToNodeId != to)
                        continue;

                    if (depart + settings.Epsilon >= constraint.StartTime && depart < constraint.EndTime - settings.Epsilon)
                    {
                        depart = constraint.EndTime;
                        if (double.IsPositiveInfinity(depart) || double.IsNaN(depart))
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

        private static IReadOnlyList<SafeInterval>[] BuildSafeIntervals(int nodeCount, IReadOnlyList<Constraint> constraints, double eps)
        {
            var blocked = new List<Interval>[nodeCount];
            for (var i = 0; i < nodeCount; i++)
                blocked[i] = new List<Interval>();

            foreach (var constraint in constraints)
            {
                if (!constraint.IsNodeConstraint)
                    continue;

                if (constraint.FromNodeId < 0 || constraint.FromNodeId >= nodeCount)
                    continue;

                blocked[constraint.FromNodeId].Add(new Interval(Math.Max(0, constraint.StartTime), constraint.EndTime));
            }

            var result = new IReadOnlyList<SafeInterval>[nodeCount];
            for (var node = 0; node < nodeCount; node++)
            {
                var merged = Merge(blocked[node], eps);
                var safe = new List<SafeInterval>();
                var start = 0.0;
                var id = 0;
                foreach (var interval in merged)
                {
                    if (interval.Start > start + eps)
                        safe.Add(new SafeInterval(id++, start, interval.Start));
                    start = Math.Max(start, interval.End);
                }

                safe.Add(new SafeInterval(id, start, double.PositiveInfinity));
                result[node] = safe;
            }

            return result;
        }

        private static IReadOnlyList<Interval> Merge(IEnumerable<Interval> intervals, double eps)
        {
            var ordered = intervals
                .Where(i => i.End > i.Start + eps)
                .OrderBy(i => i.Start)
                .ToArray();
            var result = new List<Interval>();
            foreach (var interval in ordered)
            {
                if (result.Count == 0 || interval.Start > result[result.Count - 1].End + eps)
                {
                    result.Add(interval);
                    continue;
                }

                var last = result[result.Count - 1];
                result[result.Count - 1] = new Interval(last.Start, Math.Max(last.End, interval.End));
            }

            return result;
        }

        private double Estimate(RoadmapGraph graph, int nodeId, int goalId, MapfPlannerSettings settings)
        {
            var distance = _heuristics.Get(graph, nodeId, goalId);
            return double.IsPositiveInfinity(distance) ? distance : distance / settings.AgentSpeed;
        }

        private double SearchScore(RoadmapGraph graph, SearchState state, int goalNodeId, MapfPlannerSettings settings)
        {
            return state.Time + Estimate(graph, state.NodeId, goalNodeId, settings) + BacktrackPenalty(state);
        }

        private static double BacktrackPenalty(SearchState state)
        {
            if (state.Parent?.Parent == null)
                return 0;

            return state.NodeId == state.Parent.Parent.NodeId ? 0.01 : 0;
        }

        private static void AddOrUpdate(
            List<SearchState> open,
            Dictionary<StateKey, double> bestArrival,
            SearchState state,
            double f,
            MapfPlannerSettings settings)
        {
            var key = StateKey.From(state.NodeId, state.IntervalId, state.Time, settings);
            if (bestArrival.TryGetValue(key, out var best) && best <= state.Time + settings.Epsilon)
                return;

            bestArrival[key] = state.Time;
            state.F = f;
            var index = open.FindIndex(s => s.F > f || Math.Abs(s.F - f) < settings.Epsilon && s.Time < state.Time);
            if (index < 0)
                open.Add(state);
            else
                open.Insert(index, state);
        }

        private static TimedPath BuildPath(RoadmapGraph graph, int agentId, SearchState goal)
        {
            var states = new List<SearchState>();
            for (var state = goal; state != null; state = state.Parent)
                states.Add(state);
            states.Reverse();

            var points = new List<TimedPathPoint>
            {
                new(states[0].NodeId, graph.GetNode(states[0].NodeId).Position, states[0].Time)
            };

            for (var i = 1; i < states.Count; i++)
            {
                var prev = states[i - 1];
                var cur = states[i];
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
            }

            points.Add(point);
        }

        private readonly struct Interval
        {
            public readonly double Start;
            public readonly double End;

            public Interval(double start, double end)
            {
                Start = start;
                End = end;
            }
        }

        private readonly struct SafeInterval
        {
            public readonly int Id;
            public readonly double Start;
            public readonly double End;

            public SafeInterval(int id, double start, double end)
            {
                Id = id;
                Start = start;
                End = end;
            }

            public bool Contains(double time, double eps)
            {
                return time + eps >= Start && time < End - eps;
            }
        }

        private sealed class SearchState
        {
            public readonly int NodeId;
            public readonly double Time;
            public readonly double DepartTime;
            public readonly double IntervalStart;
            public readonly double IntervalEnd;
            public readonly int IntervalId;
            public readonly double TravelDistance;
            public readonly SearchState Parent;
            public double F;

            public SearchState(
                int nodeId,
                double time,
                double departTime,
                double intervalStart,
                double intervalEnd,
                int intervalId,
                double travelDistance,
                SearchState parent)
            {
                NodeId = nodeId;
                Time = time;
                DepartTime = departTime;
                IntervalStart = intervalStart;
                IntervalEnd = intervalEnd;
                IntervalId = intervalId;
                TravelDistance = travelDistance;
                Parent = parent;
            }
        }

        private readonly struct StateKey : IEquatable<StateKey>
        {
            private readonly int _nodeId;
            private readonly int _intervalId;
            private readonly long _timeBucket;

            private StateKey(int nodeId, int intervalId, long timeBucket)
            {
                _nodeId = nodeId;
                _intervalId = intervalId;
                _timeBucket = timeBucket;
            }

            public static StateKey From(int nodeId, int intervalId, double time, MapfPlannerSettings settings)
            {
                var bucketSize = Math.Max(settings.Epsilon * 1000, 1e-3);
                return new StateKey(nodeId, intervalId, (long)Math.Round(time / bucketSize));
            }

            public bool Equals(StateKey other) => _nodeId == other._nodeId && _intervalId == other._intervalId && _timeBucket == other._timeBucket;
            public override bool Equals(object obj) => obj is StateKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_nodeId, _intervalId, _timeBucket);
        }
    }
}
