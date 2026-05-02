using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using Mapf.Authoring;
using Mapf.Core.Graph;
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
        [SerializeField] private bool logSceneSnapshotOnStart = true;

        private readonly MapfPlannerService _planner = new();
        private readonly List<TimedPath> _latestPlans = new();
        private readonly HashSet<int> _dirtyAffectedAgentIds = new();
        private CancellationTokenSource _planningCts;
        private int _planningVersion;
        private bool _queuedReplanScheduled;
        private static bool s_loggedSceneSnapshotThisPlaySession;
        private Dictionary<MapfNode, int> _nodeIds = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneSnapshotLogging()
        {
            s_loggedSceneSnapshotThisPlaySession = false;
        }

        private async void Start()
        {
            if (planOnStart)
                await RequestPlanAsync(null);
        }

        public void RequestAgentGoal(MapfAgent agent, MapfNode goal)
        {
            agent.SetGoal(goal);
            _dirtyAffectedAgentIds.Add(agent.AgentId);

            if (!_queuedReplanScheduled)
                StartCoroutine(RequestQueuedPlanNextFrame());
        }

        private IEnumerator RequestQueuedPlanNextFrame()
        {
            _queuedReplanScheduled = true;
            yield return null;

            var affectedIds = _dirtyAffectedAgentIds.ToArray();
            _queuedReplanScheduled = false;

            var affectedAgentId = affectedIds.Length == 1 ? affectedIds[0] : (int?)null;
            var task = RequestPlanAsync(affectedAgentId);
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
                Debug.LogException(task.Exception);
            else if (task.Result)
                ClearAppliedDirtyAgents(affectedIds, affectedAgentId);

            if (_dirtyAffectedAgentIds.Count > 0 && !_queuedReplanScheduled)
                StartCoroutine(RequestQueuedPlanNextFrame());
        }

        public async System.Threading.Tasks.Task<bool> RequestPlanAsync(int? affectedAgentId)
        {
            _planningCts?.Cancel();
            _planningCts = new CancellationTokenSource();
            var version = ++_planningVersion;
            var cancellationToken = _planningCts.Token;

            sceneGraph ??= FindAnyObjectByType<MapfSceneGraph>();
            if (sceneGraph == null)
            {
                Debug.LogError("No MapfSceneGraph found.");
                return false;
            }

            var graph = sceneGraph.BuildSnapshot(out _nodeIds);
            var agents = FindObjectsByType<MapfAgent>().OrderBy(a => a.AgentId).ToArray();
            ValidateUniqueAgentIds(agents);
            var plannerSettings = settings != null ? settings.ToSettings() : new MapfPlannerSettings();
            if (logSceneSnapshotOnStart && !s_loggedSceneSnapshotThisPlaySession)
            {
                s_loggedSceneSnapshotThisPlaySession = true;
                LogSceneSnapshot(graph, _nodeIds, agents, plannerSettings);
            }

            var now = Time.timeAsDouble;
            var states = new List<AgentState>();
            var existingPlans = new List<TimedPath>();
            var reservations = new List<Reservation>();
            foreach (var agent in agents)
            {
                if (!_nodeIds.TryGetValue(agent.StartNode, out var start) || !_nodeIds.TryGetValue(agent.GoalNode, out var goal))
                {
                    Debug.LogWarning($"MAPF agent '{agent.name}' has missing start or goal.", agent);
                    continue;
                }

                var controller = agent.GetComponent<MapfAgentController>();
                states.Add(controller.GetPlanningState(agent.AgentId, start, goal, now));
                if (controller.HasPlan)
                {
                    existingPlans.Add(controller.GetPlanSnapshot(agent.AgentId));
                    var reservation = controller.GetCommittedReservation(agent.AgentId, now);
                    if (reservation.HasValue)
                        reservations.Add(reservation.Value);
                }
            }

            var request = new MapfPlanningRequest(graph, states, plannerSettings, existingPlans.ToArray(), affectedAgentId, reservations.ToArray());
            MapfPlanningResult result;
            try
            {
                result = await _planner.PlanAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (version != _planningVersion || cancellationToken.IsCancellationRequested)
                return false;

            if (!result.Success)
            {
                if (result.Status == PlannerStatus.Cancelled)
                    return false;

                Debug.LogWarning($"MAPF planning failed: {result.Status} {result.Message}");
                return false;
            }

            _latestPlans.Clear();
            ApplyPlans(agents, result.Paths, Time.timeAsDouble);
            foreach (var agent in agents)
            {
                var controller = agent.GetComponent<MapfAgentController>();
                if (controller.HasPlan)
                    _latestPlans.Add(controller.GetPlanSnapshot(agent.AgentId));
            }

            return true;
        }

        private void ClearAppliedDirtyAgents(IReadOnlyList<int> attemptedAffectedIds, int? affectedAgentId)
        {
            if (affectedAgentId.HasValue)
            {
                _dirtyAffectedAgentIds.Remove(affectedAgentId.Value);
                return;
            }

            foreach (var id in attemptedAffectedIds)
                _dirtyAffectedAgentIds.Remove(id);
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

        private static void LogSceneSnapshot(
            RoadmapGraph graph,
            IReadOnlyDictionary<MapfNode, int> nodeIds,
            IReadOnlyList<MapfAgent> agents,
            MapfPlannerSettings plannerSettings)
        {
            var stableIdsByNode = nodeIds.ToDictionary(pair => pair.Value, pair => pair.Key.StableId);
            var builder = new StringBuilder();
            builder.AppendLine("MAPF_SCENE_SNAPSHOT_BEGIN");
            builder.AppendLine("Nodes:");
            foreach (var node in graph.Nodes)
                builder.AppendLine($"  {node.Id}: id=\"{Escape(node.Name)}\" pos=({FormatDecimal(node.Position.X)},{FormatDecimal(node.Position.Y)})");

            builder.AppendLine("Edges:");
            foreach (var edge in graph.Edges.Where(edge => edge.From < edge.To))
                builder.AppendLine($"  \"{Escape(stableIdsByNode[edge.From])}\" -- \"{Escape(stableIdsByNode[edge.To])}\" length={FormatDecimal(edge.Length)}");

            builder.AppendLine($"Agents: {agents.Count}");
            foreach (var agent in agents)
            {
                var start = agent.StartNode != null ? agent.StartNode.StableId : "<missing>";
                var goal = agent.GoalNode != null ? agent.GoalNode.StableId : "<missing>";
                builder.AppendLine($"  agent={agent.AgentId} start=\"{Escape(start)}\" goal=\"{Escape(goal)}\"");
            }

            builder.AppendLine("Settings:");
            builder.AppendLine($"  radius={plannerSettings.AgentRadius:0.###} speed={plannerSettings.AgentSpeed:0.###} timeLimit={plannerSettings.TimeLimitSeconds:0.###}");
            builder.AppendLine($"  maxHighLevelNodes={plannerSettings.MaxHighLevelNodes} maxLowLevelNodes={plannerSettings.MaxLowLevelNodes} maxLocalRepairIterations={plannerSettings.MaxLocalRepairIterations}");
            builder.AppendLine($"  replanStrategy={plannerSettings.ReplanStrategy}");
            builder.AppendLine("MAPF_SCENE_SNAPSHOT_END");
            Debug.Log(builder.ToString());
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string FormatDecimal(double value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void ApplyPlans(IReadOnlyList<MapfAgent> agents, IReadOnlyList<TimedPath> paths, double now)
        {
            foreach (var path in paths)
            {
                var agent = agents.FirstOrDefault(a => a.AgentId == path.AgentId);
                if (agent == null)
                    continue;

                agent.GetComponent<MapfAgentController>().ApplyPlanPreservingCommittedPrefix(path, now);
            }
        }
    }
}
