using NUnit.Framework;
using static Lumen.Core.Tests.TestGrids;

namespace Lumen.Core.Tests
{
    /// <summary>T3: 승강 거울 4상태 × 6방향 반사 표. 면과 나란한 광선은 통과(T18의 바탕).</summary>
    [TestFixture]
    public class LiftMirrorTests
    {
        [TestCase(PieceState.XRise, Direction.East, Direction.Up)]
        [TestCase(PieceState.XRise, Direction.Down, Direction.West)]
        [TestCase(PieceState.XRise, Direction.West, Direction.Down)]
        [TestCase(PieceState.XRise, Direction.Up, Direction.East)]
        [TestCase(PieceState.XRise, Direction.North, Direction.North)]
        [TestCase(PieceState.XRise, Direction.South, Direction.South)]
        [TestCase(PieceState.XFall, Direction.East, Direction.Down)]
        [TestCase(PieceState.XFall, Direction.Up, Direction.West)]
        [TestCase(PieceState.XFall, Direction.West, Direction.Up)]
        [TestCase(PieceState.XFall, Direction.Down, Direction.East)]
        [TestCase(PieceState.XFall, Direction.North, Direction.North)]
        [TestCase(PieceState.XFall, Direction.South, Direction.South)]
        [TestCase(PieceState.ZRise, Direction.North, Direction.Up)]
        [TestCase(PieceState.ZRise, Direction.Down, Direction.South)]
        [TestCase(PieceState.ZRise, Direction.South, Direction.Down)]
        [TestCase(PieceState.ZRise, Direction.Up, Direction.North)]
        [TestCase(PieceState.ZRise, Direction.East, Direction.East)]
        [TestCase(PieceState.ZRise, Direction.West, Direction.West)]
        [TestCase(PieceState.ZFall, Direction.North, Direction.Down)]
        [TestCase(PieceState.ZFall, Direction.Up, Direction.South)]
        [TestCase(PieceState.ZFall, Direction.South, Direction.Up)]
        [TestCase(PieceState.ZFall, Direction.Down, Direction.North)]
        [TestCase(PieceState.ZFall, Direction.East, Direction.East)]
        [TestCase(PieceState.ZFall, Direction.West, Direction.West)]
        public void T3_ReflectionTable(PieceState state, Direction incoming, Direction expected)
        {
            Assert.AreEqual(expected, Reflection.ReflectLift(state, incoming));

            var result = BeamTracer.Trace(CenterMirror(Lift(state), incoming, expected));

            Assert.IsTrue(result.AllCrystalsLit, "광선이 표의 출구 크리스털에 닿아야 한다.");
            var turned = expected != incoming;
            Assert.AreEqual(turned, result.IsDeflecting(P(1, 1, 1)),
                turned ? "꺾은 승강 거울은 유효 배치다." : "면과 나란해 통과한 승강 거울은 유효 배치가 아니다(T18).");
        }

        [Test]
        public void T3_ReflectionIsReversible()
        {
            // 거울 반사는 경로를 거꾸로 따라가도 같아야 한다: a→b 이면 반대(b)→반대(a).
            foreach (var state in new[] { PieceState.XRise, PieceState.XFall, PieceState.ZRise, PieceState.ZFall })
            {
                foreach (Direction incoming in System.Enum.GetValues(typeof(Direction)))
                {
                    var outgoing = Reflection.ReflectLift(state, incoming);
                    Assert.AreEqual(incoming.Opposite(), Reflection.ReflectLift(state, outgoing.Opposite()),
                        $"{state}: {incoming}->{outgoing}");
                }
            }
        }

        [Test]
        public void T18_PassThroughDoesNotSplitSegment()
        {
            // 광원 (0,0,0) 동쪽 → (1,0,0) ZRise(동서 통과) → (2,0,0) 크리스털: 구간은 하나다.
            var grid = new Grid(P(3, 1, 1));
            grid.AddSource(P(0, 0, 0), Direction.East);
            grid.PlacePiece(P(1, 0, 0), Lift(PieceState.ZRise));
            grid.AddCrystal(P(2, 0, 0));

            var result = BeamTracer.Trace(grid);

            Assert.IsTrue(result.AllCrystalsLit);
            Assert.AreEqual(1, result.Segments.Count);
            Assert.AreEqual(0, result.DeflectingPieces.Count);
        }

        [Test]
        public void ReflectLiftRejectsPlaneState()
        {
            Assert.Throws<System.ArgumentException>(() => Reflection.ReflectLift(PieceState.Slash, Direction.East));
        }
    }
}
