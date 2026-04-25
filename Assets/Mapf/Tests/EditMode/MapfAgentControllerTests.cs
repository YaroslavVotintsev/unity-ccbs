using Mapf.Core.Model;
using Mapf.UnityAdapter;
using NUnit.Framework;
using UnityEngine;

namespace Mapf.Tests
{
    public sealed class MapfAgentControllerTests
    {
        [Test]
        public void ApplyingFutureSuffixDoesNotTeleportMidEdge()
        {
            var gameObject = new GameObject("Agent");
            try
            {
                var controller = gameObject.AddComponent<MapfAgentController>();
                controller.ApplyPlan(
                    new TimedPath(0, new[]
                    {
                        new TimedPathPoint(0, new MapfVector2(0, 0), 0),
                        new TimedPathPoint(1, new MapfVector2(10, 0), 10)
                    }),
                    now: 5);

                Assert.That(gameObject.transform.position.x, Is.EqualTo(5).Within(1e-5));

                controller.ApplyPlanPreservingCommittedPrefix(
                    new TimedPath(0, new[]
                    {
                        new TimedPathPoint(1, new MapfVector2(10, 0), 10),
                        new TimedPathPoint(2, new MapfVector2(20, 0), 20)
                    }),
                    now: 5);

                Assert.That(gameObject.transform.position.x, Is.EqualTo(5).Within(1e-5));
                Assert.That(controller.CurrentPoints.Count, Is.EqualTo(3));
                Assert.That(controller.CurrentPoints[0].NodeId, Is.EqualTo(0));
                Assert.That(controller.CurrentPoints[1].NodeId, Is.EqualTo(1));
                Assert.That(controller.CurrentPoints[2].NodeId, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ReplanningWhileWaitingStartsAtCurrentNodeNow()
        {
            var gameObject = new GameObject("Agent");
            try
            {
                var controller = gameObject.AddComponent<MapfAgentController>();
                controller.ApplyPlan(
                    new TimedPath(0, new[]
                    {
                        new TimedPathPoint(4, new MapfVector2(1, 1), 0),
                        new TimedPathPoint(4, new MapfVector2(1, 1), 10),
                        new TimedPathPoint(5, new MapfVector2(2, 1), 20)
                    }),
                    now: 5);

                var state = controller.GetPlanningState(0, fallbackNodeId: 99, goalNodeId: 7, now: 5);

                Assert.That(state.StartNodeId, Is.EqualTo(4));
                Assert.That(state.EarliestStartTime, Is.EqualTo(5).Within(1e-5));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CommittedReservationDoesNotReserveNextNodeForever()
        {
            var gameObject = new GameObject("Agent");
            try
            {
                var controller = gameObject.AddComponent<MapfAgentController>();
                controller.ApplyPlan(
                    new TimedPath(0, new[]
                    {
                        new TimedPathPoint(0, new MapfVector2(0, 0), 0),
                        new TimedPathPoint(1, new MapfVector2(10, 0), 10),
                        new TimedPathPoint(2, new MapfVector2(20, 0), 20)
                    }),
                    now: 5);

                var reservation = controller.GetCommittedReservation(0, now: 5);

                Assert.That(reservation.HasValue, Is.True);
                Assert.That(reservation.Value.Path.ReservesGoalAfterArrival, Is.False);
                Assert.That(reservation.Value.Path.Points.Count, Is.EqualTo(2));
                Assert.That(reservation.Value.Path.Points[0].Time, Is.EqualTo(5).Within(1e-5));
                Assert.That(reservation.Value.Path.Points[1].NodeId, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
