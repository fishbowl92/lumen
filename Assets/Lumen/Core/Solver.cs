using System;
using System.Collections.Generic;
using System.Text;

namespace Lumen.Core
{
    /// <summary>받침대 하나에 놓은 거울.</summary>
    public readonly struct Placement
    {
        public readonly Int3 Position;
        public readonly PieceType Type;
        public readonly PieceState State;

        public Placement(Int3 position, PieceType type, PieceState state)
        {
            Position = position;
            Type = type;
            State = state;
        }

        public override string ToString() => $"{Position} {Solver.TypeText(Type)} {Solver.StateText(State)}";
    }

    /// <summary>해 하나: 모든 크리스털을 켜고, 놓은 거울이 모두 광선을 꺾거나 나누는 배치.</summary>
    public sealed class Solution
    {
        public Solution(List<Placement> placements, int floorChanges)
        {
            placements.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
            Placements = placements;
            FloorChanges = floorChanges;
            Key = string.Join(" | ", placements);
        }

        public IReadOnlyList<Placement> Placements { get; }

        /// <summary>배치 수 = 유효 배치 수(해에는 유효하지 않은 거울이 없다).</summary>
        public int Count => Placements.Count;

        /// <summary>이 해에서 광선이 위·아래로 움직인 직선 구간 수. 3D 체감 지표.</summary>
        public int FloorChanges { get; }

        public string Key { get; }

        public override string ToString() => Count == 0 ? "(거울 없이 풀림)" : Key;
    }

    /// <summary>솔버 결과와 난이도 지표(기획서 "난이도 지표").</summary>
    public sealed class SolverResult
    {
        public SolverResult(
            List<Solution> solutions,
            int nearMisses,
            long searchSpace,
            int nodes,
            bool hitNodeLimit,
            bool anyTruncated)
        {
            solutions.Sort((a, b) => a.Count != b.Count ? a.Count.CompareTo(b.Count) : string.CompareOrdinal(a.Key, b.Key));
            Solutions = solutions;
            NearMisses = nearMisses;
            SearchSpace = searchSpace;
            Nodes = nodes;
            HitNodeLimit = hitNodeLimit;
            AnyTruncated = anyTruncated;

            MinPlacements = solutions.Count > 0 ? solutions[0].Count : -1;
            foreach (var solution in solutions)
            {
                if (solution.Count == MinPlacements)
                {
                    MinSolutionCount++;
                    FloorChanges = Math.Max(FloorChanges, solution.FloorChanges);
                }
            }
        }

        /// <summary>해 목록. 배치 수가 적은 순.</summary>
        public IReadOnlyList<Solution> Solutions { get; }

        public bool Solvable => Solutions.Count > 0;

        /// <summary>최소 배치 수. 풀 수 없으면 -1. 레벨 JSON의 solverMinPlacements와 같아야 한다.</summary>
        public int MinPlacements { get; }

        /// <summary>해의 개수(11/13 결정: 유효 배치로만 이뤄진 서로 다른 배치의 수).</summary>
        public int SolutionCount => Solutions.Count;

        /// <summary>최소 배치 수로 푸는 해의 개수.</summary>
        public int MinSolutionCount { get; }

        /// <summary>최소 해의 층 전환 수(최소 해가 여럿이면 가장 큰 값).</summary>
        public int FloorChanges { get; }

        /// <summary>크리스털이 2개 이상일 때, 하나만 빼고 다 켜는 배치 수. "함정"의 강도.</summary>
        public int NearMisses { get; }

        /// <summary>받침대마다 비움 또는 거울×상태를 고르는 전체 조합 수(인벤토리 반영). 무작위로 풀릴 위험의 지표.</summary>
        public long SearchSpace { get; }

        /// <summary>실제로 광선을 계산한 탐색 노드 수.</summary>
        public int Nodes { get; }

        /// <summary>노드 상한에 걸려 탐색을 끝내지 못했는지. true면 결과를 믿을 수 없다.</summary>
        public bool HitNodeLimit { get; }

        /// <summary>탐색 중 광선 계산이 전진 횟수 상한에 걸린 배치가 있었는지.</summary>
        public bool AnyTruncated { get; }
    }

