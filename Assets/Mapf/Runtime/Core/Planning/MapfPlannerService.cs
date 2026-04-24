using System;
using System.Threading;
using System.Threading.Tasks;
using Mapf.Core.CCBS;
using Mapf.Core.Model;

namespace Mapf.Core.Planning
{
    public sealed class MapfPlannerService
    {
        private readonly CcbsPlanner _planner = new();

        public Task<MapfPlanningResult> PlanAsync(MapfPlanningRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return Task.Run(() =>
            {
                try
                {
                    return _planner.Plan(request, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return new MapfPlanningResult(PlannerStatus.Cancelled, Array.Empty<TimedPath>(), "Planning was cancelled.");
                }
            }, cancellationToken);
        }
    }
}
