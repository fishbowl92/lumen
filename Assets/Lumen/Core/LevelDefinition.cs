using System;
using System.Collections.Generic;

namespace Lumen.Core
{
    /// <summary>
    /// 검증을 마친 레벨 정의. LevelData(JSON)를 FromData로 바꿔 만든다.
    /// 게임 진행 상태는 담지 않는다(LevelSession이 맡는다).
    /// </summary>
    public sealed class LevelDefinition
    {
        private readonly List<BeamSource> _sources;
        private readonly List<Int3> _crystals;
        private readonly List<Int3> _walls;
        private readonly List<(Int3 Position, Piece Piece)> _fixedPieces;
        private readonly HashSet<Int3> _pedestals;
        private readonly Dictionary<PieceType, int> _inventory;

        private LevelDefinition(
            string id,
            string name,
            Int3 size,
            List<BeamSource> sources,
            List<Int3> crystals,
            List<Int3> walls,
            List<(Int3, Piece)> fixedPieces,
            HashSet<Int3> pedestals,
            Dictionary<PieceType, int> inventory,
            int solverMinPlacements,
            int solverMinActions)
        {
            Id = id;
            Name = name;
            Size = size;
            _sources = sources;
            _crystals = crystals;
            _walls = walls;
            _fixedPieces = fixedPieces;
            _pedestals = pedestals;
            _inventory = inventory;
            SolverMinPlacements = solverMinPlacements;
            SolverMinActions = solverMinActions;
        }

        public string Id { get; }

        public string Name { get; }

        public Int3 Size { get; }

        public IReadOnlyList<BeamSource> Sources => _sources;

        public IReadOnlyList<Int3> Crystals => _crystals;

        public IReadOnlyList<Int3> Walls => _walls;

        public IReadOnlyList<(Int3 Position, Piece Piece)> FixedPieces => _fixedPieces;

        public IReadOnlyCollection<Int3> Pedestals => _pedestals;

        /// <summary>솔버가 계산한 최소 유효 배치 수(난이도 지표).</summary>
        public int SolverMinPlacements { get; }

        /// <summary>솔버가 계산한 최소 조작 수(Par). 별 계산의 기준.</summary>
        public int SolverMinActions { get; }

        public bool IsPedestal(Int3 position) => _pedestals.Contains(position);

        public int InventoryOf(PieceType type) => _inventory.TryGetValue(type, out var count) ? count : 0;

        /// <summary>시작 상태의 격자(광원·크리스털·벽·고정 거울)를 새로 만든다.</summary>
        public Grid CreateGrid()
        {
            var grid = new Grid(Size);
            foreach (var source in _sources)
            {
                grid.AddSource(source.Position, source.Direction);
            }

            foreach (var crystal in _crystals)
            {
                grid.AddCrystal(crystal);
            }

            foreach (var wall in _walls)
            {
                grid.AddWall(wall);
            }

            foreach (var fixedPiece in _fixedPieces)
            {
                grid.PlacePiece(fixedPiece.Position, fixedPiece.Piece);
            }

            return grid;
        }

