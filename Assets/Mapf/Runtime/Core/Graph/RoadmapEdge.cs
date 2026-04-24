namespace Mapf.Core.Graph
{
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
