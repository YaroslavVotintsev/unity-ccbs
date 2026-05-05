using System;
using Mapf.Core.Model;
using Mapf.Core.Planning;

namespace Mapf.Core.CCBS
{
    /// <summary>
    /// Creates CBS constraints for one agent from a detected conflict.
    /// </summary>
    internal static class ConstraintGenerator
    {
        /// <summary>
        /// Returns a constraint that prevents the specified agent from repeating its conflicting move timing.
        /// </summary>
        public static Constraint ForAgent(int agentId, TimedMove ownMove, TimedMove otherMove, MapfPlannerSettings settings)
        {
            if (ownMove.IsWait)
            {
                var start = Math.Max(ownMove.StartTime, otherMove.StartTime);
                var end = Math.Min(ownMove.EndTime, otherMove.EndTime);
                return new Constraint(
                    agentId,
                    ownMove.FromNodeId,
                    ownMove.FromNodeId,
                    start,
                    double.IsPositiveInfinity(end) ? double.PositiveInfinity : Math.Max(start + settings.Epsilon, end));
            }

            var startConstraint = ownMove.StartTime;
            var endConstraint = startConstraint + settings.Epsilon * 2;

            if (otherMove.IsWait)
            {
                endConstraint = double.IsPositiveInfinity(otherMove.EndTime)
                    ? double.PositiveInfinity
                    : Math.Max(endConstraint, otherMove.EndTime + settings.Epsilon);
                return new Constraint(agentId, ownMove.FromNodeId, ownMove.ToNodeId, startConstraint, endConstraint);
            }

            if (CollisionMath.MovesCollide(ownMove, otherMove, settings.AgentRadius, settings.Epsilon, out var conflictTime))
            {
                endConstraint = Math.Max(endConstraint, CollisionMath.SafeStartTimeAfter(ownMove, otherMove, settings.AgentRadius, settings.Epsilon) + settings.Epsilon * 2);
            }

            return new Constraint(agentId, ownMove.FromNodeId, ownMove.ToNodeId, startConstraint, endConstraint);
        }
    }
}
