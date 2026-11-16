using NUnit.Framework;
using static Lumen.Core.Tests.TestGrids;

namespace Lumen.Core.Tests
{
    /// <summary>T10~T15, T17, T19, T28: 5레벨 풀이, 배치 규칙, 되돌리기, 유효 배치, 조작 수와 별.</summary>
    [TestFixture]
    public class LevelSessionTests
    {
        private static readonly Int3 Pedestal1 = P(2, 0, 1); // 정답 자리
        private static readonly Int3 Pedestal2 = P(1, 0, 2); // 광선이 지나지 않는 자리
        private static readonly Int3 FixedLift = P(2, 1, 1);

        [Test]
        public void T10_NoPlacementDoesNotWin()
        {
            var session = TestLevels.Level05();

            Assert.IsFalse(session.IsCleared);
            Assert.AreEqual(0, session.LastTrace.LitCrystals.Count);
            Assert.AreEqual(0, session.Stars);
        }

        [Test]
        public void T11_LiftXRiseSolvesLevel05()
        {
            var session = TestLevels.Level05();

            Assert.IsTrue(session.TryPlace(Pedestal1, PieceType.LiftMirror)); // 처음 상태 XRise

            Assert.IsTrue(session.IsCleared);
            Assert.IsTrue(session.LastTrace.IsDeflecting(Pedestal1));
            Assert.IsTrue(session.LastTrace.IsDeflecting(FixedLift));
            Assert.AreEqual(1, session.EffectivePlacedCount);
            Assert.AreEqual(3, session.Stars);

            // 경로: (0,0,1)→(2,0,1) 위로→(2,1,1) 서쪽→(0,1,1)
            var segments = session.LastTrace.Segments;
            Assert.AreEqual(3, segments.Count);
            Assert.AreEqual(Direction.East, segments[0].Direction);
            Assert.AreEqual(Direction.Up, segments[1].Direction);
            Assert.AreEqual(Direction.West, segments[2].Direction);
            Assert.AreEqual(P(0, 1, 1), segments[2].To);
        }

        [Test]
        public void T12_OtherLiftStatesDoNotSolve()
        {
            // 세션은 XRise로 놓는 순간 클리어되어 잠기므로, 다른 상태는 격자에 직접 놓고 계산한다.
            foreach (var state in new[] { PieceState.ZRise, PieceState.XFall, PieceState.ZFall })
            {
                var grid = LevelDefinition.FromData(TestLevels.Level05Data()).CreateGrid();
                grid.PlacePiece(Pedestal1, Lift(state));
                Assert.IsFalse(BeamTracer.Trace(grid).AllCrystalsLit, $"승강 {state}");
            }

            foreach (var state in new[] { PieceState.Slash, PieceState.Backslash })
            {
                var grid = LevelDefinition.FromData(TestLevels.Level05Data()).CreateGrid();
                grid.PlacePiece(Pedestal1, Plane(state));
                Assert.IsFalse(BeamTracer.Trace(grid).AllCrystalsLit, $"평면 {state}");
            }
        }

        [Test]
        public void T13_RotationOrder()
        {
            Assert.AreEqual(PieceState.Backslash, PieceRules.NextState(PieceType.PlaneMirror, PieceState.Slash));
            Assert.AreEqual(PieceState.Slash, PieceRules.NextState(PieceType.PlaneMirror, PieceState.Backslash));
            Assert.AreEqual(PieceState.ZRise, PieceRules.NextState(PieceType.LiftMirror, PieceState.XRise));
            Assert.AreEqual(PieceState.XFall, PieceRules.NextState(PieceType.LiftMirror, PieceState.ZRise));
            Assert.AreEqual(PieceState.ZFall, PieceRules.NextState(PieceType.LiftMirror, PieceState.XFall));
            Assert.AreEqual(PieceState.XRise, PieceRules.NextState(PieceType.LiftMirror, PieceState.ZFall));
            Assert.AreEqual(PieceState.Backslash, PieceRules.NextState(PieceType.Splitter, PieceState.Slash));
        }

        [Test]
        public void T13_RotationMatchesGeometry()
        {
            // 위에서 볼 때 90° 회전 = 동→북, 북→서, 서→남, 남→동. 회전 전후 반사가 이 관계를 지켜야 한다.
            foreach (var state in new[] { PieceState.XRise, PieceState.ZRise, PieceState.XFall, PieceState.ZFall })
            {
                var next = PieceRules.NextState(PieceType.LiftMirror, state);
                foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
                {
                    Assert.AreEqual(Rotate(Reflection.ReflectLift(state, d)), Reflection.ReflectLift(next, Rotate(d)),
                        $"{state}→{next}, {d}");
                }
            }

            foreach (var d in new[] { Direction.East, Direction.West, Direction.North, Direction.South })
            {
                Assert.AreEqual(Rotate(Reflection.ReflectPlane(PieceState.Slash, d)),
                    Reflection.ReflectPlane(PieceState.Backslash, Rotate(d)));
            }
        }

