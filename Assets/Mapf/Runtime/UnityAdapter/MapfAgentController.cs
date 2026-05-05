using System.Collections.Generic;
using System;
using Mapf.Core.Model;
using UnityEngine;

namespace Mapf.UnityAdapter
{
    /// <summary>
    /// Applies timed roadmap plans to an agent transform and exposes committed motion for replanning.
    /// </summary>
    public sealed class MapfAgentController : MonoBehaviour
    {
        private readonly List<TimedPathPoint> _points = new();
        private int _segmentIndex;

        public bool HasPlan => _points.Count > 0;
        public int CurrentPlanGoalNodeId => HasPlan ? _points[_points.Count - 1].NodeId : -1;
        public IReadOnlyList<TimedPathPoint> CurrentPoints => _points;

        /// <summary>
        /// Applies a plan using the current Unity time.
        /// </summary>
        public void ApplyPlan(TimedPath path)
        {
            ApplyPlan(path, Time.timeAsDouble);
        }

        /// <summary>
        /// Applies an absolute-time plan and moves the transform to the corresponding point on it.
        /// </summary>
        public void ApplyPlan(TimedPath path, double now)
        {
            _points.Clear();
            if (path == null || path.Points.Count == 0)
                return;

            _points.AddRange(path.Points);
            _segmentIndex = 0;
            MoveToPlanTime(now);
        }

        /// <summary>
        /// Applies a new plan by rebasing its first timestamp to the supplied Unity time.
        /// </summary>
        public void ApplyPlanStartingNow(TimedPath path, double now)
        {
            _points.Clear();
            if (path == null || path.Points.Count == 0)
                return;

            _points.AddRange(RebasedPoints(path.Points, now));
            _segmentIndex = 0;
            MoveToPlanTime(now);
        }

        /// <summary>
        /// Applies a replanned suffix while preserving the currently committed edge traversal.
        /// </summary>
        public void ApplyPlanPreservingCommittedPrefix(TimedPath path, double now)
        {
            if (path == null || path.Points.Count == 0)
                return;

            if (_points.Count == 0)
            {
                ApplyPlanStartingNow(path, now);
                return;
            }

            if (path.Points[0].Time <= now + 1e-6)
            {
                ApplyPlanFromCurrentPosition(path, now);
                return;
            }

            var switchPoint = path.Points[0];
            var merged = new List<TimedPathPoint>();
            foreach (var point in _points)
            {
                if (point.Time < switchPoint.Time - 1e-6)
                    merged.Add(point);
            }

            if (merged.Count == 0 || merged[merged.Count - 1].NodeId != switchPoint.NodeId || Math.Abs(merged[merged.Count - 1].Time - switchPoint.Time) > 1e-6)
                merged.Add(switchPoint);

            for (var i = 1; i < path.Points.Count; i++)
                merged.Add(path.Points[i]);

            _points.Clear();
            _points.AddRange(merged);
            _segmentIndex = 0;
            MoveToPlanTime(now);
        }

        private void ApplyPlanFromCurrentPosition(TimedPath path, double now)
        {
            if (!TryGetCurrentSegment(now, out var currentSegmentStart, out var currentSegmentEnd))
            {
                MoveToPlanTime(now);
                return;
            }

            if (currentSegmentStart.NodeId == currentSegmentEnd.NodeId)
            {
                ApplyStationaryReplan(path, currentSegmentStart, now);
                return;
            }

            if (currentSegmentEnd.Time <= now + 1e-6)
            {
                ApplyStationaryReplan(path, new TimedPathPoint(currentSegmentEnd.NodeId, currentSegmentEnd.Position, now), now);
                return;
            }

            var current = GetPositionPointAt(now) ??
                new TimedPathPoint(currentSegmentStart.NodeId, currentSegmentStart.Position, now);
            var merged = new List<TimedPathPoint> { current };
            merged.Add(currentSegmentEnd);

            var suffixStart = FindHandoffIndex(path.Points, currentSegmentEnd);
            if (suffixStart < 0)
            {
                AppendExistingSuffix(merged, currentSegmentEnd);
            }
            else
            {
                for (var i = suffixStart; i < path.Points.Count; i++)
                {
                    var point = path.Points[i];
                    if (point.Time <= currentSegmentEnd.Time + 1e-6)
                        continue;

                    merged.Add(point);
                }
            }

            _points.Clear();
            _points.AddRange(merged);
            _segmentIndex = 0;
            MoveToPlanTime(now);
        }

