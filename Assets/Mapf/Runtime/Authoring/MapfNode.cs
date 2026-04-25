using UnityEngine;

namespace Mapf.Authoring
{
    [DisallowMultipleComponent]
    public sealed class MapfNode : MonoBehaviour
    {
        [SerializeField] private string stableId;

        public string StableId => string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();

        public void Configure(string id)
        {
            stableId = id;
        }
    }
}
