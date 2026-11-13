using NUnit.Framework;
using static Lumen.Core.Tests.TestGrids;

namespace Lumen.Core.Tests
{
    /// <summary>T7·T8: 분할기, T9: 무한 루프 방지.</summary>
    [TestFixture]
    public class SplitterAndLoopTests
    {
        [Test]
        public void T7_SplitterLightsBothCrystals()
        {
            // 광원 (0,0,1) 동쪽 → 분할기 '/' (1,0,1): 꺾인 광선은 북쪽 (1,0,2), 직진 광선은 (2,0,1)
            var grid = new Grid(P(3, 1, 3));
            grid.AddSource(P(0, 0, 1), Direction.East);
            grid.PlacePiece(P(1, 0, 1), Splitter(PieceState.Slash));
            grid.AddCrystal(P(1, 0, 2));
            grid.AddCrystal(P(2, 0, 1));

            var result = BeamTracer.Trace(grid);

            Assert.IsTrue(result.IsLit(P(1, 0, 2)), "꺾인 광선");
            Assert.IsTrue(result.IsLit(P(2, 0, 1)), "직진 광선");
            Assert.IsTrue(result.AllCrystalsLit);
            Assert.IsTrue(result.IsDeflecting(P(1, 0, 1)), "나눈 분할기는 유효 배치다.");
        }

        [TestCase(PieceState.Slash, Direction.Up)]
        [TestCase(PieceState.Slash, Direction.Down)]
        [TestCase(PieceState.Backslash, Direction.Up)]
        [TestCase(PieceState.Backslash, Direction.Down)]
        public void T8_SplitterBlocksVerticalBeam(PieceState state, Direction incoming)
        {
            var result = BeamTracer.Trace(CenterMirror(Splitter(state), incoming, incoming));

            Assert.AreEqual(0, result.LitCrystals.Count);
            Assert.AreEqual(SegmentEnd.Blocked, result.Segments[result.Segments.Count - 1].End);
            Assert.IsFalse(result.IsDeflecting(P(1, 1, 1)));
        }

        [Test]
        public void T9_LoopStopsByVisitedRecord()
        {
            // 방문 기록이 없으면 분할기 → 평면 거울 3개 → 분할기를 끝없이 도는 배치(참조 구현으로 확인).
            var grid = new Grid(P(3, 1, 3));
            grid.AddSource(P(0, 0, 0), Direction.East);
            grid.PlacePiece(P(1, 0, 0), Splitter(PieceState.Backslash));
            grid.PlacePiece(P(1, 0, 1), Plane(PieceState.Slash));
            grid.PlacePiece(P(2, 0, 0), Plane(PieceState.Slash));
            grid.PlacePiece(P(2, 0, 1), Plane(PieceState.Backslash));

            var result = BeamTracer.Trace(grid);

            Assert.IsFalse(result.Truncated, "상한이 아니라 방문 기록으로 멈춰야 한다.");
            Assert.Less(result.Steps, BeamTracer.MaxSteps);
            var looped = false;
            foreach (var segment in result.Segments)
            {
                looped |= segment.End == SegmentEnd.Looped;
            }

            Assert.IsTrue(looped, "이미 지나간 (칸, 방향)에 도착한 구간이 있어야 한다.");
        }

        [Test]
        public void MaxStepsTruncatesLongBeams()
        {
            // 전진 상한 동작 확인: 반사 없이 긴 직선 격자(600칸)
            var grid = new Grid(P(600, 1, 1));
            grid.AddSource(P(0, 0, 0), Direction.East);

            var result = BeamTracer.Trace(grid);

            Assert.IsTrue(result.Truncated);
            Assert.AreEqual(BeamTracer.MaxSteps, result.Steps);
        }
    }
}
