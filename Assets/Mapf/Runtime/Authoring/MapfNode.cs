using Mapf.Core.Graph;
using UnityEngine;

namespace Mapf.Authoring
{
    [DisallowMultipleComponent]
    public sealed class MapfNode : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private RoadmapNodeKind kind = RoadmapNodeKind.Generic;

        public string StableId => string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();
        public RoadmapNodeKind Kind => kind;

        public void Configure(string id, RoadmapNodeKind nodeKind)
        {
            stableId = id;
            kind = nodeKind;
        }
    }
}
