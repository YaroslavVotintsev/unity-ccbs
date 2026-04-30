using UnityEngine;

namespace Mapf.Authoring
{
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

        public void Configure(MapfNode nodeA, MapfNode nodeB)
        {
            a = nodeA;
            b = nodeB;
        }

        private void OnDrawGizmos()
        {
            if (a == null || b == null)
                return;

            Gizmos.color = Color.gray;
            Gizmos.DrawLine(a.transform.position, b.transform.position);
        }
    }
}
