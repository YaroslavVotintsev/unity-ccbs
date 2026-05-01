using System.Linq;
using Mapf.Core.CCBS;
using Mapf.Core.Graph;
using Mapf.Core.Model;
using Mapf.Core.Planning;
using NUnit.Framework;

namespace Mapf.Tests
{
    public sealed class CcbsPlannerTests
    {
        [Test]
        public void SingleAgentFindsShortestRoadmapPath()
        {
            var graph = BuildCrossGraph();
            var request = new MapfPlanningRequest(
                graph,
                new[] { new AgentState(0, 0, 2) },
                new MapfPlannerSettings { AgentRadius = 0.1, AgentSpeed = 1 });

            var result = new CcbsPlanner().PlanGlobal(request);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Paths[0].Points.Select(p => p.NodeId), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(result.Paths[0].Cost, Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void TwoAgentsOnCrossingPathsReturnConflictFreePlans()
        {
            var graph = BuildCrossGraph();
            var request = new MapfPlanningRequest(
                graph,
                new[]
                {
                    new AgentState(0, 0, 2),
                    new AgentState(1, 3, 4)
                },
                new MapfPlannerSettings
                {
                    AgentRadius = 0.1,
                    AgentSpeed = 1,
                    TimeLimitSeconds = 2,
                    MaxHighLevelNodes = 2000
                });

            var result = new CcbsPlanner().PlanGlobal(request);

            Assert.That(result.Success, Is.True, result.Message);
            var detector = new PrivateConflictProbe();
            Assert.That(detector.HasAnyConflict(result.Paths, request.Settings), Is.False);
        }

        [Test]
        public void OppositeDirectionSwapUsesSidestepNode()
        {
            var graph = BuildSidestepGraph();
            var request = new MapfPlanningRequest(
                graph,
                new[]
                {
                    new AgentState(0, 0, 2),
                    new AgentState(1, 2, 0)
                },
                new MapfPlannerSettings
                {
                    AgentRadius = 0.1,
                    AgentSpeed = 1,
                    TimeLimitSeconds = 5,
                    MaxHighLevelNodes = 20000,
                    MaxLowLevelNodes = 20000
                });

            var result = new CcbsPlanner().PlanGlobal(request);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Paths.Any(path => path.Points.Any(point => point.NodeId == 3)), Is.True);
            var detector = new PrivateConflictProbe();
            Assert.That(detector.HasAnyConflict(result.Paths, request.Settings), Is.False);
        }

        [Test]
        public void AffectedAgentLocalRepairKeepsUnaffectedPlan()
        {
            var graph = BuildCrossGraph();
            var initial = new MapfPlanningRequest(
                graph,
                new[]
                {
                    new AgentState(0, 0, 2),
                    new AgentState(1, 3, 4)
                },
                new MapfPlannerSettings { AgentRadius = 0.1, AgentSpeed = 1, TimeLimitSeconds = 2 });
            var planner = new CcbsPlanner();
            var initialResult = planner.PlanGlobal(initial);
            Assert.That(initialResult.Success, Is.True, initialResult.Message);

            var replan = new MapfPlanningRequest(
                graph,
                new[]
                {
                    new AgentState(0, 0, 0),
                    new AgentState(1, 3, 4)
                },
                initial.Settings,
                initialResult.Paths,
                affectedAgentId: 0);

            var result = planner.Plan(replan);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Paths.First(p => p.AgentId == 1).Cost, Is.EqualTo(initialResult.Paths.First(p => p.AgentId == 1).Cost).Within(1e-6));
        }

        [Test]
        public void GlobalPlanAvoidsCommittedReservationFromNonPlannedAgent()
        {
            var graph = BuildLineGraph();
            var reservation = new Reservation(
                99,
                new TimedPath(99, new[]
                {
                    new TimedPathPoint(2, new MapfVector2(2, 0), 0),
                    new TimedPathPoint(1, new MapfVector2(1, 0), 1)
                }, reservesGoalAfterArrival: false));

            var request = new MapfPlanningRequest(
                graph,
                new[] { new AgentState(0, 0, 2) },
                new MapfPlannerSettings
                {
                    AgentRadius = 0.1,
                    AgentSpeed = 1,
                    TimeLimitSeconds = 2,
                    MaxHighLevelNodes = 2000
                },
                reservations: new[] { reservation });

            var result = new CcbsPlanner().PlanGlobal(request);

            Assert.That(result.Success, Is.True, result.Message);
            var arrivalAtReservedNode = result.Paths[0].Points.First(point => point.NodeId == 1);
            Assert.That(arrivalAtReservedNode.Time, Is.GreaterThan(1.0));
        }

