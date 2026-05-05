using System;
using System.Collections.Generic;
using System.Linq;
using Mapf.Core.Model;

namespace Mapf.Core.Graph
{
    /// <summary>
    /// Immutable bidirectional roadmap graph used by the MAPF planner.
    /// Node ids must be contiguous and zero-based.
    /// </summary>
    public sealed class RoadmapGraph
    {
        private readonly RoadmapNode[] _nodes;
        private readonly RoadmapEdge[] _edges;
        private readonly RoadmapEdge[][] _adjacency;

        public IReadOnlyList<RoadmapNode> Nodes => _nodes;
        public IReadOnlyList<RoadmapEdge> Edges => _edges;
        public int Count => _nodes.Length;

        /// <summary>
        /// Creates a graph from nodes and undirected edge pairs. Each pair is expanded into two directed edges.
        /// </summary>
        public RoadmapGraph(IEnumerable<RoadmapNode> nodes, IEnumerable<(int A, int B)> undirectedEdges)
        {
            _nodes = nodes.OrderBy(n => n.Id).ToArray();
            if (_nodes.Length == 0)
                throw new ArgumentException("Roadmap graph must contain at least one node.", nameof(nodes));

            for (var i = 0; i < _nodes.Length; i++)
                if (_nodes[i].Id != i)
                    throw new ArgumentException("Roadmap node ids must be contiguous and zero-based.");

            var directed = new Dictionary<(int, int), RoadmapEdge>();
            foreach (var (a, b) in undirectedEdges)
            {
                ValidateNodeId(a);
                ValidateNodeId(b);
                if (a == b)
                    continue;

                AddDirected(a, b);
                AddDirected(b, a);
            }

            _edges = directed.Values.OrderBy(e => e.From).ThenBy(e => e.To).ToArray();
            _adjacency = new RoadmapEdge[_nodes.Length][];
            for (var i = 0; i < _nodes.Length; i++)
                _adjacency[i] = _edges.Where(e => e.From == i).ToArray();

            void AddDirected(int from, int to)
            {
                var length = MapfVector2.Distance(_nodes[from].Position, _nodes[to].Position);
                if (length <= 1e-9)
                    return;
                directed[(from, to)] = new RoadmapEdge(from, to, length);
            }
        }

        /// <summary>
        /// Returns a node by its zero-based graph id.
        /// </summary>
        public RoadmapNode GetNode(int id)
        {
            ValidateNodeId(id);
            return _nodes[id];
        }

        /// <summary>
        /// Returns all directed outgoing edges from a node.
        /// </summary>
        public IReadOnlyList<RoadmapEdge> GetNeighbors(int id)
        {
            ValidateNodeId(id);
            return _adjacency[id];
        }

        /// <summary>
        /// Computes traversal time for an existing directed edge at the supplied constant speed.
        /// </summary>
        public double TravelTime(int from, int to, double speed)
        {
            if (speed <= 0)
                throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be positive.");

            foreach (var edge in GetNeighbors(from))
                if (edge.To == to)
                    return edge.Length / speed;

            throw new InvalidOperationException($"No edge exists from node {from} to node {to}.");
        }

        /// <summary>
        /// Throws if the supplied id is outside the graph node range.
        /// </summary>
        public void ValidateNodeId(int id)
        {
            if (id < 0 || id >= _nodes.Length)
                throw new ArgumentOutOfRangeException(nameof(id), $"Node id {id} is outside graph bounds.");
        }
    }
}
