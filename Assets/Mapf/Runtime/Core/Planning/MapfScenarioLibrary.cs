using System.Collections.Generic;
using Mapf.Core.Graph;
using Mapf.Core.Model;

namespace Mapf.Core.Planning
{
    public static class MapfScenarioLibrary
    {
        public static IReadOnlyList<MapfScenario> All()
        {
            return new[]
            {
                StraightLineSingleAgent(),
                CrossIntersection(),
                SidestepSwap(),
                PassingLoop(),
                WaitBayMerge()
            };
        }

        public static MapfScenario StraightLineSingleAgent()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "A", 0, 0),
                    Node(1, "B", 1, 0),
                    Node(2, "C", 2, 0)
                },
                new[] { (0, 1), (1, 2) });

            return new MapfScenario(
                "Straight Line Single Agent",
                graph,
                new[] { new AgentState(0, 0, 2) },
                Settings());
        }

        public static MapfScenario CrossIntersection()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "West", 0, 0),
                    Node(1, "Center", 1, 0),
                    Node(2, "East", 2, 0),
                    Node(3, "South", 1, -1),
                    Node(4, "North", 1, 1)
                },
                new[] { (0, 1), (1, 2), (1, 3), (1, 4) });

            return new MapfScenario(
                "Cross Intersection",
                graph,
                new[] { new AgentState(0, 0, 2), new AgentState(1, 3, 4) },
                Settings());
        }

        public static MapfScenario SidestepSwap()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "A", 0, 0),
                    Node(1, "B", 1, 0),
                    Node(2, "C", 2, 0),
                    Node(3, "Sidestep", 1, -1, RoadmapNodeKind.Sidestep)
                },
                new[] { (0, 1), (1, 2), (1, 3) });

            return new MapfScenario(
                "Sidestep Swap",
                graph,
                new[] { new AgentState(0, 0, 2), new AgentState(1, 2, 0) },
                Settings(maxLowLevelNodes: 20000),
                new[] { 3 });
        }

        public static MapfScenario PassingLoop()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "West", 0, 0),
                    Node(1, "MidWest", 1, 0),
                    Node(2, "MidEast", 2, 0),
                    Node(3, "East", 3, 0),
                    Node(4, "LoopSouthWest", 1, -1),
                    Node(5, "LoopSouthEast", 2, -1)
                },
                new[] { (0, 1), (1, 2), (2, 3), (1, 4), (4, 5), (5, 2) });

            return new MapfScenario(
                "Passing Loop",
                graph,
                new[] { new AgentState(0, 0, 3), new AgentState(1, 3, 0) },
                Settings(maxLowLevelNodes: 30000),
                new[] { 4, 5 });
        }

        public static MapfScenario WaitBayMerge()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "WestSource", 0, 0),
                    Node(1, "Merge", 1, 0),
                    Node(2, "EastGoal", 2, 0),
                    Node(3, "SouthSource", 1, -1),
                    Node(4, "SouthEastGoal", 2, -1, RoadmapNodeKind.Waiting)
                },
                new[] { (0, 1), (3, 1), (1, 2), (2, 4) });

            return new MapfScenario(
                "Wait Bay Merge",
                graph,
                new[] { new AgentState(0, 0, 2), new AgentState(1, 3, 4) },
                Settings(maxLowLevelNodes: 20000));
        }

        private static RoadmapNode Node(int id, string name, double x, double y, RoadmapNodeKind kind = RoadmapNodeKind.Generic)
        {
            return new RoadmapNode(id, name, new MapfVector2(x, y), kind);
        }

        private static MapfPlannerSettings Settings(int maxLowLevelNodes = 10000)
        {
            return new MapfPlannerSettings
            {
                AgentRadius = 0.1,
                AgentSpeed = 1,
                TimeLimitSeconds = 5,
                MaxHighLevelNodes = 30000,
                MaxLowLevelNodes = maxLowLevelNodes,
                MaxLocalRepairIterations = 256
            };
        }
    }
}
