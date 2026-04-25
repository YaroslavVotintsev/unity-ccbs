using System.Collections.Generic;
using Mapf.Core.Graph;
using Mapf.Core.Model;

namespace Mapf.Core.Planning
{
    public sealed class MapfPlanningRequest
    {
        public RoadmapGraph Graph { get; }
        public IReadOnlyList<AgentState> Agents { get; }
        public IReadOnlyList<TimedPath> ExistingPlans { get; }
        public IReadOnlyList<Reservation> Reservations { get; }
        public int? AffectedAgentId { get; }
        public MapfPlannerSettings Settings { get; }

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
