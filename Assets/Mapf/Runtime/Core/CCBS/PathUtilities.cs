using System;
using System.Collections.Generic;
using Mapf.Core.Model;

namespace Mapf.Core.CCBS
{
    /// <summary>
    /// Helpers for converting timed paths into collision-checkable motion primitives.
    /// </summary>
    internal static class PathUtilities
    {
        /// <summary>
        /// Converts path points into timed moves and optionally adds the infinite goal reservation.
        /// </summary>
        public static IReadOnlyList<TimedMove> ToMoves(TimedPath path)
        {
            var moves = new List<TimedMove>();
            if (path == null || path.Points.Count == 0)
                return moves;

            for (var i = 0; i + 1 < path.Points.Count; i++)
            {
                var a = path.Points[i];
                var b = path.Points[i + 1];
                if (b.Time <= a.Time + 1e-9)
                    continue;

                moves.Add(new TimedMove(path.AgentId, a.NodeId, b.NodeId, a.Position, b.Position, a.Time, b.Time));
            }

            if (path.ReservesGoalAfterArrival)
            {
                var last = path.Last;
                moves.Add(new TimedMove(path.AgentId, last.NodeId, last.NodeId, last.Position, last.Position, last.Time, double.PositiveInfinity));
            }

            return moves;
        }
    }
}
