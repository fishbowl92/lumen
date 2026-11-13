using System;
using NUnit.Framework;
using static Lumen.Core.Tests.TestGrids;

namespace Lumen.Core.Tests
{
    /// <summary>T16: 레벨 데이터 변환과 검증.</summary>
    [TestFixture]
    public class LevelDefinitionTests
    {
        [Test]
        public void T16_Level05FieldsMatch()
        {
            var definition = LevelDefinition.FromData(TestLevels.Level05Data());

            Assert.AreEqual("level05", definition.Id);
            Assert.AreEqual("위층으로", definition.Name);
            Assert.AreEqual(P(3, 2, 3), definition.Size);
            Assert.AreEqual(1, definition.Sources.Count);
            Assert.AreEqual(Direction.East, definition.Sources[0].Direction);
            Assert.AreEqual(P(0, 1, 1), definition.Crystals[0]);
            Assert.AreEqual(1, definition.FixedPieces.Count);
            Assert.AreEqual(PieceType.LiftMirror, definition.FixedPieces[0].Piece.Type);
            Assert.AreEqual(PieceState.XFall, definition.FixedPieces[0].Piece.State);
            Assert.IsTrue(definition.FixedPieces[0].Piece.IsFixed);
            Assert.IsTrue(definition.IsPedestal(P(2, 0, 1)));
            Assert.IsTrue(definition.IsPedestal(P(1, 0, 2)));
            Assert.IsFalse(definition.IsPedestal(P(0, 0, 0)));
            Assert.AreEqual(1, definition.InventoryOf(PieceType.PlaneMirror));
            Assert.AreEqual(1, definition.InventoryOf(PieceType.LiftMirror));
            Assert.AreEqual(0, definition.InventoryOf(PieceType.Splitter));
            Assert.AreEqual(1, definition.SolverMinPlacements);
        }

        [Test]
        public void T16_InvalidStateTextIsRejected()
        {
            var data = TestLevels.Level05Data();
            data.fixedPieces[0].state = "Sideways";

            Assert.Throws<FormatException>(() => LevelDefinition.FromData(data));
        }

        [Test]
        public void T16_StateNotAllowedForTypeIsRejected()
        {
            var data = TestLevels.Level05Data();
            data.fixedPieces[0].state = "/"; // 승강 거울에 평면 거울 상태

            Assert.Throws<FormatException>(() => LevelDefinition.FromData(data));
        }

        [Test]
        public void T16_InvalidDirectionIsRejected()
        {
            var data = TestLevels.Level05Data();
            data.sources[0].dir = "east";

            Assert.Throws<FormatException>(() => LevelDefinition.FromData(data));
        }

        [Test]
        public void T16_OverlappingCellsAreRejected()
        {
            var data = TestLevels.Level05Data();
            data.crystals[0] = new CellData { x = 2, y = 1, z = 1 }; // 고정 거울 칸과 겹침

            Assert.Throws<FormatException>(() => LevelDefinition.FromData(data));
        }

        [Test]
        public void T16_PedestalMustBeEmptyAndInside()
        {
            var data = TestLevels.Level05Data();
            data.pedestals[1] = new CellData { x = 0, y = 0, z = 1 }; // 광원 칸

            Assert.Throws<FormatException>(() => LevelDefinition.FromData(data));

            data = TestLevels.Level05Data();
            data.pedestals[1] = new CellData { x = 5, y = 0, z = 0 };

            Assert.Throws<FormatException>(() => LevelDefinition.FromData(data));
        }

        [Test]
        public void T16_NeedsSourceAndCrystal()
        {
            var data = TestLevels.Level05Data();
            data.crystals = new CellData[0];

            Assert.Throws<FormatException>(() => LevelDefinition.FromData(data));
        }
    }
}
