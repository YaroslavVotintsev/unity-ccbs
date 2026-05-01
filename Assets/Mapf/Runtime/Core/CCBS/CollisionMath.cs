using System;
using Mapf.Core.Model;

namespace Mapf.Core.CCBS
{
    internal static class CollisionMath
    {
        public static bool MovesCollide(TimedMove a, TimedMove b, double radius, double eps, out double conflictTime)
        {
            conflictTime = 0;
            var start = Math.Max(a.StartTime, b.StartTime);
            var end = Math.Min(a.EndTime, b.EndTime);
            if (start > end - eps)
                return false;

            if (double.IsPositiveInfinity(end))
            {
                var distance = MapfVector2.Distance(a.PositionAt(start), b.PositionAt(start));
                if (distance < radius * 2 - eps)
                {
                    conflictTime = start;
                    return true;
                }

                return false;
            }

            var duration = end - start;
            if (duration < eps)
                return false;

            var a0 = a.PositionAt(start);
            var b0 = b.PositionAt(start);
            var va = Velocity(a);
            var vb = Velocity(b);
            var relativePosition = a0 - b0;
            var relativeVelocity = va - vb;
            var r = radius * 2;

            var aa = MapfVector2.Dot(relativeVelocity, relativeVelocity);
            var bb = 2 * MapfVector2.Dot(relativePosition, relativeVelocity);
            var cc = MapfVector2.Dot(relativePosition, relativePosition) - r * r;

            if (cc < -eps)
            {
                conflictTime = start;
                return true;
            }

            if (aa < eps)
                return false;

            var discriminant = bb * bb - 4 * aa * cc;
            if (discriminant < -eps)
                return false;

            discriminant = Math.Max(0, discriminant);
            var enter = (-bb - Math.Sqrt(discriminant)) / (2 * aa);
            var exit = (-bb + Math.Sqrt(discriminant)) / (2 * aa);

            if (exit - enter <= eps)
                return false;

            if (exit < -eps || enter > duration - eps)
                return false;

            conflictTime = start + Math.Max(0, enter);
            return true;
        }

        public static double SafeStartTimeAfter(TimedMove ownMove, TimedMove otherMove, double radius, double eps)
        {
            if (!MovesCollide(ownMove, otherMove, radius, eps, out _))
                return ownMove.StartTime;

            var duration = ownMove.Duration;
            var low = ownMove.StartTime;
            var high = Math.Max(ownMove.StartTime, otherMove.EndTime + duration + radius * 2 + eps);

            var shifted = ownMove;
            for (var guard = 0; guard < 64 && MovesCollide(shifted, otherMove, radius, eps, out _); guard++)
            {
                high += Math.Max(duration, 1.0) + radius * 2 + eps;
                shifted = Shift(ownMove, high);
            }

            for (var i = 0; i < 80; i++)
            {
                var mid = (low + high) * 0.5;
                shifted = Shift(ownMove, mid);
                if (MovesCollide(shifted, otherMove, radius, eps, out _))
                    low = mid;
                else
                    high = mid;
            }

            return high;
        }

        public static bool PointMoveUnsafeInterval(
            MapfVector2 point,
            TimedMove move,
            double radius,
            double eps,
            out double start,
            out double end)
        {
            start = 0;
            end = 0;
            var r = radius * 2;
            var d = move.To - move.From;
            var duration = move.Duration;
            if (duration <= eps || double.IsPositiveInfinity(duration))
            {
                if (MapfVector2.Distance(point, move.From) < r - eps)
                {
                    start = move.StartTime;
                    end = move.EndTime;
                    return true;
                }

                return false;
            }

            var f = move.From - point;
            var a = MapfVector2.Dot(d, d);
            var b = 2 * MapfVector2.Dot(f, d);
            var c = MapfVector2.Dot(f, f) - r * r;
            if (!UnitIntervalRoots(a, b, c, eps, out var u0, out var u1))
                return false;

            start = move.StartTime + u0 * duration;
            end = move.StartTime + u1 * duration;
            return end > start + eps;
        }

        public static bool MoveAgainstPointUnsafeStartInterval(
            TimedMove move,
            MapfVector2 point,
            double pointOccupiedStart,
            double pointOccupiedEnd,
            double radius,
            double eps,
            out double start,
            out double end)
        {
            start = 0;
            end = 0;
            if (!PointMoveUnsafeInterval(point, new TimedMove(move.AgentId, move.FromNodeId, move.ToNodeId, move.From, move.To, 0, move.Duration), radius, eps, out var relativeStart, out var relativeEnd))
                return false;

            start = pointOccupiedStart - relativeEnd;
            end = double.IsPositiveInfinity(pointOccupiedEnd)
                ? double.PositiveInfinity
                : pointOccupiedEnd - relativeStart;
            return end > start + eps;
        }

        private static bool UnitIntervalRoots(double a, double b, double c, double eps, out double u0, out double u1)
        {
            u0 = 0;
            u1 = 0;

            if (c < -eps)
            {
                u0 = 0;
                u1 = 1;
            }

            if (Math.Abs(a) < eps)
            {
                if (c < -eps)
                {
                    u0 = 0;
                    u1 = 1;
                    return true;
                }

                return false;
            }

            var discriminant = b * b - 4 * a * c;
            if (discriminant < -eps)
                return false;

            discriminant = Math.Max(0, discriminant);
            var sqrt = Math.Sqrt(discriminant);
            var r0 = (-b - sqrt) / (2 * a);
            var r1 = (-b + sqrt) / (2 * a);
            u0 = Math.Max(0, Math.Min(r0, r1));
            u1 = Math.Min(1, Math.Max(r0, r1));
            return u1 > u0 + eps;
        }

        private static TimedMove Shift(TimedMove move, double startTime)
        {
            return new TimedMove(
                move.AgentId,
                move.FromNodeId,
                move.ToNodeId,
                move.From,
                move.To,
                startTime,
                startTime + move.Duration);
        }

        private static MapfVector2 Velocity(TimedMove move)
        {
            if (move.IsWait || double.IsPositiveInfinity(move.EndTime) || move.Duration <= 1e-9)
                return new MapfVector2(0, 0);

            return (move.To - move.From) * (1.0 / move.Duration);
        }
    }
}