        private void ApplyStationaryReplan(TimedPath path, TimedPathPoint current, double now)
        {
            var matchIndex = FindMatchingPositionIndex(path.Points, current.Position, now);
            if (matchIndex < 0)
            {
                MoveToPlanTime(now);
                return;
            }

            var offset = now - path.Points[matchIndex].Time;
            var merged = new List<TimedPathPoint>
            {
                new(current.NodeId, current.Position, now)
            };

            for (var i = matchIndex + 1; i < path.Points.Count; i++)
            {
                var point = path.Points[i];
                merged.Add(new TimedPathPoint(point.NodeId, point.Position, point.Time + offset));
            }

            _points.Clear();
            _points.AddRange(merged);
            _segmentIndex = 0;
            MoveToPlanTime(now);
        }

        private void AppendExistingSuffix(List<TimedPathPoint> merged, TimedPathPoint handoff)
        {
            for (var i = 0; i < _points.Count; i++)
            {
                var point = _points[i];
                if (point.Time <= handoff.Time + 1e-6)
                    continue;

                merged.Add(point);
            }
        }

        private bool TryGetCurrentSegment(double now, out TimedPathPoint start, out TimedPathPoint end)
        {
            start = default;
            end = default;

            if (_points.Count == 0)
                return false;

            if (_points.Count == 1 || now <= _points[0].Time + 1e-6)
            {
                start = _points[0];
                end = _points[0];
                return true;
            }

            for (var i = 0; i + 1 < _points.Count; i++)
            {
                var a = _points[i];
                var b = _points[i + 1];
                if (now < a.Time - 1e-6 || now > b.Time + 1e-6)
                    continue;

                start = a;
                end = b;
                return true;
            }

            start = _points[_points.Count - 1];
            end = _points[_points.Count - 1];
            return true;
        }