        /// <summary>JSON에서 읽은 데이터를 검증해 레벨 정의로 바꾼다. 잘못된 값이면 FormatException.</summary>
        public static LevelDefinition FromData(LevelData data)
        {
            if (data == null)
            {
                throw new FormatException("레벨 데이터가 비어 있다.");
            }

            var id = string.IsNullOrEmpty(data.id) ? "unnamed" : data.id;
            var size = new Int3(data.sizeX, data.sizeY, data.sizeZ);
            if (size.X <= 0 || size.Y <= 0 || size.Z <= 0)
            {
                throw new FormatException($"{id}: 격자 크기는 모두 1 이상이어야 한다: {size}");
            }

            var sources = new List<BeamSource>();
            foreach (var s in data.sources ?? Array.Empty<SourceData>())
            {
                sources.Add(new BeamSource(new Int3(s.x, s.y, s.z), ParseDirection(s.dir, id)));
            }

            var crystals = ToPositions(data.crystals);
            var walls = ToPositions(data.walls);

            var fixedPieces = new List<(Int3, Piece)>();
            foreach (var p in data.fixedPieces ?? Array.Empty<PieceData>())
            {
                var type = ParsePieceType(p.type, id);
                var state = ParseState(p.state, id);
                if (!PieceRules.IsValidState(type, state))
                {
                    throw new FormatException($"{id}: {type}에는 {state} 상태를 쓸 수 없다.");
                }

                fixedPieces.Add((new Int3(p.x, p.y, p.z), new Piece(type, state, true)));
            }

            var pedestals = new HashSet<Int3>(ToPositions(data.pedestals));

            var inventoryData = data.inventory ?? new InventoryData();
            if (inventoryData.plane < 0 || inventoryData.lift < 0 || inventoryData.splitter < 0)
            {
                throw new FormatException($"{id}: 인벤토리 수는 음수일 수 없다.");
            }

            var inventory = new Dictionary<PieceType, int>
            {
                { PieceType.PlaneMirror, inventoryData.plane },
                { PieceType.LiftMirror, inventoryData.lift },
                { PieceType.Splitter, inventoryData.splitter }
            };

            if (sources.Count == 0)
            {
                throw new FormatException($"{id}: 광원이 하나 이상 있어야 한다.");
            }

            if (crystals.Count == 0)
            {
                throw new FormatException($"{id}: 크리스털이 하나 이상 있어야 한다.");
            }

            var definition = new LevelDefinition(
                id, data.name ?? id, size, sources, crystals, walls, fixedPieces, pedestals, inventory,
                data.solverMinPlacements, data.solverMinActions);

            // 칸 겹침·격자 밖 좌표를 Grid 규칙으로 검사한다.
            Grid grid;
            try
            {
                grid = definition.CreateGrid();
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentOutOfRangeException)
            {
                throw new FormatException($"{id}: {e.Message}", e);
            }

            foreach (var pedestal in pedestals)
            {
                if (!grid.InBounds(pedestal))
                {
                    throw new FormatException($"{id}: 받침대 {pedestal}가 격자 밖이다.");
                }

                if (grid.GetKind(pedestal) != CellKind.Empty)
                {
                    throw new FormatException($"{id}: 받침대 {pedestal} 칸이 비어 있지 않다.");
                }
            }

            return definition;
        }

        public static Direction ParseDirection(string text, string levelId = "")
        {
            switch (text)
            {
                case "East": return Direction.East;
                case "West": return Direction.West;
                case "North": return Direction.North;
                case "South": return Direction.South;
                case "Up": return Direction.Up;
                case "Down": return Direction.Down;
                default: throw new FormatException($"{levelId}: 알 수 없는 방향 '{text}'");
            }
        }

        public static PieceType ParsePieceType(string text, string levelId = "")
        {
            switch (text)
            {
                case "Plane": return PieceType.PlaneMirror;
                case "Lift": return PieceType.LiftMirror;
                case "Splitter": return PieceType.Splitter;
                default: throw new FormatException($"{levelId}: 알 수 없는 거울 종류 '{text}'");
            }
        }

        public static PieceState ParseState(string text, string levelId = "")
        {
            switch (text)
            {
                case "/": return PieceState.Slash;
                case "\\": return PieceState.Backslash;
                case "XRise": return PieceState.XRise;
                case "ZRise": return PieceState.ZRise;
                case "XFall": return PieceState.XFall;
                case "ZFall": return PieceState.ZFall;
                default: throw new FormatException($"{levelId}: 알 수 없는 거울 상태 '{text}'");
            }
        }

        private static List<Int3> ToPositions(CellData[] cells)
        {
            var list = new List<Int3>();
            foreach (var c in cells ?? Array.Empty<CellData>())
            {
                list.Add(new Int3(c.x, c.y, c.z));
            }

            return list;
        }
    }
}
