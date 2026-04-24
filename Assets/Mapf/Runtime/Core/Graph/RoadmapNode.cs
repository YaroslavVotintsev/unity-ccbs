using Mapf.Core.Model;

namespace Mapf.Core.Graph
{
    public readonly struct RoadmapNode
    {
        public readonly int Id;
        public readonly string Name;
        public readonly MapfVector2 Position;
        public readonly RoadmapNodeKind Kind;

        public RoadmapNode(int id, string name, MapfVector2 position, RoadmapNodeKind kind = RoadmapNodeKind.Generic)
        {
            Id = id;
            Name = name ?? string.Empty;
            Position = position;
            Kind = kind;
        }
    }
}
