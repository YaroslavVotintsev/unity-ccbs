namespace Mapf.Core.Graph
{
    /// <summary>
    /// Directed roadmap edge with precomputed geometric length.
    /// Undirected scene edges are represented by two directed roadmap edges.
    /// </summary>
    public readonly struct RoadmapEdge
    {
        public readonly int From;
        public readonly int To;
        public readonly double Length;

        public RoadmapEdge(int from, int to, double length)
        {
            From = from;
            To = to;
            Length = length;
        }
    }
}
