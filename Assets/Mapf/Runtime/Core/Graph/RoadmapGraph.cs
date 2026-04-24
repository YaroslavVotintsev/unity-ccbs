using System;
using System.Collections.Generic;
using System.Linq;
using Mapf.Core.Model;

namespace Mapf.Core.Graph
{
    public sealed class RoadmapGraph
    {
        private readonly RoadmapNode[] _nodes;
        private readonly RoadmapEdge[] _edges;
        private readonly RoadmapEdge[][] _adjacency;

        public IReadOnlyList<RoadmapNode> Nodes => _nodes;
        public IReadOnlyList<RoadmapEdge> Edges => _edges;
        public int Count => _nodes.Length;

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

        public RoadmapNode GetNode(int id)
        {
            ValidateNodeId(id);
            return _nodes[id];
        }

        public IReadOnlyList<RoadmapEdge> GetNeighbors(int id)
        {
            ValidateNodeId(id);
            return _adjacency[id];
        }

        public double TravelTime(int from, int to, double speed)
        {
            if (speed <= 0)
                throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be positive.");

            foreach (var edge in GetNeighbors(from))
                if (edge.To == to)
                    return edge.Length / speed;

            throw new InvalidOperationException($"No edge exists from node {from} to node {to}.");
        }

        public void ValidateNodeId(int id)
        {
            if (id < 0 || id >= _nodes.Length)
                throw new ArgumentOutOfRangeException(nameof(id), $"Node id {id} is outside graph bounds.");
        }
    }
}
