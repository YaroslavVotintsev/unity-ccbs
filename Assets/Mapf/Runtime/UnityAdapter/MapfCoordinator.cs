using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using Mapf.Authoring;
using Mapf.Core.Model;
using Mapf.Core.Planning;
using UnityEngine;

namespace Mapf.UnityAdapter
{
    public sealed class MapfCoordinator : MonoBehaviour
    {
        [SerializeField] private MapfSceneGraph sceneGraph;
        [SerializeField] private MapfPlannerSettingsAsset settings;
        [SerializeField] private bool planOnStart = true;

        private readonly MapfPlannerService _planner = new();
        private readonly List<TimedPath> _latestPlans = new();
        private CancellationTokenSource _planningCts;
        private Dictionary<MapfNode, int> _nodeIds = new();

        private async void Start()
        {
            if (planOnStart)
                await RequestPlanAsync(null);
        }

        public async void RequestAgentGoal(MapfAgent agent, MapfNode goal)
        {
            agent.SetGoal(goal);
            await RequestPlanAsync(agent.AgentId);
        }

        public async System.Threading.Tasks.Task RequestPlanAsync(int? affectedAgentId)
        {
            _planningCts?.Cancel();
            _planningCts = new CancellationTokenSource();

            sceneGraph ??= FindAnyObjectByType<MapfSceneGraph>();
            if (sceneGraph == null)
            {
                Debug.LogError("No MapfSceneGraph found.");
                return;
            }

            var graph = sceneGraph.BuildSnapshot(out _nodeIds);
            var agents = FindObjectsByType<MapfAgent>().OrderBy(a => a.AgentId).ToArray();
            ValidateUniqueAgentIds(agents);
            var now = Time.timeAsDouble;
            var states = new List<AgentState>();
            foreach (var agent in agents)
            {
                if (!_nodeIds.TryGetValue(agent.StartNode, out var start) || !_nodeIds.TryGetValue(agent.GoalNode, out var goal))
                {
                    Debug.LogWarning($"MAPF agent '{agent.name}' has missing start or goal.", agent);
                    continue;
                }

                var controller = agent.GetComponent<MapfAgentController>();
                states.Add(controller.GetPlanningState(agent.AgentId, start, goal, now));
            }

            var plannerSettings = settings != null ? settings.ToSettings() : new MapfPlannerSettings();
            var request = new MapfPlanningRequest(graph, states, plannerSettings, _latestPlans.ToArray(), affectedAgentId);
            var result = await _planner.PlanAsync(request, _planningCts.Token);
            if (!result.Success)
            {
                Debug.LogWarning($"MAPF planning failed: {result.Status} {result.Message}");
                return;
            }

            _latestPlans.Clear();
            _latestPlans.AddRange(result.Paths);
            ApplyPlans(agents, result.Paths);
        }

        private static void ValidateUniqueAgentIds(IEnumerable<MapfAgent> agents)
        {
            var duplicates = agents
                .GroupBy(agent => agent.AgentId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicates.Length == 0)
                return;

            var message = $"Duplicate MAPF agent id(s): {string.Join(", ", duplicates)}. Every MapfAgent Agent Id must be unique.";
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        private static void ApplyPlans(IReadOnlyList<MapfAgent> agents, IReadOnlyList<TimedPath> paths)
        {
            foreach (var path in paths)
            {
                var agent = agents.FirstOrDefault(a => a.AgentId == path.AgentId);
                if (agent == null)
                    continue;

                agent.GetComponent<MapfAgentController>().ApplyPlan(path);
            }
        }
    }
}
