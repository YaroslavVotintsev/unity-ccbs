using Mapf.Core.Graph;
using Mapf.Core.Model;
using NUnit.Framework;

namespace Mapf.Tests
{
    public sealed class RoadmapGraphTests
    {
        [Test]
        public void UndirectedEdgesBecomeBidirectional()
        {
            var graph = new RoadmapGraph(
                new[]
                {
                    new RoadmapNode(0, "A", new MapfVector2(0, 0)),
                    new RoadmapNode(1, "B", new MapfVector2(2, 0))
                },
                new[] { (0, 1) });

            Assert.That(graph.GetNeighbors(0).Count, Is.EqualTo(1));
            Assert.That(graph.GetNeighbors(0)[0].To, Is.EqualTo(1));
            Assert.That(graph.GetNeighbors(1).Count, Is.EqualTo(1));
            Assert.That(graph.GetNeighbors(1)[0].To, Is.EqualTo(0));
            Assert.That(graph.TravelTime(0, 1, 2), Is.EqualTo(1).Within(1e-6));
        }
    }
}
