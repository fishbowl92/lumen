using System;

namespace Lumen.Core
{
    /// <summary>
    /// 격자 칸 좌표. x = 동(+)/서(-), y = 층(위 +), z = 북(+)/남(-).
    /// UnityEngine.Vector3Int를 쓰지 않는 이유: 코어는 Unity 없이 솔버와 테스트에서도 돌아야 한다.
    /// </summary>
    public readonly struct Int3 : IEquatable<Int3>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public Int3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Int3 operator +(Int3 a, Int3 b) => new Int3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Int3 operator -(Int3 a, Int3 b) => new Int3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static bool operator ==(Int3 a, Int3 b) => a.Equals(b);

        public static bool operator !=(Int3 a, Int3 b) => !a.Equals(b);

        public bool Equals(Int3 other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is Int3 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
