using System.Globalization;
using System.Collections.Generic;
using Mapf.Core.Model;
using Mapf.UnityAdapter;
using UnityEngine;

namespace Mapf.Authoring
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MapfAgentController))]
    public sealed class MapfAgent : MonoBehaviour
    {
        [SerializeField] private int agentId;
        [SerializeField] private MapfNode startNode;
        [SerializeField] private MapfNode goalNode;
        [SerializeField] private float arrivalDistance = 0.05f;

        private MapfNode _lastRuntimeGoal;
        private MapfCoordinator _coordinator;
        private MapfNode _assignmentStartNode;
        private MapfNode _assignmentGoalNode;
        private float _assignmentStartTime;
        private float _assignmentDistance;
        private float _trackingStartTime;
        private float _overallDistance;
        private Vector3 _previousPosition;
        private MapfAgentController _controller;
        private double _previousPlanSampleTime;
        private bool _hasPreviousPosition;
        private bool _hasPreviousPlanSampleTime;
        private bool _hasTrackingStartTime;
        private bool _hasActiveAssignment;
        private bool _arrivalLogged;

        public int AgentId => agentId;
        public MapfNode StartNode => startNode;
        public MapfNode GoalNode => goalNode;

        public void SetGoal(MapfNode goal)
        {
            goalNode = goal;
            _lastRuntimeGoal = goal;
            BeginAssignment(goal);
        }

        public void Configure(int id, MapfNode start, MapfNode goal)
        {
            agentId = id;
            startNode = start;
            goalNode = goal;
            _lastRuntimeGoal = goal;
        }

        private void Start()
        {
            _lastRuntimeGoal = goalNode;
            _coordinator = FindAnyObjectByType<MapfCoordinator>();
            _controller = GetComponent<MapfAgentController>();
            _previousPosition = transform.position;
            _hasPreviousPosition = true;
            _previousPlanSampleTime = Time.timeAsDouble;
            _hasPreviousPlanSampleTime = true;
            if (goalNode != null)
                BeginAssignment(goalNode);
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            AccumulateMovement();
            TryLogArrival();

            if (goalNode == _lastRuntimeGoal)
                return;

            _coordinator ??= FindAnyObjectByType<MapfCoordinator>();
            if (_coordinator != null && goalNode != null)
                _coordinator.RequestAgentGoal(this, goalNode);
        }

        private void BeginAssignment(MapfNode goal)
        {
            if (!Application.isPlaying || goal == null)
                return;

            AccumulateMovement();
            var now = Time.time;
            _assignmentStartNode = NearestNode(transform.position) ?? startNode;
            _assignmentGoalNode = goal;
            _assignmentStartTime = now;
            _assignmentDistance = 0;
            _hasActiveAssignment = true;
            _arrivalLogged = false;

            if (!_hasTrackingStartTime)
            {
                _trackingStartTime = now;
                _hasTrackingStartTime = true;
            }

            _previousPosition = transform.position;
            _hasPreviousPosition = true;
            _previousPlanSampleTime = Time.timeAsDouble;
            _hasPreviousPlanSampleTime = true;
        }

        private void AccumulateMovement()
        {
            _controller ??= GetComponent<MapfAgentController>();
            if (_controller != null && _controller.HasPlan && _hasPreviousPlanSampleTime)
            {
                var now = Time.timeAsDouble;
                AddDistance((float)PlanDistanceBetween(_controller.CurrentPoints, _previousPlanSampleTime, now));
                _previousPlanSampleTime = now;
                _previousPosition = transform.position;
                _hasPreviousPosition = true;
                return;
            }

            var current = transform.position;
            if (!_hasPreviousPosition)
            {
                _previousPosition = current;
                _hasPreviousPosition = true;
                _previousPlanSampleTime = Time.timeAsDouble;
                _hasPreviousPlanSampleTime = true;
                return;
            }

            var distance = Vector2.Distance(_previousPosition, current);
            AddDistance(distance);
            _previousPosition = current;
            _previousPlanSampleTime = Time.timeAsDouble;
            _hasPreviousPlanSampleTime = true;
        }

        private void AddDistance(float distance)
        {
            if (distance <= 1e-5f)
                return;

            _overallDistance += distance;
            if (_hasActiveAssignment && !_arrivalLogged)
                _assignmentDistance += distance;
        }

        private void TryLogArrival()
        {
            if (!_hasActiveAssignment || _arrivalLogged || _assignmentGoalNode == null)
                return;

            var distance = Vector2.Distance(transform.position, _assignmentGoalNode.transform.position);
            if (distance > arrivalDistance)
                return;

            _arrivalLogged = true;
            var assignmentTime = Time.time - _assignmentStartTime;
            var overallRuntime = _hasTrackingStartTime ? Time.time - _trackingStartTime : 0f;
            Debug.Log(
                "MAPF agent arrived: " +
                $"agent={agentId} " +
                $"start=\"{NodeId(_assignmentStartNode)}\" " +
                $"arrived=\"{NodeId(_assignmentGoalNode)}\" " +
                $"assignmentTime={FormatDecimal(assignmentTime)} " +
                $"assignmentDistance={FormatDecimal(_assignmentDistance)} " +
                $"overallRuntime={FormatDecimal(overallRuntime)} " +
                $"overallDistance={FormatDecimal(_overallDistance)}",
                this);
        }

        private static MapfNode NearestNode(Vector3 position)
        {
            var allNodes = FindObjectsByType<MapfNode>(FindObjectsInactive.Exclude);
            MapfNode best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var node in allNodes)
            {
                var distance = Vector2.Distance(position, node.transform.position);
                if (distance >= bestDistance)
                    continue;

                best = node;
                bestDistance = distance;
            }

            return best;
        }

        private static string NodeId(MapfNode node)
        {
            return node != null ? node.StableId : "<unknown>";
        }

        private static string FormatDecimal(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static double PlanDistanceBetween(IReadOnlyList<TimedPathPoint> points, double fromTime, double toTime)
        {
            if (points == null || points.Count < 2 || toTime <= fromTime)
                return 0;

            var distance = 0.0;
            for (var i = 0; i + 1 < points.Count; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var duration = b.Time - a.Time;
                if (duration <= 1e-6 || a.NodeId == b.NodeId)
                    continue;

                var start = System.Math.Max(fromTime, a.Time);
                var end = System.Math.Min(toTime, b.Time);
                if (end <= start)
                    continue;

                distance += MapfVector2.Distance(a.Position, b.Position) * ((end - start) / duration);
            }

            return distance;
        }
    }
}
