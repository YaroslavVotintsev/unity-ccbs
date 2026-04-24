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

            if (exit < -eps || enter > duration - eps)
                return false;

            conflictTime = start + Math.Max(0, enter);
            return true;
        }

        private static MapfVector2 Velocity(TimedMove move)
        {
            if (move.IsWait || double.IsPositiveInfinity(move.EndTime) || move.Duration <= 1e-9)
                return new MapfVector2(0, 0);

            return (move.To - move.From) * (1.0 / move.Duration);
        }
    }
}
