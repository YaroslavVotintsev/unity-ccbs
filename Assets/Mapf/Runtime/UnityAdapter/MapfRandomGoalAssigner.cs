using System;
using System.Collections.Generic;
using System.Linq;
using Mapf.Authoring;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Mapf.UnityAdapter
{
    public sealed class MapfRandomGoalAssigner : MonoBehaviour
    {
        [SerializeField] private MapfCoordinator coordinator;
        [SerializeField] private List<MapfAgent> agents = new();
        [SerializeField] private List<MapfNode> destinationNodes = new();
        [SerializeField] private float waitSecondsAtDestination = 2f;
        [SerializeField] private float arrivalDistance = 0.05f;
        [SerializeField] private bool assignOnStart = true;
        [SerializeField] private bool avoidCurrentGoal = true;
        [SerializeField] private bool useDeterministicSeed;
        [SerializeField] private int randomSeed = 12345;

        private readonly Dictionary<MapfAgent, AgentRuntimeState> _states = new();

        private void Start()
        {
            coordinator ??= FindAnyObjectByType<MapfCoordinator>();
            if (useDeterministicSeed)
                Random.InitState(randomSeed);

            RefreshAgents();
            ValidateConfiguration();

            if (!assignOnStart)
                return;

            foreach (var agent in _states.Keys.ToArray())
                AssignRandomGoal(agent);
        }

        private void Update()
        {
            if (destinationNodes.Count == 0)
                return;

            RefreshAgents();

            foreach (var agent in _states.Keys.ToArray())
            {
                if (agent == null)
                    continue;

                var state = _states[agent];
                if (!HasReachedGoal(agent))
                {
                    state.ArrivalTime = null;
                    _states[agent] = state;
                    continue;
                }

                if (!state.ArrivalTime.HasValue)
                {
                    state.ArrivalTime = Time.time;
                    _states[agent] = state;
                    continue;
                }

                if (Time.time - state.ArrivalTime.Value < waitSecondsAtDestination)
                    continue;

                AssignRandomGoal(agent);
            }
        }

        private void AssignRandomGoal(MapfAgent agent)
        {
            if (agent == null || coordinator == null || destinationNodes.Count == 0)
                return;

            var goal = PickGoal(agent);
            if (goal == null)
                return;

            var start = NearestNode(agent.transform.position);
            Debug.Log(
                $"MAPF random goal assigned: agent={agent.AgentId} start=\"{NodeId(start)}\" destination=\"{NodeId(goal)}\"",
                agent);

            coordinator.RequestAgentGoal(agent, goal);
            var state = _states.TryGetValue(agent, out var existing) ? existing : NewState(agent);
            state.ArrivalTime = null;
            state.AssignedGoal = goal;
            _states[agent] = state;
        }

        private MapfNode PickGoal(MapfAgent agent)
        {
            var occupiedGoals = _states
                .Where(pair => pair.Key != null && pair.Key != agent)
                .Select(pair => pair.Value.AssignedGoal != null ? pair.Value.AssignedGoal : pair.Key.GoalNode)
                .Where(node => node != null)
                .ToHashSet();
            var occupiedNodes = _states.Keys
                .Where(other => other != null && other != agent)
                .Select(CurrentOccupiedNode)
                .Where(node => node != null)
                .ToHashSet();

            var candidates = destinationNodes
                .Where(node => node != null)
                .Distinct()
                .Where(node => !occupiedGoals.Contains(node))
                .Where(node => !occupiedNodes.Contains(node))
                .ToList();

            if (avoidCurrentGoal && candidates.Count > 1)
                candidates.Remove(agent.GoalNode);

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"MAPF random goal skipped: no unassigned destination is available for agent={agent.AgentId}.", agent);
                return null;
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        private bool HasReachedGoal(MapfAgent agent)
        {
            if (agent.GoalNode == null)
                return false;

            var distance = Vector2.Distance(agent.transform.position, agent.GoalNode.transform.position);
            return distance <= arrivalDistance;
        }

        private MapfNode NearestNode(Vector3 position)
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

        private MapfNode CurrentOccupiedNode(MapfAgent agent)
        {
            if (agent == null)
                return null;

            var nearest = NearestNode(agent.transform.position);
            if (nearest == null)
                return null;

            var distance = Vector2.Distance(agent.transform.position, nearest.transform.position);
            return distance <= arrivalDistance ? nearest : null;
        }

        private void RefreshAgents()
        {
            if (agents.Count == 0)
                agents.AddRange(FindObjectsByType<MapfAgent>(FindObjectsInactive.Exclude).OrderBy(agent => agent.AgentId));

            foreach (var agent in agents)
            {
                if (agent != null && !_states.ContainsKey(agent))
                    _states.Add(agent, NewState(agent));
            }

            foreach (var agent in _states.Keys.Where(agent => agent == null || !agents.Contains(agent)).ToArray())
                _states.Remove(agent);
        }

        private void ValidateConfiguration()
        {
            if (coordinator == null)
                Debug.LogError("MapfRandomGoalAssigner needs a MapfCoordinator in the scene.", this);

            if (destinationNodes.Count == 0)
                Debug.LogError("MapfRandomGoalAssigner needs at least one destination node.", this);

            if (agents.Count == 0)
                Debug.LogWarning("MapfRandomGoalAssigner found no agents to control.", this);
        }

        private static string NodeId(MapfNode node)
        {
            return node != null ? node.StableId : "<unknown>";
        }

        private static AgentRuntimeState NewState(MapfAgent agent)
        {
            return new AgentRuntimeState
            {
                AssignedGoal = agent.GoalNode
            };
        }

        [Serializable]
        private struct AgentRuntimeState
        {
            public float? ArrivalTime;
            public MapfNode AssignedGoal;
        }
    }
}
