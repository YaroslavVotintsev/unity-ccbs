using Mapf.Core.Model;

namespace Mapf.Core.Graph
{
    /// <summary>
    /// Immutable roadmap node used by the pure planner.
    /// </summary>
    public readonly struct RoadmapNode
    {
        public readonly int Id;
        public readonly string Name;
        public readonly MapfVector2 Position;

        public RoadmapNode(int id, string name, MapfVector2 position)
        {
            Id = id;
            Name = name ?? string.Empty;
            Position = position;
        }
    }
}
