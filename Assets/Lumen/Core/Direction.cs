using System;

namespace Lumen.Core
{
    /// <summary>광선의 진행 방향 6개.</summary>
    public enum Direction
    {
        East,  // +X
        West,  // -X
        North, // +Z
        South, // -Z
        Up,    // +Y
        Down   // -Y
    }

    public static class DirectionExtensions
    {
        /// <summary>한 칸 전진할 때 더하는 좌표.</summary>
        public static Int3 ToOffset(this Direction direction)
        {
            switch (direction)
            {
                case Direction.East: return new Int3(1, 0, 0);
                case Direction.West: return new Int3(-1, 0, 0);
                case Direction.North: return new Int3(0, 0, 1);
                case Direction.South: return new Int3(0, 0, -1);
                case Direction.Up: return new Int3(0, 1, 0);
                case Direction.Down: return new Int3(0, -1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        /// <summary>위·아래 방향인지. 평면 거울과 분할기는 수직 광선을 막는다.</summary>
        public static bool IsVertical(this Direction direction) =>
            direction == Direction.Up || direction == Direction.Down;

        public static Direction Opposite(this Direction direction)
        {
            switch (direction)
            {
                case Direction.East: return Direction.West;
                case Direction.West: return Direction.East;
                case Direction.North: return Direction.South;
                case Direction.South: return Direction.North;
                case Direction.Up: return Direction.Down;
                case Direction.Down: return Direction.Up;
                default: throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}
