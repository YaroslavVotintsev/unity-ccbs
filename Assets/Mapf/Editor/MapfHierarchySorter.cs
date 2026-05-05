using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Mapf.Editor
{
    /// <summary>
    /// Editor utility for sorting selected transform children by GameObject name.
    /// </summary>
    public static class MapfHierarchySorter
    {
        /// <summary>
        /// Sorts the active transform's direct children alphabetically by name.
        /// </summary>
        [MenuItem("Tools/MAPF/Sort Children By Name")]
        public static void SortByName()
        {
            Transform parent = Selection.activeTransform;

            if (parent == null)
            {
                Debug.LogWarning("Select a parent GameObject first.");
                return;
            }

            // Get children
            var children = parent.Cast<Transform>().ToList();

            // Sort alphabetically
            var sorted = children.OrderBy(c => c.name).ToList();

            // Apply order
            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].SetSiblingIndex(i);
            }

            Debug.Log("Children sorted by name.");
        }
    }
}
