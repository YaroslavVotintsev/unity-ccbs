using System.Collections.Generic;
using System.Linq;
using Mapf.Authoring;
using Mapf.Core.Graph;
using Mapf.Core.Planning;
using UnityEngine;

namespace Mapf.UnityAdapter
{
    /// <summary>
    /// Helper that instantiates predefined MAPF scenarios into the current Unity scene.
    /// </summary>
    public sealed class MapfScenarioSpawner : MonoBehaviour
    {
        [SerializeField] private MapfScenarioPreset preset = MapfScenarioPreset.BasicSidestepSwap;
        [SerializeField] private bool spawnOnStart;
        [SerializeField] private bool clearChildrenBeforeSpawn = true;
        [SerializeField] private Sprite agentSprite;
        [SerializeField] private Color[] agentColors = { Color.cyan, Color.yellow, Color.magenta, Color.green };

        private void Start()
        {
            if (spawnOnStart)
                Spawn();
        }

        [ContextMenu("Spawn MAPF Scenario")]
        /// <summary>
        /// Spawns the selected scenario, including nodes, edges, agents, and support components.
        /// </summary>
        public void Spawn()
        {
            if (clearChildrenBeforeSpawn)
                ClearChildren();

            var scenario = GetScenario(preset);
            var nodeObjects = new Dictionary<int, MapfNode>();

            var nodeRoot = new GameObject("Nodes");
            nodeRoot.transform.SetParent(transform, false);
            foreach (var node in scenario.Graph.Nodes)
            {
                var go = new GameObject($"{node.Id}_{node.Name}");
                go.transform.SetParent(nodeRoot.transform, false);
                go.transform.position = new Vector3((float)node.Position.X, (float)node.Position.Y, 0);
                var mapfNode = go.AddComponent<MapfNode>();
                mapfNode.Configure(node.Name);
                nodeObjects[node.Id] = mapfNode;
            }

            var edgeRoot = new GameObject("Edges");
            edgeRoot.transform.SetParent(transform, false);
            var uniqueEdges = new HashSet<(int, int)>();
            foreach (var edge in scenario.Graph.Edges)
            {
                var a = Mathf.Min(edge.From, edge.To);
                var b = Mathf.Max(edge.From, edge.To);
                if (!uniqueEdges.Add((a, b)))
                    continue;

                var go = new GameObject($"{a}_{b}");
                go.transform.SetParent(edgeRoot.transform, false);
                var mapfEdge = go.AddComponent<MapfEdge>();
                mapfEdge.Configure(nodeObjects[a], nodeObjects[b]);
            }

            var agentRoot = new GameObject("Agents");
            agentRoot.transform.SetParent(transform, false);
            foreach (var agent in scenario.Agents)
            {
                var go = new GameObject($"Agent_{agent.AgentId}");
                go.transform.SetParent(agentRoot.transform, false);
                var start = scenario.Graph.GetNode(agent.StartNodeId).Position;
                go.transform.position = new Vector3((float)start.X, (float)start.Y, -0.1f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = agentSprite;
                renderer.color = agentColors.Length == 0 ? Color.white : agentColors[agent.AgentId % agentColors.Length];

                var mapfAgent = go.AddComponent<MapfAgent>();
                mapfAgent.Configure(agent.AgentId, nodeObjects[agent.StartNodeId], nodeObjects[agent.GoalNodeId]);
                go.AddComponent<MapfAgentController>();
            }

            if (GetComponent<MapfSceneGraph>() == null)
                gameObject.AddComponent<MapfSceneGraph>();
            if (GetComponent<MapfCoordinator>() == null)
                gameObject.AddComponent<MapfCoordinator>();
            if (GetComponent<MapfDebugGizmos>() == null)
                gameObject.AddComponent<MapfDebugGizmos>();
        }

        private static MapfScenario GetScenario(MapfScenarioPreset selected)
        {
            return selected switch
            {
                MapfScenarioPreset.BasicStraightLineSingleAgent => MapfScenarioLibrary.StraightLineSingleAgent(),
                MapfScenarioPreset.BasicCrossIntersection => MapfScenarioLibrary.CrossIntersection(),
                MapfScenarioPreset.BasicSidestepSwap => MapfScenarioLibrary.SidestepSwap(),
                MapfScenarioPreset.BasicPassingLoop => MapfScenarioLibrary.PassingLoop(),
                MapfScenarioPreset.BasicWaitBayMerge => MapfScenarioLibrary.WaitBayMerge(),
                MapfScenarioPreset.BasicThreeAgentCorridorWithTwoBays => MapfScenarioLibrary.ThreeAgentCorridorWithTwoBays(),
                MapfScenarioPreset.BasicLoggedElevenNodeThreeAgent => MapfScenarioLibrary.LoggedElevenNodeThreeAgent(),
                MapfScenarioPreset.ThreeAgentsElevenNodeOppositeEnds => MapfScenarioLibrary.ThreeAgentsElevenNodeOppositeEnds(),
                MapfScenarioPreset.FourAgentsTwelveNodeOppositeEnds => MapfScenarioLibrary.FourAgentsTwelveNodeOppositeEnds(),
                MapfScenarioPreset.FiveAgentsThirteenNodeOppositeEnds => MapfScenarioLibrary.FiveAgentsThirteenNodeOppositeEnds(),
                MapfScenarioPreset.FiveAgentsLongSideBayCorridor => MapfScenarioLibrary.FiveAgentsLongSideBayCorridor(),
                _ => MapfScenarioLibrary.SidestepSwap()
            };
        }

        private void ClearChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

    }
}
