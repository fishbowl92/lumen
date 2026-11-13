using System;
using System.Collections.Generic;

namespace Lumen.Core
{
    /// <summary>칸의 내용 종류. 한 칸에는 하나만 들어간다.</summary>
    public enum CellKind
    {
        Empty,
        Wall,
        Source,
        Crystal,
        Piece
    }

    /// <summary>광원: 위치와 발사 방향.</summary>
    public readonly struct BeamSource
    {
        public readonly Int3 Position;
        public readonly Direction Direction;

        public BeamSource(Int3 position, Direction direction)
        {
            Position = position;
            Direction = direction;
        }
    }

    /// <summary>
    /// 레벨의 칸 상태. 광원·크리스털·벽·거울이 어디에 있는지만 안다.
    /// 배치 규칙(받침대, 인벤토리)은 LevelSession이 맡는다.
    /// </summary>
    public sealed class Grid
    {
        private readonly Dictionary<Int3, CellKind> _kinds = new Dictionary<Int3, CellKind>();
        private readonly Dictionary<Int3, Piece> _pieces = new Dictionary<Int3, Piece>();
        private readonly List<BeamSource> _sources = new List<BeamSource>();
        private readonly List<Int3> _crystals = new List<Int3>();

        public Grid(Int3 size)
        {
            if (size.X <= 0 || size.Y <= 0 || size.Z <= 0)
            {
                throw new ArgumentException($"격자 크기는 모두 1 이상이어야 한다: {size}", nameof(size));
            }

            Size = size;
        }

        public Int3 Size { get; }

        public IReadOnlyList<BeamSource> Sources => _sources;

        public IReadOnlyList<Int3> Crystals => _crystals;

        public bool InBounds(Int3 position) =>
            position.X >= 0 && position.X < Size.X &&
            position.Y >= 0 && position.Y < Size.Y &&
            position.Z >= 0 && position.Z < Size.Z;

        public CellKind GetKind(Int3 position)
        {
            RequireInBounds(position);
            return _kinds.TryGetValue(position, out var kind) ? kind : CellKind.Empty;
        }

        public bool TryGetPiece(Int3 position, out Piece piece) => _pieces.TryGetValue(position, out piece);

        public void AddWall(Int3 position) => Occupy(position, CellKind.Wall);

        public void AddCrystal(Int3 position)
        {
            Occupy(position, CellKind.Crystal);
            _crystals.Add(position);
        }

        public void AddSource(Int3 position, Direction direction)
        {
            Occupy(position, CellKind.Source);
            _sources.Add(new BeamSource(position, direction));
        }

        public void PlacePiece(Int3 position, Piece piece)
        {
            Occupy(position, CellKind.Piece);
            _pieces[position] = piece;
        }

        /// <summary>이미 놓인 거울의 상태를 바꾼다(회전).</summary>
        public void ReplacePiece(Int3 position, Piece piece)
        {
            if (!_pieces.ContainsKey(position))
            {
                throw new InvalidOperationException($"{position}에 거울이 없다.");
            }

            _pieces[position] = piece;
        }

        public bool RemovePiece(Int3 position)
        {
            if (!_pieces.Remove(position))
            {
                return false;
            }

            _kinds.Remove(position);
            return true;
        }

        private void Occupy(Int3 position, CellKind kind)
        {
            RequireInBounds(position);
            if (_kinds.ContainsKey(position))
            {
                throw new InvalidOperationException($"{position} 칸은 이미 {_kinds[position]}이(가) 차지하고 있다.");
            }

            _kinds[position] = kind;
        }

        private void RequireInBounds(Int3 position)
        {
            if (!InBounds(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, $"격자 {Size} 밖의 칸이다.");
            }
        }
    }
}
