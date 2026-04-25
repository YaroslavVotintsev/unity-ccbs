using System;
using System.Collections.Generic;
using System.Linq;
using Mapf.Core.Graph;
using Mapf.Core.Model;
using UnityEngine;

namespace Mapf.Authoring
{
    public sealed class MapfSceneGraph : MonoBehaviour
    {
        public RoadmapGraph BuildSnapshot(out Dictionary<MapfNode, int> nodeIds)
        {
            var nodes = FindObjectsByType<MapfNode>()
                .OrderBy(n => n.StableId, StringComparer.Ordinal)
                .ToArray();
            var edges = FindObjectsByType<MapfEdge>();

            ValidateRequiredNodeIds(nodes);
            ValidateUniqueNodeIds(nodes);

            nodeIds = new Dictionary<MapfNode, int>();
            var graphNodes = new List<RoadmapNode>();
            for (var i = 0; i < nodes.Length; i++)
            {
                nodeIds[nodes[i]] = i;
                var p = nodes[i].transform.position;
                graphNodes.Add(new RoadmapNode(i, nodes[i].StableId, new MapfVector2(p.x, p.y)));
            }

            var graphEdges = new List<(int A, int B)>();
            var seenEdges = new HashSet<(int A, int B)>();
            foreach (var edge in edges)
            {
                if (edge.A == null || edge.B == null)
                {
                    Debug.LogWarning($"MAPF edge '{edge.name}' is missing an endpoint.", edge);
                    continue;
                }

                if (edge.A == edge.B)
                {
                    Fail($"MAPF edge '{edge.name}' connects node '{edge.A.StableId}' to itself.", edge);
                }

                if (!nodeIds.TryGetValue(edge.A, out var a) || !nodeIds.TryGetValue(edge.B, out var b))
                    continue;

                if (edge.RequireAxisAligned && !IsAxisAligned(edge.A.transform.position, edge.B.transform.position, edge.AxisTolerance))
                {
                    Debug.LogWarning($"MAPF edge '{edge.name}' is not axis-aligned.", edge);
                    continue;
                }

                var edgeKey = a < b ? (a, b) : (b, a);
                if (!seenEdges.Add(edgeKey))
                {
                    Fail($"Duplicate MAPF edge between nodes '{edge.A.StableId}' and '{edge.B.StableId}'. Only one undirected edge is allowed between the same nodes.", edge);
                }

                graphEdges.Add((a, b));
            }

            return new RoadmapGraph(graphNodes, graphEdges);
        }

        private static void ValidateUniqueNodeIds(IEnumerable<MapfNode> nodes)
        {
            var duplicates = nodes
                .GroupBy(node => node.StableId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicates.Length == 0)
                return;

            Fail($"Duplicate MAPF node id(s): {string.Join(", ", duplicates)}. Every MapfNode Stable Id must be unique.");
        }

        private static void ValidateRequiredNodeIds(IEnumerable<MapfNode> nodes)
        {
            var missing = nodes
                .Where(node => string.IsNullOrWhiteSpace(node.StableId))
                .Select(node => node.name)
                .ToArray();

            if (missing.Length == 0)
                return;

            Fail($"MAPF node id is required. These nodes have an empty Stable Id: {string.Join(", ", missing)}.");
        }

        private static bool IsAxisAligned(Vector3 a, Vector3 b, float tolerance)
        {
            return Mathf.Abs(a.x - b.x) <= tolerance || Mathf.Abs(a.y - b.y) <= tolerance;
        }

        private static void Fail(string message, UnityEngine.Object context = null)
        {
            Debug.LogError(message, context);
            throw new InvalidOperationException(message);
        }
    }
}
