using Mapf.Authoring;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Mapf.UnityAdapter
{
    public sealed class MapfDebugGizmos : MonoBehaviour
    {
        [SerializeField] private float nodeRadius = 0.08f;
        [SerializeField] private float labelOffset = 0.18f;
        [SerializeField] private bool showNodeIds = true;
        [SerializeField] private Color nodeColor = new(0.2f, 0.7f, 1f, 1f);
        [SerializeField] private Color planColor = new(1f, 0.75f, 0.2f, 1f);

        private void OnDrawGizmos()
        {
            Gizmos.color = nodeColor;
            foreach (var node in FindObjectsByType<MapfNode>())
            {
                Gizmos.DrawSphere(node.transform.position, nodeRadius);
#if UNITY_EDITOR
                if (showNodeIds)
                {
                    var labelPosition = node.transform.position + Vector3.up * labelOffset;
                    Handles.Label(labelPosition, node.StableId);
                }
#endif
            }

            Gizmos.color = planColor;
            foreach (var controller in FindObjectsByType<MapfAgentController>())
            {
                var points = controller.CurrentPoints;
                for (var i = 0; i + 1 < points.Count; i++)
                {
                    var a = new Vector3((float)points[i].Position.X, (float)points[i].Position.Y, 0);
                    var b = new Vector3((float)points[i + 1].Position.X, (float)points[i + 1].Position.Y, 0);
                    Gizmos.DrawLine(a, b);
                }
            }
        }
    }
}
