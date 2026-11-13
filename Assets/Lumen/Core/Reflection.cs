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
    }
}
