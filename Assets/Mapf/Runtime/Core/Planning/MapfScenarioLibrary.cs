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
                WaitBayMerge(),
                ThreeAgentCorridorWithTwoBays(),
                LoggedElevenNodeThreeAgent(),
                ThreeAgentsElevenNodeOppositeEnds(),
                FourAgentsTwelveNodeOppositeEnds(),
                FiveAgentsThirteenNodeOppositeEnds(),
                FiveAgentsLongSideBayCorridor()
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
                "Basic Straight Line Single Agent",
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
                "Basic Cross Intersection",
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
                    Node(3, "Sidestep", 1, -1)
                },
                new[] { (0, 1), (1, 2), (1, 3) });

            return new MapfScenario(
                "Basic Sidestep Swap",
                graph,
                new[] { new AgentState(0, 0, 2), new AgentState(1, 2, 0) },
                Settings(),
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
                "Basic Passing Loop",
                graph,
                new[] { new AgentState(0, 0, 3), new AgentState(1, 3, 0) },
                Settings(),
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
                    Node(4, "SouthEastGoal", 2, -1)
                },
                new[] { (0, 1), (3, 1), (1, 2), (2, 4) });

            return new MapfScenario(
                "Basic Wait Bay Merge",
                graph,
                new[] { new AgentState(0, 0, 2), new AgentState(1, 3, 4) },
                Settings());
        }

        public static MapfScenario ThreeAgentCorridorWithTwoBays()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "1", 0, 0),
                    Node(1, "2", 1, 0),
                    Node(2, "3", 2, 0),
                    Node(3, "4", 3, 0),
                    Node(4, "5", 4, 0),
                    Node(5, "6", 5, 0),
                    Node(6, "7", 6, 0),
                    Node(7, "8", 5, 1),
                    Node(8, "9", 2, -1)
                },
                new[] { (0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (5, 7), (2, 8) });

            return new MapfScenario(
                "Basic Three Agent Corridor With Two Bays",
                graph,
                new[]
                {
                    new AgentState(0, 0, 6),
                    new AgentState(1, 6, 0),
                    new AgentState(2, 8, 7)
                },
                Settings(),
                new[] { 7, 8 });
        }

        public static MapfScenario LoggedElevenNodeThreeAgent()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "1", 0, 0),
                    Node(1, "10", 3, -1),
                    Node(2, "11", 6, 1),
                    Node(3, "2", 1, 0),
                    Node(4, "3", 2, 0),
                    Node(5, "4", 3, 0),
                    Node(6, "5", 4, 0),
                    Node(7, "6", 5, 0),
                    Node(8, "7", 6, 0),
                    Node(9, "8", 7, 0),
                    Node(10, "9", 8, 0)
                },
                new[]
                {
                    (0, 3),
                    (1, 5),
                    (2, 8),
                    (3, 4),
                    (4, 5),
                    (5, 6),
                    (6, 7),
                    (7, 8),
                    (8, 9),
                    (9, 10)
                });

            return new MapfScenario(
                "Basic Logged Eleven Node Three Agent",
                graph,
                new[]
                {
                    new AgentState(0, 0, 8),
                    new AgentState(1, 10, 0),
                    new AgentState(2, 1, 2)
                },
                Settings(),
                new[] { 1, 2 });
        }

        public static MapfScenario ThreeAgentsElevenNodeOppositeEnds()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "1", 0, 0),
                    Node(1, "10", 3, -1),
                    Node(2, "11", 6, 1),
                    Node(3, "2", 1, 0),
                    Node(4, "3", 2, 0),
                    Node(5, "4", 3, 0),
                    Node(6, "5", 4, 0),
                    Node(7, "6", 5, 0),
                    Node(8, "7", 6, 0),
                    Node(9, "8", 7, 0),
                    Node(10, "9", 8, 0)
                },
                new[]
                {
                    (0, 3),
                    (1, 5),
                    (2, 8),
                    (3, 4),
                    (4, 5),
                    (5, 6),
                    (6, 7),
                    (7, 8),
                    (8, 9),
                    (9, 10)
                });

            return new MapfScenario(
                "Three Agents Eleven Node Opposite Ends",
                graph,
                new[]
                {
                    new AgentState(0, 0, 10),
                    new AgentState(1, 10, 0),
                    new AgentState(2, 1, 2)
                },
                Settings(),
                new[] { 1, 2 });
        }

        public static MapfScenario FourAgentsTwelveNodeOppositeEnds()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "1", 0, 0),
                    Node(1, "10", 3, -1),
                    Node(2, "11", 6, 1),
                    Node(3, "12", 7, -1),
                    Node(4, "2", 1, 0),
                    Node(5, "3", 2, 0),
                    Node(6, "4", 3, 0),
                    Node(7, "5", 4, 0),
                    Node(8, "6", 5, 0),
                    Node(9, "7", 6, 0),
                    Node(10, "8", 7, 0),
                    Node(11, "9", 8, 0)
                },
                new[]
                {
                    (0, 4),
                    (1, 6),
                    (2, 9),
                    (3, 10),
                    (4, 5),
                    (5, 6),
                    (6, 7),
                    (7, 8),
                    (8, 9),
                    (9, 10),
                    (10, 11)
                });

            return new MapfScenario(
                "Four Agents Twelve Node Opposite Ends",
                graph,
                new[]
                {
                    new AgentState(0, 0, 11),
                    new AgentState(1, 11, 0),
                    new AgentState(2, 1, 2),
                    new AgentState(3, 2, 3)
                },
                Settings(),
                new[] { 1, 2, 3 });
        }

        public static MapfScenario FiveAgentsThirteenNodeOppositeEnds()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    Node(0, "1", 0, 0),
                    Node(1, "10", 3, -1),
                    Node(2, "11", 6, 1),
                    Node(3, "12", 7, -1),
                    Node(4, "13", 1, 1),
                    Node(5, "2", 1, 0),
                    Node(6, "3", 2, 0),
                    Node(7, "4", 3, 0),
                    Node(8, "5", 4, 0),
                    Node(9, "6", 5, 0),
                    Node(10, "7", 6, 0),
                    Node(11, "8", 7, 0),
                    Node(12, "9", 8, 0)
                },
                new[]
                {
                    (0, 5),
                    (1, 7),
                    (2, 10),
                    (3, 11),
                    (4, 5),
                    (5, 6),
                    (6, 7),
                    (7, 8),
                    (8, 9),
                    (9, 10),
                    (10, 11),
                    (11, 12)
                });

            return new MapfScenario(
                "Five Agents Thirteen Node Opposite Ends",
                graph,
                new[]
                {
                    new AgentState(0, 0, 12),
                    new AgentState(1, 12, 0),
                    new AgentState(2, 1, 2),
                    new AgentState(3, 2, 3),
                    new AgentState(4, 4, 1)
                },
                Settings(),
                new[] { 1, 2, 3, 4 });
        }

        public static MapfScenario FiveAgentsLongSideBayCorridor()
        {
            var graph = LongSideBayCorridorGraph();

            return new MapfScenario(
                "Five Agents Long Side Bay Corridor",
                graph,
                new[]
                {
                    new AgentState(0, 28, 45),
                    new AgentState(1, 54, 48),
                    new AgentState(2, 0, 0),
                    new AgentState(3, 29, 42),
                    new AgentState(4, 36, 44)
                },
                Settings());
        }

        public static RoadmapGraph LongSideBayCorridorGraph()
        {
            var nodes = new List<RoadmapNode>();
            var edges = new List<(int, int)>();

            nodes.Add(Node(0, "12", 0, 0));
            for (var i = 1; i <= 28; i++)
                nodes.Add(Node(i, CorridorName(i), i, 0));

            edges.Add((0, 1));
            for (var i = 1; i < 28; i++)
                edges.Add((i, i + 1));

            for (var bay = 13; bay <= 38; bay++)
            {
                var bayIndex = nodes.Count;
                var corridorIndex = bay - 12;
                nodes.Add(Node(bayIndex, bay.ToString(), corridorIndex, 1));
                edges.Add((corridorIndex, bayIndex));
            }

            return new RoadmapGraph(nodes, edges);
        }

        private static RoadmapNode Node(int id, string name, double x, double y)
        {
            return new RoadmapNode(id, name, new MapfVector2(x, y));
        }

        private static string CorridorName(int index)
        {
            return index <= 9 ? (119 + index).ToString() : (120 + index).ToString();
        }

        private static MapfPlannerSettings Settings()
        {
            return new MapfPlannerSettings
            {
                AgentRadius = 0.3,
                AgentSpeed = 1,
                TimeLimitSeconds = 5,
                MaxHighLevelNodes = 20000,
                MaxLowLevelNodes = 5000,
                MaxLocalRepairIterations = 128
            };
        }
    }
}
