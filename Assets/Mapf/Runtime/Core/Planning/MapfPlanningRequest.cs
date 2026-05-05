using System.Collections.Generic;
using Mapf.Core.Graph;
using Mapf.Core.Model;

namespace Mapf.Core.Planning
{
    /// <summary>
    /// Complete pure C# planning request: graph snapshot, agent states, settings, existing plans, and reservations.
    /// </summary>
    public sealed class MapfPlanningRequest
    {
        public RoadmapGraph Graph { get; }
        public IReadOnlyList<AgentState> Agents { get; }
        public IReadOnlyList<TimedPath> ExistingPlans { get; }
        public IReadOnlyList<Reservation> Reservations { get; }
        public int? AffectedAgentId { get; }
        public MapfPlannerSettings Settings { get; }

        /// <summary>
        /// Creates a planning request. Existing plans and reservations are optional and mainly used during replanning.
        /// </summary>
        public MapfPlanningRequest(
            RoadmapGraph graph,
            IReadOnlyList<AgentState> agents,
            MapfPlannerSettings settings,
            IReadOnlyList<TimedPath> existingPlans = null,
            int? affectedAgentId = null,
            IReadOnlyList<Reservation> reservations = null)
        {
            Graph = graph;
            Agents = agents;
            Settings = settings ?? new MapfPlannerSettings();
            ExistingPlans = existingPlans ?? new List<TimedPath>();
            Reservations = reservations ?? new List<Reservation>();
            AffectedAgentId = affectedAgentId;
        }
    }
}
