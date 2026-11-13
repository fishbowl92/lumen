using System.Collections.Generic;

namespace Lumen.Core
{
    /// <summary>
    /// 한 레벨을 플레이하는 동안의 상태: 배치·회전·회수·되돌리기·리셋과 승리·별 판정.
    /// 화면(Game 어셈블리)은 이 클래스만 조작하고, 바뀔 때마다 LastTrace를 다시 그린다.
    /// </summary>
    public sealed class LevelSession
    {
        private readonly Dictionary<PieceType, int> _remaining = new Dictionary<PieceType, int>();
        private readonly HashSet<Int3> _placed = new HashSet<Int3>();
        private readonly Stack<HistoryEntry> _history = new Stack<HistoryEntry>();

        public LevelSession(LevelDefinition definition)
        {
            Definition = definition;
            Reset();
        }

        public LevelDefinition Definition { get; }

        public Grid Grid { get; private set; }

        /// <summary>마지막 변경 뒤의 광선 계산 결과.</summary>
        public TraceResult LastTrace { get; private set; }

        /// <summary>클리어하면 true가 되고 입력이 잠긴다. Reset으로 풀린다.</summary>
        public bool IsCleared { get; private set; }

        /// <summary>플레이어가 놓은 배치형 거울 수.</summary>
        public int PlacedCount => _placed.Count;

        /// <summary>놓은 거울 중 광선의 방향을 바꾸거나 나눈 것의 수. 별 계산에 쓴다.</summary>
        public int EffectivePlacedCount
        {
            get
            {
                var count = 0;
                foreach (var position in _placed)
                {
                    if (LastTrace.IsDeflecting(position))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>클리어 전에는 0, 클리어 뒤에는 1~3.</summary>
        public int Stars => IsCleared ? ScoreStars(EffectivePlacedCount, Definition.SolverMinPlacements) : 0;

        public bool CanUndo => !IsCleared && _history.Count > 0;

        public int Remaining(PieceType type) => _remaining.TryGetValue(type, out var count) ? count : 0;

        public bool IsPlayerPiece(Int3 position) => _placed.Contains(position);

        /// <summary>
        /// 별 규칙(11/13 결정): 유효 배치 수가 최소 이하면 3개, 최소보다 1~2개 많으면 2개, 3개 이상 많으면 1개.
        /// </summary>
        public static int ScoreStars(int effectivePlacements, int solverMinPlacements)
        {
            var extra = effectivePlacements - solverMinPlacements;
            if (extra <= 0)
            {
                return 3;
            }

            return extra <= 2 ? 2 : 1;
        }

        public bool CanPlace(Int3 position, PieceType type) =>
            !IsCleared &&
            Definition.IsPedestal(position) &&
            Grid.GetKind(position) == CellKind.Empty &&
            Remaining(type) > 0;

        /// <summary>받침대 칸에 인벤토리의 거울을 처음 상태로 놓는다.</summary>
        public bool TryPlace(Int3 position, PieceType type)
        {
            if (!CanPlace(position, type))
            {
                return false;
            }

            Place(position, new Piece(type, PieceRules.InitialState(type), false));
            _history.Push(HistoryEntry.Placed(position));
            Refresh();
            return true;
        }

        /// <summary>플레이어가 놓은 거울을 위에서 볼 때 90° 돌린다. 고정 거울은 거부.</summary>
        public bool TryRotate(Int3 position)
        {
            if (IsCleared || !_placed.Contains(position) || !Grid.TryGetPiece(position, out var piece))
            {
                return false;
            }

            Grid.ReplacePiece(position, piece.WithState(PieceRules.NextState(piece.Type, piece.State)));
            _history.Push(HistoryEntry.Rotated(position, piece.State));
            Refresh();
            return true;
        }

        /// <summary>플레이어가 놓은 거울을 인벤토리로 회수한다. 고정 거울은 거부.</summary>
        public bool TryRemove(Int3 position)
        {
            if (IsCleared || !_placed.Contains(position) || !Grid.TryGetPiece(position, out var piece))
            {
                return false;
            }

            Remove(position, piece.Type);
            _history.Push(HistoryEntry.Removed(position, piece));
            Refresh();
            return true;
        }

        /// <summary>마지막 배치·회전·회수를 하나 취소한다.</summary>
        public bool Undo()
        {
            if (!CanUndo)
            {
                return false;
            }

            var entry = _history.Pop();
            switch (entry.Kind)
            {
                case ActionKind.Placed:
                    Grid.TryGetPiece(entry.Position, out var placed);
                    Remove(entry.Position, placed.Type);
                    break;
                case ActionKind.Rotated:
                    Grid.TryGetPiece(entry.Position, out var rotated);
                    Grid.ReplacePiece(entry.Position, rotated.WithState(entry.PreviousState));
                    break;
                case ActionKind.Removed:
                    Place(entry.Position, entry.Piece);
                    break;
            }

            Refresh();
            return true;
        }

        /// <summary>레벨 시작 상태로 되돌린다. 클리어 잠금도 풀린다.</summary>
        public void Reset()
        {
            Grid = Definition.CreateGrid();
            _placed.Clear();
            _history.Clear();
            _remaining.Clear();
            foreach (PieceType type in System.Enum.GetValues(typeof(PieceType)))
            {
                _remaining[type] = Definition.InventoryOf(type);
            }

            IsCleared = false;
            Refresh();
        }

        private void Place(Int3 position, Piece piece)
        {
            Grid.PlacePiece(position, piece);
            _placed.Add(position);
            _remaining[piece.Type]--;
        }

        private void Remove(Int3 position, PieceType type)
        {
            Grid.RemovePiece(position);
            _placed.Remove(position);
            _remaining[type]++;
        }

        private void Refresh()
        {
            LastTrace = BeamTracer.Trace(Grid);
            if (LastTrace.AllCrystalsLit)
            {
                IsCleared = true; // 클리어 순간 입력을 잠근다(기획서 엣지 케이스 표).
            }
        }

        private enum ActionKind
        {
            Placed,
            Rotated,
            Removed
        }

        private readonly struct HistoryEntry
        {
            public readonly ActionKind Kind;
            public readonly Int3 Position;
            public readonly PieceState PreviousState;
            public readonly Piece Piece;

            private HistoryEntry(ActionKind kind, Int3 position, PieceState previousState, Piece piece)
            {
                Kind = kind;
                Position = position;
                PreviousState = previousState;
                Piece = piece;
            }

            public static HistoryEntry Placed(Int3 position) => new HistoryEntry(ActionKind.Placed, position, default, default);

            public static HistoryEntry Rotated(Int3 position, PieceState previous) =>
                new HistoryEntry(ActionKind.Rotated, position, previous, default);

            public static HistoryEntry Removed(Int3 position, Piece piece) =>
                new HistoryEntry(ActionKind.Removed, position, default, piece);
        }
    }
}
