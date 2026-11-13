using System.Collections.Generic;
using NUnit.Framework;

namespace Lumen.Core.Tests
{
    /// <summary>
    /// T20~T26, T29: 솔버. 기대값은 파이썬 전수 탐색(reference/solver_bruteforce.py)으로 구했다.
    /// </summary>
    [TestFixture]
    public class SolverTests
    {
        private static SolverResult Solve(LevelData data, int nodeLimit = Solver.DefaultNodeLimit) =>
            Solver.Solve(LevelDefinition.FromData(data), nodeLimit);

        private static LevelData Open(int sizeX, int sizeZ, CellData[] crystals, CellData[] pedestals, InventoryData inventory) =>
            new LevelData
            {
                id = "test",
                sizeX = sizeX,
                sizeY = 1,
                sizeZ = sizeZ,
                sources = new[] { new SourceData { x = 0, y = 0, z = 0, dir = "East" } },
                crystals = crystals,
                walls = new CellData[0],
                fixedPieces = new PieceData[0],
                pedestals = pedestals,
                inventory = inventory
            };

        private static CellData C(int x, int z) => new CellData { x = x, y = 0, z = z };

        [Test]
        public void T20_Level01HasOneSolution()
        {
            var r = Solve(TestLevels.Level01Data());

            Assert.IsTrue(r.Solvable);
            Assert.AreEqual(1, r.MinPlacements);
            Assert.AreEqual(1, r.SolutionCount);
            Assert.AreEqual("(1, 0, 1) Plane /", r.Solutions[0].Key);
            Assert.AreEqual(0, r.FloorChanges);
            Assert.AreEqual(5, r.SearchSpace);
            Assert.IsFalse(r.HitNodeLimit);
        }

        [Test]
        public void T21_Level05HasOneSolutionWithOneFloorChange()
        {
            // (1,0,2)의 평면 거울은 광선이 닿지 않아 어떤 해에도 들어가지 않는다.
            var r = Solve(TestLevels.Level05Data());

            Assert.AreEqual(1, r.MinPlacements);
            Assert.AreEqual(1, r.SolutionCount);
            Assert.AreEqual("(2, 0, 1) Lift XRise", r.Solutions[0].Key);
            Assert.AreEqual(1, r.FloorChanges);
            Assert.AreEqual(29, r.SearchSpace);
        }

        [Test]
        public void T22_NotEnoughMirrorsIsUnsolvable()
        {
            var data = TestLevels.Level01Data();
            data.inventory = new InventoryData { plane = 0, lift = 0, splitter = 1 };
            var r = Solve(data);

            // 분할기 '/'는 크리스털을 켜지만 직진 광선도 남긴다 → 해. 분할기도 없으면 풀 수 없다.
            Assert.IsTrue(r.Solvable);

            data.inventory = new InventoryData();
            r = Solve(data);
            Assert.IsFalse(r.Solvable);
            Assert.AreEqual(-1, r.MinPlacements);
            Assert.AreEqual(0, r.SolutionCount);
        }

        [Test]
        public void T23_CrystalInFrontOfSourceNeedsNoMirror()
        {
            var r = Solve(Open(3, 1, new[] { C(2, 0) }, new[] { C(1, 0) },
                new InventoryData { plane = 1 }));

            Assert.AreEqual(0, r.MinPlacements);
            Assert.AreEqual(1, r.SolutionCount);
            Assert.AreEqual(0, r.Solutions[0].Count);
        }

        [Test]
        public void T24_MultipleSolutionsAreSortedBySize()
        {
            // 4×4 빈 바닥, 광원 (0,0,0) 동쪽, 크리스털 (3,0,3), 평면 거울 2개, 받침대는 x=1~3 전부
            var pedestals = new List<CellData>();
            for (var x = 1; x < 4; x++)
            {
                for (var z = 0; z < 4; z++)
                {
                    if (x != 3 || z != 3)
                    {
                        pedestals.Add(C(x, z));
                    }
                }
            }

            var r = Solve(Open(4, 4, new[] { C(3, 3) }, pedestals.ToArray(), new InventoryData { plane = 2 }));

            Assert.AreEqual(3, r.SolutionCount);
            Assert.AreEqual(1, r.MinPlacements);
            Assert.AreEqual(1, r.MinSolutionCount);
            Assert.AreEqual("(3, 0, 0) Plane /", r.Solutions[0].Key);
            Assert.AreEqual("(1, 0, 0) Plane / | (1, 0, 3) Plane /", r.Solutions[1].Key);
            Assert.AreEqual("(2, 0, 0) Plane / | (2, 0, 3) Plane /", r.Solutions[2].Key);
            Assert.AreEqual(243, r.SearchSpace);
        }

        [Test]
        public void T25_NearMissesCountOneDarkCrystal()
        {
            // 광원 (0,0,1) 동쪽, 크리스털 (1,0,2)·(2,0,1), 받침대 (1,0,1), 평면 1·분할기 1
            // 해: 분할기 '/' 하나. 근접 오답: 비움, 평면 '/', 분할기 '\' (각각 크리스털 1개만 켬)
            var data = Open(3, 3, new[] { C(1, 2), C(2, 1) }, new[] { C(1, 1) },
                new InventoryData { plane = 1, splitter = 1 });
            data.sources = new[] { new SourceData { x = 0, y = 0, z = 1, dir = "East" } };
            var r = Solve(data);

            Assert.AreEqual(1, r.SolutionCount);
            Assert.AreEqual("(1, 0, 1) Splitter /", r.Solutions[0].Key);
            Assert.AreEqual(3, r.NearMisses);
        }

        [Test]
        public void T29_ParCountsPlacementsAndRotations()
        {
            Assert.AreEqual(0, PieceRules.RotationsTo(PieceType.PlaneMirror, PieceState.Slash));
            Assert.AreEqual(1, PieceRules.RotationsTo(PieceType.PlaneMirror, PieceState.Backslash));
            Assert.AreEqual(0, PieceRules.RotationsTo(PieceType.LiftMirror, PieceState.XRise));
            Assert.AreEqual(1, PieceRules.RotationsTo(PieceType.LiftMirror, PieceState.ZRise));
            Assert.AreEqual(2, PieceRules.RotationsTo(PieceType.LiftMirror, PieceState.XFall));
            Assert.AreEqual(3, PieceRules.RotationsTo(PieceType.LiftMirror, PieceState.ZFall));

            Assert.AreEqual(1, Solve(TestLevels.Level05Data()).MinActions); // 승강 XRise: 배치 1

            // 1레벨을 ''로만 풀리게 바꾸면: 광원 (0,0,1) 동쪽 → (1,0,1) '' → 남쪽 (1,0,0)
            var data = TestLevels.Level01Data();
            data.crystals = new[] { C(1, 0) };
            var r = Solve(data);
            Assert.AreEqual("(1, 0, 1) Plane \\", r.Solutions[0].Key);
            Assert.AreEqual(2, r.MinActions); // 배치 1 + 회전 1
        }

        [Test]
        public void T26_NodeLimitIsReported()
        {
            var r = Solve(TestLevels.Level05Data(), nodeLimit: 1);

            Assert.IsTrue(r.HitNodeLimit);
        }
    }
}
