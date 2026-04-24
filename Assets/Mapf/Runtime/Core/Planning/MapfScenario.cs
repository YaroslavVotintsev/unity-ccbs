using System.Collections.Generic;
using Mapf.Core.Graph;
using Mapf.Core.Model;

namespace Mapf.Core.Planning
{
    public sealed class MapfScenario
    {
        public string Name { get; }
        public RoadmapGraph Graph { get; }
        public IReadOnlyList<AgentState> Agents { get; }
        public IReadOnlyList<int> ExpectedVisitedNodes { get; }
        public MapfPlannerSettings Settings { get; }

        public MapfScenario(
            string name,
            RoadmapGraph graph,
            IReadOnlyList<AgentState> agents,
            MapfPlannerSettings settings = null,
            IReadOnlyList<int> expectedVisitedNodes = null)
        {
            Name = name;
            Graph = graph;
            Agents = agents;
            Settings = settings ?? new MapfPlannerSettings();
            ExpectedVisitedNodes = expectedVisitedNodes ?? new List<int>();
        }
    }
}
