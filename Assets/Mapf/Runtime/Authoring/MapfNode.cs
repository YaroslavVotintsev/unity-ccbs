using UnityEngine;

namespace Mapf.Authoring
{
    /// <summary>
    /// Scene-authored roadmap node.
    /// Nodes are converted to immutable graph nodes before planning and are identified by a stable string id.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapfNode : MonoBehaviour
    {
        [SerializeField] private string stableId;

        public string StableId => string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();

        /// <summary>
        /// Assigns the stable id for a node created by tools or scenario spawning.
        /// </summary>
        public void Configure(string id)
        {
            stableId = id;
        }
    }
}
