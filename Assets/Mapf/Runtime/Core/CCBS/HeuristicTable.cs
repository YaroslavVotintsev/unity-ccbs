using System;
using System.Collections.Generic;
using Mapf.Core.Graph;

namespace Mapf.Core.CCBS
{
    /// <summary>
    /// Caches shortest roadmap distances to goal nodes for low-level A* scoring.
    /// </summary>
    internal sealed class HeuristicTable
    {
        private readonly Dictionary<int, double[]> _byGoal = new();

        /// <summary>
        /// Returns the roadmap distance from a node to a goal, building and caching the table when needed.
        /// </summary>
        public double Get(RoadmapGraph graph, int nodeId, int goalId)
        {
            if (!_byGoal.TryGetValue(goalId, out var distances))
            {
                distances = Build(graph, goalId);
                _byGoal[goalId] = distances;
            }

            return distances[nodeId];
        }

        private static double[] Build(RoadmapGraph graph, int goalId)
        {
            var distance = new double[graph.Count];
            var visited = new bool[graph.Count];
            for (var i = 0; i < distance.Length; i++)
                distance[i] = double.PositiveInfinity;

            distance[goalId] = 0;
            for (var step = 0; step < graph.Count; step++)
            {
                var best = -1;
                var bestDistance = double.PositiveInfinity;
                for (var i = 0; i < graph.Count; i++)
                {
                    if (!visited[i] && distance[i] < bestDistance)
                    {
                        best = i;
                        bestDistance = distance[i];
                    }
                }

                if (best < 0)
                    break;

                visited[best] = true;
                foreach (var edge in graph.GetNeighbors(best))
                {
                    var candidate = distance[best] + edge.Length;
                    if (candidate < distance[edge.To])
                        distance[edge.To] = candidate;
                }
            }

            return distance;
        }
    }
}
