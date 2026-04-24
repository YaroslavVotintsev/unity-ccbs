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
            _points.Clear();
            _points.AddRange(path.Points);
            _segmentIndex = 0;
            if (_points.Count > 0)
                transform.position = ToVector3(_points[0].Position, transform.position.z);
        }

        public AgentState GetPlanningState(int agentId, int fallbackNodeId, int goalNodeId, double now)
        {
            if (_points.Count == 0)
                return new AgentState(agentId, fallbackNodeId, goalNodeId, now);

            var next = GetNextStablePoint(now);
            return new AgentState(agentId, next.NodeId, goalNodeId, Math.Max(now, next.Time));
        }

        private void Update()
        {
            if (_points.Count < 2)
                return;

            var now = Time.timeAsDouble;
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

        private TimedPathPoint GetNextStablePoint(double now)
        {
            for (var i = 0; i < _points.Count; i++)
                if (_points[i].Time >= now - 1e-6)
                    return _points[i];

            return _points[_points.Count - 1];
        }

        private static Vector3 ToVector3(MapfVector2 p, float z) => new((float)p.X, (float)p.Y, z);
    }
}
