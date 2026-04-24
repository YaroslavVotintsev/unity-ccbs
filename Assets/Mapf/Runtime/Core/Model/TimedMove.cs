using System;

namespace Mapf.Core.Model
{
    public readonly struct TimedMove
    {
        public readonly int AgentId;
        public readonly int FromNodeId;
        public readonly int ToNodeId;
        public readonly MapfVector2 From;
        public readonly MapfVector2 To;
        public readonly double StartTime;
        public readonly double EndTime;

        public TimedMove(int agentId, int fromNodeId, int toNodeId, MapfVector2 from, MapfVector2 to, double startTime, double endTime)
        {
            AgentId = agentId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            From = from;
            To = to;
            StartTime = startTime;
            EndTime = endTime;
        }

        public bool IsWait => FromNodeId == ToNodeId;
        public double Duration => EndTime - StartTime;

        public MapfVector2 PositionAt(double time)
        {
            if (IsWait || double.IsPositiveInfinity(EndTime) || Math.Abs(Duration) < 1e-9)
                return From;

            var u = (time - StartTime) / Duration;
            if (u < 0) u = 0;
            if (u > 1) u = 1;
            return MapfVector2.Lerp(From, To, u);
        }
    }
}
