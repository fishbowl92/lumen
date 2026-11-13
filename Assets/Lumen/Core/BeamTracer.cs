using System.Collections.Generic;

namespace Lumen.Core
{
    /// <summary>직선 광선 구간이 끝난 이유.</summary>
    public enum SegmentEnd
    {
        Turned,      // 거울에서 꺾임
        Split,       // 분할기에서 나뉨
        Blocked,     // 벽·광원·수직 광선을 막는 거울
        Absorbed,    // 크리스털에 흡수
        OutOfBounds, // 격자 밖으로 나감 (To는 격자 밖 첫 칸)
        Looped,      // 이미 지나간 (칸, 방향)에 다시 도착
        Truncated    // 전진 횟수 상한 도달
    }

    /// <summary>렌더링용 직선 광선 구간. From에서 To까지 Direction 방향으로 곧게 뻗는다.</summary>
    public readonly struct BeamSegment
    {
        public readonly Int3 From;
        public readonly Int3 To;
        public readonly Direction Direction;
        public readonly SegmentEnd End;

        public BeamSegment(Int3 from, Int3 to, Direction direction, SegmentEnd end)
        {
            From = from;
            To = to;
            Direction = direction;
            End = end;
        }

        public override string ToString() => $"{From}->{To} {Direction} [{End}]";
    }

    /// <summary>광선 계산 결과.</summary>
    public sealed class TraceResult
    {
        private readonly HashSet<Int3> _lit;
        private readonly HashSet<Int3> _deflecting;

        public TraceResult(
            HashSet<Int3> litCrystals,
            List<BeamSegment> segments,
            HashSet<Int3> deflectingPieces,
            int steps,
            bool truncated,
            bool allCrystalsLit)
        {
            _lit = litCrystals;
            _deflecting = deflectingPieces;
            LitCrystals = litCrystals;
            Segments = segments;
            DeflectingPieces = deflectingPieces;
            Steps = steps;
            Truncated = truncated;
            AllCrystalsLit = allCrystalsLit;
        }

        /// <summary>점등된 크리스털 위치.</summary>
        public IReadOnlyCollection<Int3> LitCrystals { get; }

        public IReadOnlyList<BeamSegment> Segments { get; }

        /// <summary>
        /// 광선의 방향을 바꾸거나 광선을 나눈 거울의 위치(고정형 포함).
        /// 유효 배치 수와 해의 개수는 이 집합으로 계산한다.
        /// </summary>
        public IReadOnlyCollection<Int3> DeflectingPieces { get; }

        /// <summary>전체 전진 횟수(모든 가지 합).</summary>
        public int Steps { get; }

        /// <summary>전진 횟수 상한(BeamTracer.MaxSteps)에 걸려 계산을 멈췄는지.</summary>
        public bool Truncated { get; }

        /// <summary>크리스털이 하나 이상 있고 모두 점등됐는지 = 승리 조건.</summary>
        public bool AllCrystalsLit { get; }

        public bool IsLit(Int3 crystal) => _lit.Contains(crystal);

        /// <summary>이 칸의 거울이 광선의 방향을 바꾸거나 나눴는지.</summary>
        public bool IsDeflecting(Int3 piece) => _deflecting.Contains(piece);
    }

    /// <summary>
    /// 광선 계산기. 규칙(기획서 규칙 명세 · 구현 지시서 2장):
    /// 1. 모든 광원에서 (칸, 방향)으로 시작한다.
    /// 2. 한 칸 전진한다. 격자 밖이면 그 가지는 소멸한다.
    /// 3. 도착한 (칸, 방향)을 이미 방문했으면 그 가지는 멈춘다(모든 가지가 방문 기록을 공유).
    /// 4. 도착 칸의 내용대로 처리한다.
    /// 5. 전체 전진 횟수가 MaxSteps에 이르면 강제로 멈춘다.
    /// </summary>
    public static class BeamTracer
    {
        public const int MaxSteps = 512;