    /// <summary>
    /// 레벨 솔버(기획서 "솔버 방식"). 게임과 같은 BeamTracer를 쓴다.
    ///
    /// 탐색: 현재 광선이 지나가는 칸 중 아직 정하지 않은 첫 받침대를 골라
    /// "비워 둠" 또는 "남은 거울 × 상태"로 갈라 들어간다. 광선이 지나지 않는 받침대에 놓은 거울은
    /// 결과를 바꾸지 않으므로 고르지 않는다. 광선 위에 정할 받침대가 더 없으면 그 배치를 평가한다.
    ///
    /// 해: 모든 크리스털이 켜지고, 놓은 거울이 모두 광선을 꺾거나 나눈 배치.
    /// 이런 배치는 탐색에서 정확히 한 번씩 나온다(배치가 정해지면 갈림길마다 고를 값이 하나로 정해지므로).
    /// 쓸모없는 거울을 더 얹은 배치는 그 거울을 빼도 여전히 풀리므로(광선은 더 멀리 갈 뿐이다) 따로 세지 않는다.
    /// </summary>
    public static class Solver
    {
        public const int DefaultNodeLimit = 2_000_000;

        private static readonly PieceType[] Types = { PieceType.PlaneMirror, PieceType.LiftMirror, PieceType.Splitter };

        private static readonly PieceState[] PlaneStates = { PieceState.Slash, PieceState.Backslash };

        private static readonly PieceState[] LiftStates =
            { PieceState.XRise, PieceState.ZRise, PieceState.XFall, PieceState.ZFall };

        public static PieceState[] StatesOf(PieceType type) => type == PieceType.LiftMirror ? LiftStates : PlaneStates;

        public static SolverResult Solve(LevelDefinition definition, int nodeLimit = DefaultNodeLimit)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var search = new Search(definition, nodeLimit);
            search.Run();
            return new SolverResult(
                search.Solutions,
                search.NearMisses,
                CountSearchSpace(definition),
                search.Nodes,
                search.HitNodeLimit,
                search.AnyTruncated);
        }

        /// <summary>
        /// 받침대마다 비움 또는 거울×상태를 고르는 조합 수. 인벤토리 수를 넘는 배치는 세지 않는다.
        /// (평면 a개, 승강 b개, 분할기 c개를 쓴 경우의 수를 받침대 하나씩 늘려 가며 센다.)
        /// </summary>
        public static long CountSearchSpace(LevelDefinition definition)
        {
            var maxPlane = definition.InventoryOf(PieceType.PlaneMirror);
            var maxLift = definition.InventoryOf(PieceType.LiftMirror);
            var maxSplitter = definition.InventoryOf(PieceType.Splitter);

            var ways = new Dictionary<(int, int, int), long> { { (0, 0, 0), 1 } };
            for (var i = 0; i < definition.Pedestals.Count; i++)
            {
                var next = new Dictionary<(int, int, int), long>();
                foreach (var pair in ways)
                {
                    var (a, b, c) = pair.Key;
                    Add(next, (a, b, c), pair.Value);
                    if (a < maxPlane) Add(next, (a + 1, b, c), pair.Value * PlaneStates.Length);
                    if (b < maxLift) Add(next, (a, b + 1, c), pair.Value * LiftStates.Length);
                    if (c < maxSplitter) Add(next, (a, b, c + 1), pair.Value * PlaneStates.Length);
                }

                ways = next;
            }

            long total = 0;
            foreach (var value in ways.Values)
            {
                total += value;
            }

            return total;
        }

        private static void Add(Dictionary<(int, int, int), long> map, (int, int, int) key, long value)
        {
            map.TryGetValue(key, out var current);
            map[key] = current + value;
        }

        public static string TypeText(PieceType type)
        {
            switch (type)
            {
                case PieceType.PlaneMirror: return "Plane";
                case PieceType.LiftMirror: return "Lift";
                default: return "Splitter";
            }
        }

        public static string StateText(PieceState state)
        {
            switch (state)
            {
                case PieceState.Slash: return "/";
                case PieceState.Backslash: return "\\";
                default: return state.ToString();
            }
        }

