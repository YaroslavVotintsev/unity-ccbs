using System;
using System.Threading;
using System.Threading.Tasks;
using Mapf.Core.CCBS;
using Mapf.Core.Model;

namespace Mapf.Core.Planning
{
    /// <summary>
    /// Asynchronous service wrapper around <see cref="CcbsPlanner"/>.
    /// </summary>
    public sealed class MapfPlannerService
    {
        private readonly CcbsPlanner _planner = new();

        /// <summary>
        /// Runs planning on a worker task and returns a result object, converting cancellation into a cancelled status.
        /// </summary>
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
