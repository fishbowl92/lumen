using System;

namespace Lumen.Core
{
    /// <summary>거울의 반사 표. 기획서 규칙 명세의 표를 그대로 옮긴다.</summary>
    public static class Reflection
    {
        /// <summary>
        /// 평면 거울(분할기의 꺾인 광선 포함)의 반사 방향.
        /// 수직 광선은 막히므로 이 함수에 넣지 않는다.
        /// </summary>
        public static Direction ReflectPlane(PieceState state, Direction incoming)
        {
            if (incoming.IsVertical())
            {
                throw new ArgumentException("평면 거울은 수직 광선을 반사하지 않는다(차단).", nameof(incoming));
            }

            switch (state)
            {
                case PieceState.Slash:
                    switch (incoming)
                    {
                        case Direction.East: return Direction.North;
                        case Direction.West: return Direction.South;
                        case Direction.North: return Direction.East;
                        default: return Direction.West; // South
                    }
                case PieceState.Backslash:
                    switch (incoming)
                    {
                        case Direction.East: return Direction.South;
                        case Direction.West: return Direction.North;
                        case Direction.North: return Direction.West;
                        default: return Direction.East; // South
                    }
                default:
                    throw new ArgumentException($"평면 거울 상태가 아니다: {state}", nameof(state));
            }
        }

        /// <summary>
        /// 승강 거울의 반사 방향. 거울 면과 나란한 광선은 빈칸처럼 통과하므로 들어온 방향을 그대로 돌려준다.
        /// XRise: 동→위, 아래→서, 서→아래, 위→동 (남북 통과)
        /// XFall: 동→아래, 위→서, 서→위, 아래→동 (남북 통과)
        /// ZRise: 북→위, 아래→남, 남→아래, 위→북 (동서 통과)
        /// ZFall: 북→아래, 위→남, 남→위, 아래→북 (동서 통과)
        /// </summary>
        public static Direction ReflectLift(PieceState state, Direction incoming)
        {
            switch (state)
            {
                case PieceState.XRise:
                    switch (incoming)
                    {
                        case Direction.East: return Direction.Up;
                        case Direction.Down: return Direction.West;
                        case Direction.West: return Direction.Down;
                        case Direction.Up: return Direction.East;
                        default: return incoming;
                    }
                case PieceState.XFall:
                    switch (incoming)
                    {
                        case Direction.East: return Direction.Down;
                        case Direction.Up: return Direction.West;
                        case Direction.West: return Direction.Up;
                        case Direction.Down: return Direction.East;
                        default: return incoming;
                    }
                case PieceState.ZRise:
                    switch (incoming)
                    {
                        case Direction.North: return Direction.Up;
                        case Direction.Down: return Direction.South;
                        case Direction.South: return Direction.Down;
                        case Direction.Up: return Direction.North;
                        default: return incoming;
                    }
                case PieceState.ZFall:
                    switch (incoming)
                    {
                        case Direction.North: return Direction.Down;
                        case Direction.Up: return Direction.South;
                        case Direction.South: return Direction.Up;
                        case Direction.Down: return Direction.North;
                        default: return incoming;
                    }
                default:
                    throw new ArgumentException($"승강 거울 상태가 아니다: {state}", nameof(state));
            }
        }
    }
}