        private static int FindHandoffIndex(IReadOnlyList<TimedPathPoint> points, TimedPathPoint handoff)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.NodeId == handoff.NodeId && Math.Abs(point.Time - handoff.Time) < 1e-5)
                    return i + 1;
            }

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.NodeId == handoff.NodeId && point.Time >= handoff.Time - 1e-6)
                    return i + 1;
            }

            return -1;
        }

        private static int FindMatchingPositionIndex(IReadOnlyList<TimedPathPoint> points, MapfVector2 position, double now)
        {
            var bestIndex = -1;
            var bestTime = double.NegativeInfinity;
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.Time > now + 1e-5)
                    continue;

                if (point.Time <= bestTime)
                    continue;

                if (MapfVector2.Distance(point.Position, position) < 1e-5)
                {
                    bestIndex = i;
                    bestTime = point.Time;
                }
            }

            return bestIndex;
        }

        private static IReadOnlyList<TimedPathPoint> RebasedPoints(IReadOnlyList<TimedPathPoint> points, double now)
        {
            if (points.Count == 0)
                return points;

            var offset = now - points[0].Time;
            if (Math.Abs(offset) < 1e-6)
                return points;

            var rebased = new TimedPathPoint[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                rebased[i] = new TimedPathPoint(point.NodeId, point.Position, point.Time + offset);
            }

            return rebased;
        }

        /// <summary>
        /// Returns the planner start state for this agent at a replanning instant.
        /// Mid-edge agents report the next stable node and its arrival time.
        /// </summary>
        public AgentState GetPlanningState(int agentId, int fallbackNodeId, int goalNodeId, double now)
        {
            if (_points.Count == 0)
                return new AgentState(agentId, fallbackNodeId, goalNodeId, now);

            var next = GetNextPlanningPoint(now);
            return new AgentState(agentId, next.NodeId, goalNodeId, Math.Max(now, next.Time));
        }

        /// <summary>
        /// Creates a pure timed-path snapshot of the current controller plan.
        /// </summary>
        public TimedPath GetPlanSnapshot(int agentId)
        {
            return new TimedPath(agentId, _points.ToArray());
        }

        /// <summary>
        /// Returns the current non-interruptible edge segment as a reservation, if the agent is mid-edge.
        /// </summary>
        public Reservation? GetCommittedReservation(int agentId, double now)
        {
            if (_points.Count < 2)
                return null;

            for (var i = 0; i + 1 < _points.Count; i++)
            {
                var a = _points[i];
                var b = _points[i + 1];
                if (now < a.Time - 1e-6 || now > b.Time + 1e-6)
                    continue;

                if (a.NodeId == b.NodeId)
                    return null;

                var start = GetPositionPointAt(now);
                if (!start.HasValue)
                    return null;

                return new Reservation(
                    agentId,
                    new TimedPath(agentId, new[] { start.Value, b }, reservesGoalAfterArrival: false));
            }

            return null;
        }

        private void Update()
        {
            if (_points.Count < 2)
                return;

            MoveToPlanTime(Time.timeAsDouble);
        }

        private void MoveToPlanTime(double now)
        {
            if (_points.Count == 0)
                return;

            if (_points.Count == 1)
            {
                _segmentIndex = 0;
                transform.position = ToVector3(_points[0].Position, transform.position.z);
                return;
            }

            _segmentIndex = Math.Min(_segmentIndex, _points.Count - 2);
            while (_segmentIndex + 1 < _points.Count && now > _points[_segmentIndex + 1].Time)
                _segmentIndex++;

            if (_segmentIndex + 1 >= _points.Count)
            {
                transform.position = ToVector3(_points[_points.Count - 1].Position, transform.position.z);
                return;
            }

            var a = _points[_segmentIndex];
            var b = _points[_segmentIndex + 1];
            var duration = b.Time - a.Time;
            var t = duration <= 1e-6 ? 1 : Mathf.Clamp01((float)((now - a.Time) / duration));
            var position = MapfVector2.Lerp(a.Position, b.Position, t);
            transform.position = ToVector3(position, transform.position.z);
        }

        private TimedPathPoint GetNextPlanningPoint(double now)
        {
            if (_points.Count == 1 || now >= _points[_points.Count - 1].Time - 1e-6)
                return new TimedPathPoint(_points[_points.Count - 1].NodeId, _points[_points.Count - 1].Position, now);

            for (var i = 0; i + 1 < _points.Count; i++)
            {
                var a = _points[i];
                var b = _points[i + 1];
                if (now < a.Time - 1e-6 || now > b.Time + 1e-6)
                    continue;

                if (a.NodeId == b.NodeId)
                    return new TimedPathPoint(a.NodeId, a.Position, now);

                return b;
            }

            return _points[_points.Count - 1];
        }

        private TimedPathPoint? GetPositionPointAt(double now)
        {
            if (_points.Count == 0)
                return null;

            if (_points.Count == 1 || now <= _points[0].Time + 1e-6)
                return _points[0];

            for (var i = 0; i + 1 < _points.Count; i++)
            {
                var a = _points[i];
                var b = _points[i + 1];
                if (now < a.Time - 1e-6 || now > b.Time + 1e-6)
                    continue;

                if (a.NodeId == b.NodeId)
                    return new TimedPathPoint(a.NodeId, a.Position, now);

                var duration = b.Time - a.Time;
                var t = duration <= 1e-6 ? 1 : (now - a.Time) / duration;
                return new TimedPathPoint(a.NodeId, MapfVector2.Lerp(a.Position, b.Position, t), now);
            }

            return _points[_points.Count - 1];
        }

        private static Vector3 ToVector3(MapfVector2 p, float z) => new((float)p.X, (float)p.Y, z);
    }
}