        [Test]
        public void GoalReservationWaitsForInfiniteSafeInterval()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    new RoadmapNode(0, "West", new MapfVector2(0, 0)),
                    new RoadmapNode(1, "Goal", new MapfVector2(1, 0)),
                    new RoadmapNode(2, "East", new MapfVector2(2, 0)),
                    new RoadmapNode(3, "Bay", new MapfVector2(1, -1))
                },
                new[] { (0, 1), (1, 2), (1, 3) });
            var reservation = new Reservation(
                99,
                new TimedPath(99, new[]
                {
                    new TimedPathPoint(0, new MapfVector2(0, 0), 1.5),
                    new TimedPathPoint(2, new MapfVector2(2, 0), 3.5)
                }, reservesGoalAfterArrival: false));

            var request = new MapfPlanningRequest(
                graph,
                new[] { new AgentState(0, 3, 1) },
                new MapfPlannerSettings
                {
                    AgentRadius = 0.5,
                    AgentSpeed = 1,
                    TimeLimitSeconds = 2,
                    MaxHighLevelNodes = 2000,
                    MaxLowLevelNodes = 2000
                },
                reservations: new[] { reservation });

            var result = new CcbsPlanner().PlanGlobal(request);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Paths[0].Last.Time, Is.GreaterThanOrEqualTo(3.5));
        }

        [Test]
        public void OwnCommittedReservationDoesNotBlockSuffixPlanning()
        {
            var graph = BuildLineGraph();
            var existing = new TimedPath(0, new[]
            {
                new TimedPathPoint(0, new MapfVector2(0, 0), 0),
                new TimedPathPoint(1, new MapfVector2(1, 0), 10)
            });
            var reservation = new Reservation(0, existing);
            var request = new MapfPlanningRequest(
                graph,
                new[] { new AgentState(0, 1, 2, 10) },
                new MapfPlannerSettings
                {
                    AgentRadius = 0.1,
                    AgentSpeed = 1,
                    TimeLimitSeconds = 2,
                    MaxHighLevelNodes = 2000
                },
                existingPlans: new[] { existing },
                reservations: new[] { reservation });

            var result = new CcbsPlanner().PlanGlobal(request);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Paths[0].Points.Select(p => p.NodeId), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(result.Paths[0].Points.Last().Time, Is.EqualTo(11).Within(1e-6));
        }

        [Test]
        public void ThreeAgentCorridorUsesWaitingInsteadOfLoopingThirdAgent()
        {
            var scenario = MapfScenarioLibrary.ThreeAgentCorridorWithTwoBays();
            var result = new CcbsPlanner().PlanGlobal(new MapfPlanningRequest(scenario.Graph, scenario.Agents, scenario.Settings));

            Assert.That(result.Success, Is.True, result.Message);
            var agentC = result.Paths.First(path => path.AgentId == 2);
            Assert.That(agentC.Points.First().NodeId, Is.EqualTo(8));
            Assert.That(agentC.Points.Last().NodeId, Is.EqualTo(7));

            var travelNodes = agentC.Points
                .Where((point, index) => index == 0 || point.NodeId != agentC.Points[index - 1].NodeId)
                .Select(point => point.NodeId)
                .ToArray();
            Assert.That(travelNodes.Distinct().Count(), Is.EqualTo(travelNodes.Length));
        }

        [Test]
        public void WaitBayMergeStaysSolvable()
        {
            var scenario = MapfScenarioLibrary.WaitBayMerge();
            var result = new CcbsPlanner().PlanGlobal(new MapfPlanningRequest(scenario.Graph, scenario.Agents, scenario.Settings));

            Assert.That(result.Success, Is.True, result.Message);
            var detector = new PrivateConflictProbe();
            Assert.That(detector.HasAnyConflict(result.Paths, scenario.Settings), Is.False);
        }

        [Test]
        public void FiveAgentSideBayCorridorStaysSolvable()
        {
            var graph = MapfScenarioLibrary.LongSideBayCorridorGraph();
            var request = new MapfPlanningRequest(
                graph,
                new[]
                {
                    new AgentState(0, 28, 45),
                    new AgentState(1, 54, 48),
                    new AgentState(2, 0, 0),
                    new AgentState(3, 29, 42),
                    new AgentState(4, 36, 44)
                },
                new MapfPlannerSettings
                {
                    AgentRadius = 0.5,
                    AgentSpeed = 5,
                    TimeLimitSeconds = 5,
                    MaxHighLevelNodes = 100000,
                    MaxLowLevelNodes = 100000,
                    MaxLocalRepairIterations = 10000
                });

            var result = new CcbsPlanner().PlanGlobal(request);

            Assert.That(result.Success, Is.True, result.Message);
            var detector = new PrivateConflictProbe();
            Assert.That(detector.HasAnyConflict(result.Paths, request.Settings), Is.False);
        }

        private static RoadmapGraph BuildCrossGraph()
        {
            return new RoadmapGraph(
                new[]
                {
                    new RoadmapNode(0, "A", new MapfVector2(0, 0)),
                    new RoadmapNode(1, "B", new MapfVector2(1, 0)),
                    new RoadmapNode(2, "C", new MapfVector2(2, 0)),
                    new RoadmapNode(3, "D", new MapfVector2(1, -1)),
                    new RoadmapNode(4, "E", new MapfVector2(1, 1))
                },
                new[] { (0, 1), (1, 2), (1, 3), (1, 4) });
        }

        private static RoadmapGraph BuildLineGraph()
        {
            return new RoadmapGraph(
                new[]
                {
                    new RoadmapNode(0, "A", new MapfVector2(0, 0)),
                    new RoadmapNode(1, "B", new MapfVector2(1, 0)),
                    new RoadmapNode(2, "C", new MapfVector2(2, 0))
                },
                new[] { (0, 1), (1, 2) });
        }

        private static RoadmapGraph BuildSidestepGraph()
        {
            return new RoadmapGraph(
                new[]
                {
                    new RoadmapNode(0, "A", new MapfVector2(0, 0)),
                    new RoadmapNode(1, "B", new MapfVector2(1, 0)),
                    new RoadmapNode(2, "C", new MapfVector2(2, 0)),
                    new RoadmapNode(3, "D", new MapfVector2(1, -1))
                },
                new[] { (0, 1), (1, 2), (1, 3) });
        }

        private sealed class PrivateConflictProbe
        {
            public bool HasAnyConflict(System.Collections.Generic.IReadOnlyList<TimedPath> paths, MapfPlannerSettings settings)
            {
                for (var i = 0; i < paths.Count; i++)
                for (var j = i + 1; j < paths.Count; j++)
                {
                    foreach (var a in ToMoves(paths[i]))
                    foreach (var b in ToMoves(paths[j]))
                    {
                        if (OverlapsAndTooClose(a, b, settings.AgentRadius))
                            return true;
                    }
                }

                return false;
            }

            private static System.Collections.Generic.IEnumerable<TimedMove> ToMoves(TimedPath path)
            {
                for (var i = 0; i + 1 < path.Points.Count; i++)
                    yield return new TimedMove(path.AgentId, path.Points[i].NodeId, path.Points[i + 1].NodeId, path.Points[i].Position, path.Points[i + 1].Position, path.Points[i].Time, path.Points[i + 1].Time);

                if (path.ReservesGoalAfterArrival && path.Points.Count > 0)
                {
                    var last = path.Last;
                    yield return new TimedMove(path.AgentId, last.NodeId, last.NodeId, last.Position, last.Position, last.Time, double.PositiveInfinity);
                }
            }

            private static bool OverlapsAndTooClose(TimedMove a, TimedMove b, double radius)
            {
                var start = System.Math.Max(a.StartTime, b.StartTime);
                var end = System.Math.Min(a.EndTime, b.EndTime);
                if (end <= start)
                    return false;

                for (var k = 0; k <= 10; k++)
                {
                    var t = start + (end - start) * k / 10.0;
                    if (MapfVector2.Distance(a.PositionAt(t), b.PositionAt(t)) < radius * 2 - 1e-6)
                        return true;
                }

                return false;
            }
        }
    }
}
