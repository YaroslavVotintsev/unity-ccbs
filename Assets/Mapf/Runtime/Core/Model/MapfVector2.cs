using System;

namespace Mapf.Core.Model
{
    /// <summary>
    /// Minimal double-precision 2D vector used by the pure planner without depending on UnityEngine.
    /// </summary>
    public readonly struct MapfVector2 : IEquatable<MapfVector2>
    {
        public readonly double X;
        public readonly double Y;

        public MapfVector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static MapfVector2 operator +(MapfVector2 a, MapfVector2 b) => new(a.X + b.X, a.Y + b.Y);
        public static MapfVector2 operator -(MapfVector2 a, MapfVector2 b) => new(a.X - b.X, a.Y - b.Y);
        public static MapfVector2 operator *(MapfVector2 a, double s) => new(a.X * s, a.Y * s);

        public double SqrMagnitude => X * X + Y * Y;
        public double Magnitude => Math.Sqrt(SqrMagnitude);

        public static double Distance(MapfVector2 a, MapfVector2 b) => (a - b).Magnitude;
        public static double Dot(MapfVector2 a, MapfVector2 b) => a.X * b.X + a.Y * b.Y;
        public static MapfVector2 Lerp(MapfVector2 a, MapfVector2 b, double t) => a + (b - a) * t;

        public bool Equals(MapfVector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is MapfVector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
