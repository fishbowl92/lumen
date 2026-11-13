using NUnit.Framework;
using static Lumen.Core.Tests.TestGrids;

namespace Lumen.Core.Tests
{
    /// <summary>T4·T5·T6과 격자 기본 규칙.</summary>
    [TestFixture]
    public class BeamTracerBasicTests
    {
        [Test]
        public void T4_BeamLeavingGridVanishes()
        {
            var grid = new Grid(P(3, 1, 1));
            grid.AddSource(P(0, 0, 0), Direction.East);

            var result = BeamTracer.Trace(grid);

            Assert.AreEqual(0, result.LitCrystals.Count);
            Assert.AreEqual(1, result.Segments.Count);
            var segment = result.Segments[0];
            Assert.AreEqual(SegmentEnd.OutOfBounds, segment.End);
            Assert.AreEqual(P(0, 0, 0), segment.From);
            Assert.AreEqual(P(3, 0, 0), segment.To, "To는 격자 밖 첫 칸이다.");
            Assert.AreEqual(3, result.Steps);
        }

        [Test]
        public void T5_WallBlocksBeam()
        {
            var grid = new Grid(P(3, 1, 1));
            grid.AddSource(P(0, 0, 0), Direction.East);
            grid.AddWall(P(1, 0, 0));
            grid.AddCrystal(P(2, 0, 0));

            var result = BeamTracer.Trace(grid);

            Assert.AreEqual(0, result.LitCrystals.Count);
            Assert.AreEqual(SegmentEnd.Blocked, result.Segments[0].End);
            Assert.AreEqual(P(1, 0, 0), result.Segments[0].To);
        }

        [Test]
        public void T5_OtherSourceBlocksBeam()
        {
            var grid = new Grid(P(3, 1, 3));
            grid.AddSource(P(0, 0, 0), Direction.East);
            grid.AddSource(P(1, 0, 0), Direction.North); // 첫 광선의 길을 막는 두 번째 광원
            grid.AddCrystal(P(2, 0, 0));

            var result = BeamTracer.Trace(grid);

            Assert.IsFalse(result.IsLit(P(2, 0, 0)));
            Assert.AreEqual(SegmentEnd.Blocked, result.Segments[0].End);
        }

        [Test]
        public void T6_CrystalHitByTwoBeamsLightsOnce()
        {
            var grid = new Grid(P(3, 1, 3));
            grid.AddCrystal(P(1, 0, 1));
            grid.AddSource(P(0, 0, 1), Direction.East);
            grid.AddSource(P(1, 0, 0), Direction.North);

            var result = BeamTracer.Trace(grid);

            Assert.AreEqual(1, result.LitCrystals.Count);
            Assert.IsTrue(result.AllCrystalsLit);
            Assert.AreEqual(2, result.Segments.Count);
            Assert.AreEqual(SegmentEnd.Absorbed, result.Segments[0].End);
            Assert.AreEqual(SegmentEnd.Absorbed, result.Segments[1].End);
        }

        [Test]
        public void NoCrystalMeansNotWon()
        {
            var grid = new Grid(P(2, 1, 1));
            grid.AddSource(P(0, 0, 0), Direction.East);

            Assert.IsFalse(BeamTracer.Trace(grid).AllCrystalsLit);
        }

        [Test]
        public void TwoBouncesReachCrystal()
        {
            // 광원 (0,0,0) 동쪽 → (2,0,0) '/' 북쪽 → (2,0,2) '\' 서쪽 → (0,0,2) 크리스털
            var grid = new Grid(P(3, 1, 3));
            grid.AddSource(P(0, 0, 0), Direction.East);
            grid.PlacePiece(P(2, 0, 0), Plane(PieceState.Slash));
            grid.PlacePiece(P(2, 0, 2), Plane(PieceState.Backslash));
            grid.AddCrystal(P(0, 0, 2));

            var result = BeamTracer.Trace(grid);

            Assert.IsTrue(result.AllCrystalsLit);
            Assert.AreEqual(3, result.Segments.Count);
            Assert.AreEqual(2, result.DeflectingPieces.Count);
        }

        [Test]
        public void Grid_RejectsOccupiedCell()
        {
            var grid = new Grid(P(2, 1, 1));
            grid.AddWall(P(0, 0, 0));

            Assert.Throws<System.InvalidOperationException>(() => grid.AddCrystal(P(0, 0, 0)));
        }

        [Test]
        public void Grid_RejectsOutOfBounds()
        {
            var grid = new Grid(P(2, 1, 1));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => grid.AddWall(P(2, 0, 0)));
        }

        [Test]
        public void Grid_RemovePieceFreesCell()
        {
            var grid = new Grid(P(2, 1, 1));
            grid.PlacePiece(P(0, 0, 0), Plane(PieceState.Slash));

            Assert.IsTrue(grid.RemovePiece(P(0, 0, 0)));
            Assert.AreEqual(CellKind.Empty, grid.GetKind(P(0, 0, 0)));
            Assert.IsFalse(grid.RemovePiece(P(0, 0, 0)));
        }

        [Test]
        public void Piece_RejectsInvalidState()
        {
            Assert.Throws<System.ArgumentException>(() => new Piece(PieceType.PlaneMirror, PieceState.XRise, false));
            Assert.Throws<System.ArgumentException>(() => new Piece(PieceType.LiftMirror, PieceState.Slash, false));
        }
    }
}
