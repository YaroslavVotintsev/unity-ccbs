namespace Mapf.Core.Model
{
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
