using System.Collections.Generic;
using System.Linq;
using Mapf.Core.CCBS;
using Mapf.Core.Model;
using Mapf.Core.Planning;
using NUnit.Framework;

namespace Mapf.Tests
{
    public sealed class MapfScenarioLibraryTests
    {
        public static IEnumerable<TestCaseData> Scenarios()
        {
            foreach (var scenario in MapfScenarioLibrary.All())
                yield return new TestCaseData(scenario).SetName($"Scenario_{scenario.Name.Replace(" ", "_")}");
        }

        [TestCaseSource(nameof(Scenarios))]
        public void ScenarioFindsConflictFreePlan(MapfScenario scenario)
        {
            var request = new MapfPlanningRequest(scenario.Graph, scenario.Agents, scenario.Settings);
            var result = new CcbsPlanner().PlanGlobal(request);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(HasAnyConflict(result.Paths, scenario.Settings), Is.False);

            foreach (var expectedNode in scenario.ExpectedVisitedNodes)
            {
                Assert.That(
                    result.Paths.Any(path => path.Points.Any(point => point.NodeId == expectedNode)),
                    Is.True,
                    $"Expected at least one path in '{scenario.Name}' to visit node {expectedNode}.");
            }
        }

        private static bool HasAnyConflict(IReadOnlyList<TimedPath> paths, MapfPlannerSettings settings)
        {
            for (var i = 0; i < paths.Count; i++)
            for (var j = i + 1; j < paths.Count; j++)
            foreach (var a in ToMoves(paths[i]))
            foreach (var b in ToMoves(paths[j]))
            {
                if (OverlapsAndTooClose(a, b, settings.AgentRadius))
                    return true;
            }

            return false;
        }

        private static IEnumerable<TimedMove> ToMoves(TimedPath path)
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

            for (var k = 0; k <= 20; k++)
            {
                var t = start + (end - start) * k / 20.0;
                if (MapfVector2.Distance(a.PositionAt(t), b.PositionAt(t)) < radius * 2 - 1e-6)
                    return true;
            }

            return false;
        }
    }
}
