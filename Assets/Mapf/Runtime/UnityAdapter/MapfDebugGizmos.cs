using Mapf.Authoring;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Mapf.UnityAdapter
{
    /// <summary>
    /// Centralized Scene view debug drawing for roadmap nodes, roadmap edges, and agent labels.
    /// </summary>
    public sealed class MapfDebugGizmos : MonoBehaviour
    {
        [SerializeField] private float nodeRadius = 0.08f;
        [SerializeField] private Vector2 nodeLabelOffset = new(0f, 0.18f);
        [SerializeField] private Vector2 agentLabelOffset = new(0f, 0.18f);
        [SerializeField] private bool showNodeIds = true;
        [SerializeField] private bool showAgentIds = true;
        [SerializeField] private Color nodeColor = new(0.2f, 0.7f, 1f, 1f);
        [SerializeField] private Color edgeColor = Color.gray;

        private void OnDrawGizmos()
        {
            Gizmos.color = nodeColor;
            foreach (var node in FindObjectsByType<MapfNode>())
            {
                Gizmos.DrawSphere(node.transform.position, nodeRadius);
#if UNITY_EDITOR
                if (showNodeIds)
                {
                    var labelPosition = node.transform.position + ToVector3(nodeLabelOffset);
                    Handles.Label(labelPosition, node.StableId);
                }
#endif
            }

            Gizmos.color = edgeColor;
            foreach (var edge in FindObjectsByType<MapfEdge>())
            {
                if (edge.A == null || edge.B == null)
                    continue;

                Gizmos.DrawLine(edge.A.transform.position, edge.B.transform.position);
            }

            foreach (var controller in FindObjectsByType<MapfAgentController>())
            {
#if UNITY_EDITOR
                if (showAgentIds && controller.TryGetComponent<MapfAgent>(out var agent))
                {
                    var labelPosition = controller.transform.position + ToVector3(agentLabelOffset);
                    Handles.Label(labelPosition, $"ID:{agent.AgentId}\nGoal:{NodeId(agent.GoalNode)}");
                }
#endif
            }
        }

        private static Vector3 ToVector3(Vector2 offset)
        {
            return new Vector3(offset.x, offset.y, 0);
        }

        private static string NodeId(MapfNode node)
        {
            return node != null ? node.StableId : "<none>";
        }
    }
}