        /// <summary>결과를 사람이 읽을 수 있게 요약한다(에디터 창·로그용).</summary>
        public static string Describe(LevelDefinition definition, SolverResult result, int maxSolutions = 10)
        {
            var text = new StringBuilder();
            text.Append($"{definition.Id}: ");
            text.Append(result.Solvable
                ? $"min {result.MinPlacements} (json {definition.SolverMinPlacements}), solutions {result.SolutionCount}"
                : "UNSOLVABLE");
            text.Append($", floor changes {result.FloorChanges}, near misses {result.NearMisses}");
            text.Append($", search space {result.SearchSpace}, nodes {result.Nodes}");
            if (result.HitNodeLimit)
            {
                text.Append(", NODE LIMIT HIT");
            }

            for (var i = 0; i < result.Solutions.Count && i < maxSolutions; i++)
            {
                text.Append($"\n  [{result.Solutions[i].Count}] {result.Solutions[i]}");
            }

            if (result.Solutions.Count > maxSolutions)
            {
                text.Append($"\n  ... and {result.Solutions.Count - maxSolutions} more");
            }

            return text.ToString();
        }

        private sealed class Search
        {
            private readonly LevelDefinition _definition;
            private readonly Grid _grid;
            private readonly int _nodeLimit;
            private readonly HashSet<Int3> _decided = new HashSet<Int3>();
            private readonly List<Placement> _placed = new List<Placement>();
            private readonly Dictionary<PieceType, int> _remaining = new Dictionary<PieceType, int>();

            public Search(LevelDefinition definition, int nodeLimit)
            {
                _definition = definition;
                _grid = definition.CreateGrid();
                _nodeLimit = nodeLimit;
                foreach (var type in Types)
                {
                    _remaining[type] = definition.InventoryOf(type);
                }
            }

            public List<Solution> Solutions { get; } = new List<Solution>();

            public int NearMisses { get; private set; }

            public int Nodes { get; private set; }

            public bool HitNodeLimit { get; private set; }

            public bool AnyTruncated { get; private set; }

            public void Run() => Visit();

            private void Visit()
            {
                if (HitNodeLimit)
                {
                    return;
                }

                if (++Nodes > _nodeLimit)
                {
                    HitNodeLimit = true;
                    return;
                }

                var trace = BeamTracer.Trace(_grid);
                AnyTruncated |= trace.Truncated;

                if (!TryFindOpenPedestal(trace, out var cell))
                {
                    Evaluate(trace);
                    return;
                }

                _decided.Add(cell);

                Visit(); // 비워 둔다

                foreach (var type in Types)
                {
                    if (_remaining[type] == 0)
                    {
                        continue;
                    }

                    foreach (var state in StatesOf(type))
                    {
                        _grid.PlacePiece(cell, new Piece(type, state, false));
                        _remaining[type]--;
                        _placed.Add(new Placement(cell, type, state));

                        Visit();

                        _placed.RemoveAt(_placed.Count - 1);
                        _remaining[type]++;
                        _grid.RemovePiece(cell);
                    }
                }

                _decided.Remove(cell);
            }

            /// <summary>광선이 지나가는 칸 중 아직 정하지 않은 첫 받침대(광선 계산 순서 기준).</summary>
            private bool TryFindOpenPedestal(TraceResult trace, out Int3 cell)
            {
                foreach (var segment in trace.Segments)
                {
                    var offset = segment.Direction.ToOffset();
                    var position = segment.From;
                    while (position != segment.To)
                    {
                        position += offset;
                        if (!_grid.InBounds(position))
                        {
                            break;
                        }

                        if (_definition.IsPedestal(position) && !_decided.Contains(position) &&
                            _grid.GetKind(position) == CellKind.Empty)
                        {
                            cell = position;
                            return true;
                        }
                    }
                }

                cell = default;
                return false;
            }

            private void Evaluate(TraceResult trace)
            {
                foreach (var placement in _placed)
                {
                    if (!trace.IsDeflecting(placement.Position))
                    {
                        return; // 쓸모없는 거울이 있는 배치는 그 거울을 뺀 배치로 이미 센다
                    }
                }

                if (trace.AllCrystalsLit)
                {
                    var floorChanges = 0;
                    foreach (var segment in trace.Segments)
                    {
                        if (segment.Direction.IsVertical())
                        {
                            floorChanges++;
                        }
                    }

                    Solutions.Add(new Solution(new List<Placement>(_placed), floorChanges));
                    return;
                }

                var crystals = _grid.Crystals.Count;
                if (crystals >= 2 && trace.LitCrystals.Count == crystals - 1)
                {
                    NearMisses++;
                }
            }
        }
    }
}
