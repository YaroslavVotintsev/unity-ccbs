using System.Collections.Generic;
using System;
using Mapf.Core.Model;
using UnityEngine;

namespace Mapf.UnityAdapter
{
    public sealed class MapfAgentController : MonoBehaviour
    {
        private readonly List<TimedPathPoint> _points = new();
        private int _segmentIndex;

        public bool HasPlan => _points.Count > 0;
        public int CurrentPlanGoalNodeId => HasPlan ? _points[_points.Count - 1].NodeId : -1;
        public IReadOnlyList<TimedPathPoint> CurrentPoints => _points;

        public void ApplyPlan(TimedPath path)
        {
            ApplyPlan(path, Time.timeAsDouble);
        }

        public void ApplyPlan(TimedPath path, double now)
        {
            _points.Clear();
            if (path == null || path.Points.Count == 0)
                return;

            _points.AddRange(path.Points);
            MoveToPlanTime(now);
        }

        public void ApplyPlanPreservingCommittedPrefix(TimedPath path, double now)
        {
            if (path == null || path.Points.Count == 0)
                return;

            if (_points.Count == 0 || path.Points[0].Time <= now + 1e-6)
            {
                ApplyPlan(path, now);
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
            MoveToPlanTime(now);
        }

        public AgentState GetPlanningState(int agentId, int fallbackNodeId, int goalNodeId, double now)
        {
            if (_points.Count == 0)
                return new AgentState(agentId, fallbackNodeId, goalNodeId, now);

            var next = GetNextPlanningPoint(now);
            return new AgentState(agentId, next.NodeId, goalNodeId, Math.Max(now, next.Time));
        }

        public TimedPath GetPlanSnapshot(int agentId)
        {
            return new TimedPath(agentId, _points.ToArray());
        }

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