        public static TraceResult Trace(Grid grid)
        {
            var lit = new HashSet<Int3>();
            var deflecting = new HashSet<Int3>();
            var visited = new HashSet<(Int3, Direction)>();
            var segments = new List<BeamSegment>();
            var pending = new Stack<(Int3 Start, Direction Direction)>();

            // 먼저 넣은 광원이 먼저 계산되도록 거꾸로 쌓는다(결과 집합은 순서와 무관).
            for (var i = grid.Sources.Count - 1; i >= 0; i--)
            {
                pending.Push((grid.Sources[i].Position, grid.Sources[i].Direction));
            }

            var steps = 0;
            var truncated = false;

            while (pending.Count > 0 && !truncated)
            {
                var branch = pending.Pop();
                var position = branch.Start;
                var direction = branch.Direction;
                var segmentStart = position;
                var alive = true;

                while (alive)
                {
                    if (steps >= MaxSteps)
                    {
                        truncated = true;
                        segments.Add(new BeamSegment(segmentStart, position, direction, SegmentEnd.Truncated));
                        break;
                    }

                    var next = position + direction.ToOffset();
                    steps++;

                    if (!grid.InBounds(next))
                    {
                        segments.Add(new BeamSegment(segmentStart, next, direction, SegmentEnd.OutOfBounds));
                        break;
                    }

                    if (!visited.Add((next, direction)))
                    {
                        segments.Add(new BeamSegment(segmentStart, next, direction, SegmentEnd.Looped));
                        break;
                    }

                    switch (grid.GetKind(next))
                    {
                        case CellKind.Empty:
                            position = next;
                            break;

                        case CellKind.Wall:
                        case CellKind.Source:
                            segments.Add(new BeamSegment(segmentStart, next, direction, SegmentEnd.Blocked));
                            alive = false;
                            break;

                        case CellKind.Crystal:
                            lit.Add(next);
                            segments.Add(new BeamSegment(segmentStart, next, direction, SegmentEnd.Absorbed));
                            alive = false;
                            break;

                        case CellKind.Piece:
                            grid.TryGetPiece(next, out var piece);
                            alive = ApplyPiece(piece, next, ref direction, ref segmentStart, segments, deflecting, pending);
                            position = next;
                            break;
                    }
                }
            }

            var allLit = grid.Crystals.Count > 0 && lit.Count == grid.Crystals.Count;
            return new TraceResult(lit, segments, deflecting, steps, truncated, allLit);
        }

        /// <summary>거울 칸에 도착한 광선을 처리한다. 광선이 계속 나아가면 true.</summary>
        private static bool ApplyPiece(
            Piece piece,
            Int3 at,
            ref Direction direction,
            ref Int3 segmentStart,
            List<BeamSegment> segments,
            HashSet<Int3> deflecting,
            Stack<(Int3 Start, Direction Direction)> pending)
        {
            switch (piece.Type)
            {
                case PieceType.PlaneMirror:
                    if (direction.IsVertical())
                    {
                        segments.Add(new BeamSegment(segmentStart, at, direction, SegmentEnd.Blocked));
                        return false;
                    }

                    segments.Add(new BeamSegment(segmentStart, at, direction, SegmentEnd.Turned));
                    direction = Reflection.ReflectPlane(piece.State, direction);
                    segmentStart = at;
                    deflecting.Add(at);
                    return true;

                case PieceType.LiftMirror:
                {
                    var outgoing = Reflection.ReflectLift(piece.State, direction);
                    if (outgoing == direction)
                    {
                        // 면과 나란한 광선: 빈칸처럼 통과. 구간도 끊지 않는다.
                        return true;
                    }

                    segments.Add(new BeamSegment(segmentStart, at, direction, SegmentEnd.Turned));
                    direction = outgoing;
                    segmentStart = at;
                    deflecting.Add(at);
                    return true;
                }

                case PieceType.Splitter:
                    if (direction.IsVertical())
                    {
                        segments.Add(new BeamSegment(segmentStart, at, direction, SegmentEnd.Blocked));
                        return false;
                    }

                    // 꺾인 광선은 새 가지로 쌓고, 지금 광선은 직진을 이어 간다.
                    segments.Add(new BeamSegment(segmentStart, at, direction, SegmentEnd.Split));
                    pending.Push((at, Reflection.ReflectPlane(piece.State, direction)));
                    segmentStart = at;
                    deflecting.Add(at);
                    return true;

                default:
                    segments.Add(new BeamSegment(segmentStart, at, direction, SegmentEnd.Blocked));
                    return false;
            }
        }
    }
}
