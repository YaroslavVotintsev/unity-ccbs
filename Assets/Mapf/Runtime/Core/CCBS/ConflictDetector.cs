using System;
using System.Collections.Generic;
using Mapf.Core.Model;
using Mapf.Core.Planning;

namespace Mapf.Core.CCBS
{
    /// <summary>
    /// Detects continuous-time pairwise conflicts between timed paths.
    /// </summary>
    internal sealed class ConflictDetector
    {
        /// <summary>
        /// Finds the earliest conflict across a set of paths.
        /// </summary>
        public Conflict FindFirstConflict(IReadOnlyList<TimedPath> paths, MapfPlannerSettings settings)
        {
            Conflict best = default;
            var bestTime = double.PositiveInfinity;

            for (var i = 0; i < paths.Count; i++)
            {
                for (var j = i + 1; j < paths.Count; j++)
                {
                    var conflict = FindFirstConflict(paths[i], paths[j], settings);
                    if (conflict.IsValid && conflict.Time < bestTime)
                    {
                        best = conflict;
                        bestTime = conflict.Time;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Returns all detected pairwise conflicts, ordered by time.
        /// </summary>
        public IReadOnlyList<Conflict> FindAllConflicts(IReadOnlyList<TimedPath> paths, MapfPlannerSettings settings)
        {
            var conflicts = new List<Conflict>();
            for (var i = 0; i < paths.Count; i++)
            {
                for (var j = i + 1; j < paths.Count; j++)
                    AddConflicts(paths[i], paths[j], settings, conflicts);
            }

            conflicts.Sort((a, b) => a.Time.CompareTo(b.Time));
            return conflicts;
        }

        /// <summary>
        /// Finds the earliest conflict between two paths.
        /// </summary>
        public Conflict FindFirstConflict(TimedPath a, TimedPath b, MapfPlannerSettings settings)
        {
            var movesA = PathUtilities.ToMoves(a);
            var movesB = PathUtilities.ToMoves(b);
            Conflict best = default;
            var bestTime = double.PositiveInfinity;

            foreach (var moveA in movesA)
            {
                foreach (var moveB in movesB)
                {
                    if (!CollisionMath.MovesCollide(moveA, moveB, settings.AgentRadius, settings.Epsilon, out var time))
                        continue;

                    if (time < bestTime)
                    {
                        best = new Conflict(a.AgentId, b.AgentId, moveA, moveB, time);
                        bestTime = time;
                    }
                }
            }

            return best;
        }

        private static void AddConflicts(TimedPath a, TimedPath b, MapfPlannerSettings settings, List<Conflict> conflicts)
        {
            var movesA = PathUtilities.ToMoves(a);
            var movesB = PathUtilities.ToMoves(b);
            foreach (var moveA in movesA)
            foreach (var moveB in movesB)
            {
                if (CollisionMath.MovesCollide(moveA, moveB, settings.AgentRadius, settings.Epsilon, out var time))
                    conflicts.Add(new Conflict(a.AgentId, b.AgentId, moveA, moveB, time));
            }
        }

        /// <summary>
        /// Checks one path against fixed paths and returns the first conflict found.
        /// </summary>
        public bool HasConflict(TimedPath path, IEnumerable<TimedPath> fixedPaths, MapfPlannerSettings settings, out Conflict conflict)
        {
            conflict = default;
            foreach (var fixedPath in fixedPaths)
            {
                if (fixedPath.AgentId == path.AgentId)
                    continue;

                conflict = FindFirstConflict(path, fixedPath, settings);
                if (conflict.IsValid)
                    return true;
            }

            return false;
        }
    }
}
