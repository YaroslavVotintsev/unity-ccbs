using UnityEngine;

namespace Mapf.Authoring
{
    /// <summary>
    /// Scene-authored undirected roadmap edge between two <see cref="MapfNode"/> objects.
    /// The scene graph adapter converts these components into bidirectional graph edges.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapfEdge : MonoBehaviour
    {
        [SerializeField] private MapfNode a;
        [SerializeField] private MapfNode b;
        [SerializeField] private bool requireAxisAligned = false;
        [SerializeField] private float axisTolerance = 0.01f;

        public MapfNode A => a;
        public MapfNode B => b;
        public bool RequireAxisAligned => requireAxisAligned;
        public float AxisTolerance => axisTolerance;

        /// <summary>
        /// Assigns the endpoints for an edge created by editor tools or scenario spawning.
        /// </summary>
        public void Configure(MapfNode nodeA, MapfNode nodeB)
        {
            a = nodeA;
            b = nodeB;
        }
    }
}
