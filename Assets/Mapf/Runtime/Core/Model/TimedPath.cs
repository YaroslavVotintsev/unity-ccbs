using System;
using System.Collections.Generic;
using System.Linq;

namespace Mapf.Core.Model
{
    public sealed class TimedPath
    {
        public int AgentId { get; }
        public IReadOnlyList<TimedPathPoint> Points { get; }
        public bool ReservesGoalAfterArrival { get; }
        public double Cost => Points.Count == 0 ? -1 : Points[Points.Count - 1].Time;
        public bool IsEmpty => Points.Count == 0;

        public TimedPath(int agentId, IEnumerable<TimedPathPoint> points, bool reservesGoalAfterArrival = true)
        {
            AgentId = agentId;
            Points = points?.ToArray() ?? Array.Empty<TimedPathPoint>();
            ReservesGoalAfterArrival = reservesGoalAfterArrival;
        }

        public TimedPathPoint Last => Points[Points.Count - 1];
    }
}
