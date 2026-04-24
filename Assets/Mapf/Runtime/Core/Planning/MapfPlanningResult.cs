using System;
using System.Collections.Generic;
using Mapf.Core.Model;

namespace Mapf.Core.Planning
{
    public sealed class MapfPlanningResult
    {
        public PlannerStatus Status { get; }
        public IReadOnlyList<TimedPath> Paths { get; }
        public string Message { get; }
        public double Flowtime { get; }

        public bool Success => Status == PlannerStatus.Success;

        public MapfPlanningResult(PlannerStatus status, IReadOnlyList<TimedPath> paths, string message = "")
        {
            Status = status;
            Paths = paths ?? Array.Empty<TimedPath>();
            Message = message ?? string.Empty;

            var flowtime = 0.0;
            foreach (var path in Paths)
                if (!path.IsEmpty)
                    flowtime += path.Cost;
            Flowtime = flowtime;
        }
    }
}
