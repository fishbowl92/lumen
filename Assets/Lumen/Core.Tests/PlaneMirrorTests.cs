using NUnit.Framework;
using static Lumen.Core.Tests.TestGrids;

namespace Lumen.Core.Tests
{
    /// <summary>T1·T2: 평면 거울 반사 표와 수직 광선 차단.</summary>
    [TestFixture]
    public class PlaneMirrorTests
    {
        [TestCase(PieceState.Slash, Direction.East, Direction.North)]
        [TestCase(PieceState.Slash, Direction.West, Direction.South)]
        [TestCase(PieceState.Slash, Direction.North, Direction.East)]
        [TestCase(PieceState.Slash, Direction.South, Direction.West)]
        [TestCase(PieceState.Backslash, Direction.East, Direction.South)]
        [TestCase(PieceState.Backslash, Direction.West, Direction.North)]
        [TestCase(PieceState.Backslash, Direction.North, Direction.West)]
        [TestCase(PieceState.Backslash, Direction.South, Direction.East)]
        public void T1_ReflectionTable(PieceState state, Direction incoming, Direction expected)
        {
            Assert.AreEqual(expected, Reflection.ReflectPlane(state, incoming));
        }

        [TestCase(PieceState.Slash, Direction.East, Direction.North)]
        [TestCase(PieceState.Slash, Direction.West, Direction.South)]
        [TestCase(PieceState.Slash, Direction.North, Direction.East)]
        [TestCase(PieceState.Slash, Direction.South, Direction.West)]
        [TestCase(PieceState.Backslash, Direction.East, Direction.South)]
        [TestCase(PieceState.Backslash, Direction.West, Direction.North)]
        [TestCase(PieceState.Backslash, Direction.North, Direction.West)]
        [TestCase(PieceState.Backslash, Direction.South, Direction.East)]
        public void T1_TracerFollowsReflection(PieceState state, Direction incoming, Direction expected)
        {
            var grid = CenterMirror(Plane(state), incoming, expected);

            var result = BeamTracer.Trace(grid);

            Assert.IsTrue(result.AllCrystalsLit, "반사된 광선이 출구 크리스털에 닿아야 한다.");
            Assert.IsTrue(result.IsDeflecting(P(1, 1, 1)), "꺾은 거울은 유효 배치로 기록된다.");
        }

        [TestCase(PieceState.Slash, Direction.Up)]
        [TestCase(PieceState.Slash, Direction.Down)]
        [TestCase(PieceState.Backslash, Direction.Up)]
        [TestCase(PieceState.Backslash, Direction.Down)]
        public void T2_VerticalBeamIsBlocked(PieceState state, Direction incoming)
        {
            // 직진했다면 닿았을 자리에 크리스털을 둔다.
            var grid = CenterMirror(Plane(state), incoming, incoming);

            var result = BeamTracer.Trace(grid);

            Assert.AreEqual(0, result.LitCrystals.Count);
            Assert.AreEqual(SegmentEnd.Blocked, result.Segments[result.Segments.Count - 1].End);
            Assert.IsFalse(result.IsDeflecting(P(1, 1, 1)), "막기만 한 거울은 유효 배치가 아니다.");
        }

        [Test]
        public void T2_ReflectPlaneRejectsVertical()
        {
            Assert.Throws<System.ArgumentException>(() => Reflection.ReflectPlane(PieceState.Slash, Direction.Up));
        }
    }
}
