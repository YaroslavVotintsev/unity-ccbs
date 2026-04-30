using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Mapf.Editor
{
    public static class MapfHierarchySorter
    {
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