        [Test]
        public void T14_PlacementRules()
        {
            var session = TestLevels.Level05();

            Assert.IsFalse(session.TryPlace(P(1, 0, 1), PieceType.PlaneMirror), "받침대가 아닌 칸");
            Assert.IsFalse(session.TryPlace(Pedestal2, PieceType.Splitter), "인벤토리에 없는 종류");
            Assert.IsTrue(session.TryPlace(Pedestal2, PieceType.PlaneMirror));
            Assert.AreEqual(0, session.Remaining(PieceType.PlaneMirror));
            Assert.IsFalse(session.TryPlace(Pedestal1, PieceType.PlaneMirror), "인벤토리 초과");
            Assert.IsFalse(session.TryPlace(Pedestal2, PieceType.LiftMirror), "이미 거울이 있는 받침대");

            Assert.IsFalse(session.TryRotate(FixedLift), "고정 거울 회전");
            Assert.IsFalse(session.TryRemove(FixedLift), "고정 거울 회수");

            Assert.IsTrue(session.TryRemove(Pedestal2));
            Assert.AreEqual(1, session.Remaining(PieceType.PlaneMirror), "회수하면 인벤토리로 돌아온다");
            Assert.AreEqual(CellKind.Empty, session.Grid.GetKind(Pedestal2));
        }

        [Test]
        public void T15_UndoAndReset()
        {
            var session = TestLevels.Level05();

            Assert.IsTrue(session.TryPlace(Pedestal2, PieceType.PlaneMirror));
            Assert.IsTrue(session.TryRotate(Pedestal2));
            Assert.IsTrue(session.TryRemove(Pedestal2));

            Assert.IsTrue(session.Undo()); // 회수 취소 → \ 상태로 돌아옴
            Assert.IsTrue(session.Grid.TryGetPiece(Pedestal2, out var piece));
            Assert.AreEqual(PieceState.Backslash, piece.State);
            Assert.AreEqual(0, session.Remaining(PieceType.PlaneMirror));

            Assert.IsTrue(session.Undo()); // 회전 취소 → /
            session.Grid.TryGetPiece(Pedestal2, out piece);
            Assert.AreEqual(PieceState.Slash, piece.State);

            Assert.IsTrue(session.Undo()); // 배치 취소
            Assert.AreEqual(CellKind.Empty, session.Grid.GetKind(Pedestal2));
            Assert.AreEqual(1, session.Remaining(PieceType.PlaneMirror));
            Assert.IsFalse(session.Undo(), "더 되돌릴 것이 없다");

            session.TryPlace(Pedestal1, PieceType.LiftMirror);
            Assert.IsTrue(session.IsCleared);
            Assert.IsFalse(session.TryRemove(Pedestal1), "클리어 뒤에는 입력이 잠긴다");
            Assert.IsFalse(session.Undo(), "클리어 뒤에는 되돌리기도 잠긴다");

            session.Reset();
            Assert.IsFalse(session.IsCleared);
            Assert.AreEqual(0, session.PlacedCount);
            Assert.AreEqual(1, session.Remaining(PieceType.LiftMirror));
        }

        [Test]
        public void T17_UnusedMirrorIsNotCounted()
        {
            var session = TestLevels.Level05();

            Assert.IsTrue(session.TryPlace(Pedestal2, PieceType.PlaneMirror)); // 광선이 닿지 않는 자리
            Assert.IsTrue(session.TryPlace(Pedestal1, PieceType.LiftMirror));

            Assert.IsTrue(session.IsCleared);
            Assert.AreEqual(2, session.PlacedCount);
            Assert.AreEqual(1, session.EffectivePlacedCount);
            Assert.AreEqual(3, session.Stars, "광선이 닿지 않는 거울은 세지 않는다: 유효 거울 1개, 경로 5 = Par → 별 3개");
        }

