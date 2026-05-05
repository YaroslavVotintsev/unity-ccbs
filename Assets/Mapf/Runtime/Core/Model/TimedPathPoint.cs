namespace Mapf.Core.Model
{
    /// <summary>
    /// A node, position, and absolute time sample in an agent path.
    /// </summary>
    public readonly struct TimedPathPoint
    {
        public readonly int NodeId;
        public readonly MapfVector2 Position;
        public readonly double Time;

        public TimedPathPoint(int nodeId, MapfVector2 position, double time)
        {
            NodeId = nodeId;
            Position = position;
            Time = time;
        }
    }
}
