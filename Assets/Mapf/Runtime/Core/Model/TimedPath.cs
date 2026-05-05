using System;
using System.Collections.Generic;
using System.Linq;

namespace Mapf.Core.Model
{
    /// <summary>
    /// Absolute-time route for one agent.
    /// Consecutive points define waits or straight-line edge traversals.
    /// </summary>
    public sealed class TimedPath
    {
        public int AgentId { get; }
        public IReadOnlyList<TimedPathPoint> Points { get; }
        public bool ReservesGoalAfterArrival { get; }
        public double Cost => Points.Count == 0 ? -1 : Points[Points.Count - 1].Time;
        public bool IsEmpty => Points.Count == 0;

        /// <summary>
        /// Creates a timed path from ordered absolute-time points.
        /// </summary>
        public TimedPath(int agentId, IEnumerable<TimedPathPoint> points, bool reservesGoalAfterArrival = true)
        {
            AgentId = agentId;
            Points = points?.ToArray() ?? Array.Empty<TimedPathPoint>();
            ReservesGoalAfterArrival = reservesGoalAfterArrival;
        }

        public TimedPathPoint Last => Points[Points.Count - 1];
    }
}