        // 별 규칙(11/16 변경): 유효 거울 수 > 최소 배치 수 → 1, 아니면 경로 ≤ Par → 3, 그 밖 → 2
        [TestCase(3, 3, 6, 6, 3)]   // 최소 거울, 최단 경로
        [TestCase(3, 3, 10, 6, 2)]  // 최소 거울, 돌아간 경로
        [TestCase(4, 3, 5, 6, 1)]   // 거울을 더 써서 경로가 짧아져도 "어시스트" → 1
        [TestCase(4, 3, 12, 6, 1)]
        [TestCase(1, 1, 5, 5, 3)]
        public void T19_StarRule(int mirrors, int minMirrors, int path, int par, int expectedStars)
        {
            Assert.AreEqual(expectedStars, LevelSession.ScoreStars(mirrors, minMirrors, path, par));
        }

        [Test]
        public void T19_TwoRoutesGiveThreeAndTwoStars()
        {
            var shortRoute = new LevelSession(LevelDefinition.FromData(TestLevels.Level06Data()));
            Assert.IsTrue(shortRoute.TryPlace(new Int3(3, 0, 4), PieceType.LiftMirror));
            Assert.IsTrue(shortRoute.TryPlace(new Int3(3, 1, 2), PieceType.PlaneMirror));
            Assert.IsTrue(shortRoute.TryPlace(new Int3(4, 1, 2), PieceType.LiftMirror));
            SetState(shortRoute, new Int3(3, 0, 4), PieceState.XFall);
            SetState(shortRoute, new Int3(3, 1, 2), PieceState.Backslash);
            SetState(shortRoute, new Int3(4, 1, 2), PieceState.XFall);
            Assert.IsTrue(shortRoute.IsCleared);
            Assert.AreEqual(6, shortRoute.PathLength);
            Assert.AreEqual(3, shortRoute.Stars);

            var longRoute = new LevelSession(LevelDefinition.FromData(TestLevels.Level06Data()));
            Assert.IsTrue(longRoute.TryPlace(new Int3(1, 0, 4), PieceType.LiftMirror));
            Assert.IsTrue(longRoute.TryPlace(new Int3(1, 1, 2), PieceType.PlaneMirror));
            Assert.IsTrue(longRoute.TryPlace(new Int3(4, 1, 2), PieceType.LiftMirror));
            SetState(longRoute, new Int3(1, 0, 4), PieceState.XFall);
            SetState(longRoute, new Int3(1, 1, 2), PieceState.Backslash);
            SetState(longRoute, new Int3(4, 1, 2), PieceState.XFall);
            Assert.IsTrue(longRoute.IsCleared);
            Assert.AreEqual(10, longRoute.PathLength);
            Assert.AreEqual(2, longRoute.Stars);
        }

        /// <summary>클리어 전까지 거울을 돌려 원하는 상태로 맞춘다(클리어되면 입력이 잠긴다).</summary>
        private static void SetState(LevelSession session, Int3 position, PieceState target)
        {
            for (var i = 0; i < 4 && !session.IsCleared; i++)
            {
                session.Grid.TryGetPiece(position, out var piece);
                if (piece.State == target)
                {
                    return;
                }

                session.TryRotate(position);
            }
        }

        [Test]
        public void T28_MovesCountEveryChangeAndResetClearsThem()
        {
            var session = TestLevels.Level05();

            Assert.IsTrue(session.TryPlace(Pedestal2, PieceType.PlaneMirror)); // 1
            Assert.IsTrue(session.TryRotate(Pedestal2));                         // 2
            Assert.IsTrue(session.TryRemove(Pedestal2));                         // 3
            Assert.IsTrue(session.Undo());                                       // 4 (평면 거울이 돌아온다)
            Assert.IsFalse(session.TryRotate(Pedestal1), "빈 칸 회전은 조작으로 세지 않는다");
            Assert.AreEqual(4, session.MoveCount);

            Assert.IsTrue(session.TryPlace(Pedestal1, PieceType.LiftMirror));    // 5, 클리어
            Assert.IsTrue(session.IsCleared);
            Assert.AreEqual(5, session.MoveCount);
            Assert.AreEqual(3, session.Stars, "조작 수는 별에 영향이 없다(11/16): 여러 번 시도해도 최단 경로면 별 3개");

            session.Reset();
            Assert.AreEqual(0, session.MoveCount);
            Assert.IsTrue(session.TryPlace(Pedestal1, PieceType.LiftMirror));
            Assert.AreEqual(3, session.Stars);
        }

        private static Direction Rotate(Direction d)
        {
            switch (d)
            {
                case Direction.East: return Direction.North;
                case Direction.North: return Direction.West;
                case Direction.West: return Direction.South;
                case Direction.South: return Direction.East;
                default: return d; // 위·아래는 y축 회전에 불변
            }
        }
    }
}
