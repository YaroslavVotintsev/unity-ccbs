using System;
using System.Collections.Generic;
using System.Linq;

namespace Mapf.Core.Model
{
    public sealed class TimedPath
    {
        public int AgentId { get; }
        public IReadOnlyList<TimedPathPoint> Points { get; }
        public double Cost => Points.Count == 0 ? -1 : Points[Points.Count - 1].Time;
        public bool IsEmpty => Points.Count == 0;

        public TimedPath(int agentId, IEnumerable<TimedPathPoint> points)
        {
            AgentId = agentId;
            Points = points?.ToArray() ?? Array.Empty<TimedPathPoint>();
        }

        public TimedPathPoint Last => Points[Points.Count - 1];
    }
}
