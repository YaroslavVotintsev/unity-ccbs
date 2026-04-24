using System.Linq;
using Mapf.Authoring;
using UnityEditor;
using UnityEngine;

namespace Mapf.Editor
{
    public static class MapfEdgeCreationMenu
    {
        private const string MenuPath = "Tools/MAPF/Create Edge Between Selected Nodes";

        [MenuItem(MenuPath)]
        public static void CreateEdgeBetweenSelectedNodes()
        {
            var nodes = GetSelectedNodes();

            if (nodes.Length != 2)
            {
                EditorUtility.DisplayDialog("Create MAPF Edge", "Select exactly two GameObjects with MapfNode components.", "OK");
                return;
            }

            if (HasMissingId(nodes, out var missingMessage))
            {
                EditorUtility.DisplayDialog("Create MAPF Edge", missingMessage, "OK");
                return;
            }

            if (EdgeExists(nodes[0], nodes[1]))
            {
                EditorUtility.DisplayDialog("Create MAPF Edge", $"An edge already exists between '{nodes[0].StableId}' and '{nodes[1].StableId}'.", "OK");
                return;
            }

            var edgeRoot = FindOrCreateEdgeRoot(nodes);
            var edgeObject = new GameObject($"Edge_{nodes[0].StableId}_{nodes[1].StableId}");
            Undo.RegisterCreatedObjectUndo(edgeObject, "Create MAPF Edge");
            edgeObject.transform.SetParent(edgeRoot, false);

            var edge = edgeObject.AddComponent<MapfEdge>();
            edge.Configure(nodes[0], nodes[1]);
            Selection.activeGameObject = edgeObject;
            EditorUtility.SetDirty(edgeObject);
        }

        [MenuItem(MenuPath, true)]
        public static bool ValidateCreateEdgeBetweenSelectedNodes()
        {
            return GetSelectedNodes().Length == 2;
        }

        private static MapfNode[] GetSelectedNodes()
        {
            var nodes = new System.Collections.Generic.List<MapfNode>();
            foreach (var gameObject in Selection.gameObjects)
            {
                var node = gameObject.GetComponent<MapfNode>();
                if (node != null && !nodes.Contains(node))
                    nodes.Add(node);
            }

            return nodes.ToArray();
        }

        private static bool HasMissingId(MapfNode[] nodes, out string message)
        {
            var missing = nodes
                .Where(node => string.IsNullOrWhiteSpace(node.StableId))
                .Select(node => node.name)
                .ToArray();

            if (missing.Length == 0)
            {
                message = string.Empty;
                return false;
            }

            message = $"Every selected MapfNode must have a Stable Id. Missing id on: {string.Join(", ", missing)}.";
            return true;
        }

        private static bool EdgeExists(MapfNode a, MapfNode b)
        {
            foreach (var edge in Object.FindObjectsByType<MapfEdge>())
            {
                if (edge.A == null || edge.B == null)
                    continue;

                var sameDirection = edge.A == a && edge.B == b;
                var reverseDirection = edge.A == b && edge.B == a;
                if (sameDirection || reverseDirection)
                    return true;
            }

            return false;
        }

        private static Transform FindOrCreateEdgeRoot(MapfNode[] nodes)
        {
            var commonParent = nodes[0].transform.parent == nodes[1].transform.parent
                ? nodes[0].transform.parent
                : null;

            Transform searchRoot = commonParent;

            while (searchRoot != null)
            {
                var edgeRoot = searchRoot
                    .GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "Edges");

                if (edgeRoot != null)
                    return edgeRoot;

                searchRoot = searchRoot.parent;
            }

            var rootObject = new GameObject("Edges");
            Undo.RegisterCreatedObjectUndo(rootObject, "Create MAPF Edge Root");

            if (commonParent != null)
                rootObject.transform.SetParent(commonParent, false);

            return rootObject.transform;
        }
    }
}
