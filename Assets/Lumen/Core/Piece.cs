using System;

namespace Lumen.Core
{
    /// <summary>거울 종류.</summary>
    public enum PieceType
    {
        PlaneMirror, // 평면 거울: 수평 광선을 90° 꺾는다
        LiftMirror,  // 승강 거울: 수평 광선과 수직 광선을 서로 바꾼다
        Splitter     // 분할기: 평면 거울처럼 꺾은 광선과 직진 광선 두 개로 나눈다
    }

    /// <summary>
    /// 거울 상태. 평면 거울·분할기는 Slash(/)·Backslash(\),
    /// 승강 거울은 XRise·ZRise·XFall·ZFall 중 하나를 쓴다.
    /// </summary>
    public enum PieceState
    {
        Slash,
        Backslash,
        XRise,
        ZRise,
        XFall,
        ZFall
    }

    /// <summary>격자에 놓인 거울 하나. 고정형(IsFixed)은 플레이어가 움직이거나 돌릴 수 없다.</summary>
    public readonly struct Piece : IEquatable<Piece>
    {
        public readonly PieceType Type;
        public readonly PieceState State;
        public readonly bool IsFixed;

        public Piece(PieceType type, PieceState state, bool isFixed)
        {
            if (!PieceRules.IsValidState(type, state))
            {
                throw new ArgumentException($"{type}에는 {state} 상태를 쓸 수 없다.", nameof(state));
            }

            Type = type;
            State = state;
            IsFixed = isFixed;
        }

        public Piece WithState(PieceState state) => new Piece(Type, state, IsFixed);

        public bool Equals(Piece other) => Type == other.Type && State == other.State && IsFixed == other.IsFixed;

        public override bool Equals(object obj) => obj is Piece other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Type, (int)State, IsFixed);

        public override string ToString() => $"{Type}:{State}{(IsFixed ? " (고정)" : "")}";
    }

    /// <summary>거울 종류별 허용 상태와 회전 순서.</summary>
    public static class PieceRules
    {
        public static bool IsValidState(PieceType type, PieceState state)
        {
            switch (type)
            {
                case PieceType.PlaneMirror:
                case PieceType.Splitter:
                    return state == PieceState.Slash || state == PieceState.Backslash;
                case PieceType.LiftMirror:
                    return state == PieceState.XRise || state == PieceState.ZRise ||
                           state == PieceState.XFall || state == PieceState.ZFall;
                default:
                    return false;
            }
        }

        /// <summary>인벤토리에서 처음 꺼내 놓을 때의 상태.</summary>
        public static PieceState InitialState(PieceType type)
        {
            switch (type)
            {
                case PieceType.PlaneMirror:
                case PieceType.Splitter:
                    return PieceState.Slash;
                case PieceType.LiftMirror:
                    return PieceState.XRise;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>처음 놓은 상태에서 이 상태가 되기까지 필요한 회전(좌클릭) 횟수.</summary>
        public static int RotationsTo(PieceType type, PieceState state)
        {
            if (!IsValidState(type, state))
            {
                throw new ArgumentException($"{type}에는 {state} 상태를 쓸 수 없다.", nameof(state));
            }

            var count = 0;
            for (var current = InitialState(type); current != state; current = NextState(type, current))
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// 좌클릭 한 번(위에서 볼 때 90° 회전) 뒤의 상태.
        /// 평면 거울·분할기: / → \ → /
        /// 승강 거울: XRise → ZRise → XFall → ZFall → XRise (y축 90° 회전과 일치)
        /// </summary>
        public static PieceState NextState(PieceType type, PieceState state)
        {
            if (!IsValidState(type, state))
            {
                throw new ArgumentException($"{type}에는 {state} 상태를 쓸 수 없다.", nameof(state));
            }

            switch (state)
            {
                case PieceState.Slash: return PieceState.Backslash;
                case PieceState.Backslash: return PieceState.Slash;
                case PieceState.XRise: return PieceState.ZRise;
                case PieceState.ZRise: return PieceState.XFall;
                case PieceState.XFall: return PieceState.ZFall;
                case PieceState.ZFall: return PieceState.XRise;
                default: throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}
