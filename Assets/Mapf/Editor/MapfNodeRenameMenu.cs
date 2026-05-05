using System.Linq;
using Mapf.Authoring;
using UnityEditor;

namespace Mapf.Editor
{
    /// <summary>
    /// Editor menu commands for renaming selected node GameObjects to their stable ids.
    /// </summary>
    public static class MapfNodeRenameMenu
    {
        private const string MenuPath = "Tools/MAPF/Rename Selected Nodes To Id";

        /// <summary>
        /// Renames every selected <see cref="MapfNode"/> GameObject to its Stable Id.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void RenameSelectedNodesToId()
        {
            var nodes = Selection.gameObjects
                .Select(go => go.GetComponent<MapfNode>())
                .Where(node => node != null)
                .Distinct()
                .ToArray();

            if (nodes.Length == 0)
            {
                EditorUtility.DisplayDialog("Rename MAPF Nodes", "Select one or more GameObjects with MapfNode components.", "OK");
                return;
            }

            var missingIds = nodes
                .Where(node => string.IsNullOrWhiteSpace(node.StableId))
                .Select(node => node.name)
                .ToArray();

            if (missingIds.Length > 0)
            {
                EditorUtility.DisplayDialog("Rename MAPF Nodes", $"Cannot rename nodes with empty Stable Id: {string.Join(", ", missingIds)}.", "OK");
                return;
            }

            foreach (var node in nodes)
            {
                Undo.RecordObject(node.gameObject, "Rename MAPF Node To Id");
                node.gameObject.name = node.StableId;
                EditorUtility.SetDirty(node.gameObject);
            }
        }

        /// <summary>
        /// Enables the rename command when at least one selected GameObject has a <see cref="MapfNode"/>.
        /// </summary>
        [MenuItem(MenuPath, true)]
        public static bool ValidateRenameSelectedNodesToId()
        {
            return Selection.gameObjects.Any(go => go.GetComponent<MapfNode>() != null);
        }
    }
}
