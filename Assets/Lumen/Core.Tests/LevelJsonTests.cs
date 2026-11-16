using NUnit.Framework;
using UnityEngine;

namespace Lumen.Core.Tests
{
    /// <summary>
    /// T16·T27: 실제 레벨 JSON 파일을 Unity JsonUtility로 읽는다.
    /// JsonUtility가 필요해 Unity 안에서만 실행된다.
    /// </summary>
    [TestFixture]
    public class LevelJsonTests
    {
        private const string LevelFolder = "Assets/Lumen/Resources/Levels";
        private const string Level05Path = LevelFolder + "/level05.json";

        [Test]
        public void T16_Level05JsonLoadsAndSolves()
        {
            var text = System.IO.File.ReadAllText(Level05Path);
            var data = JsonUtility.FromJson<LevelData>(text);
            var definition = LevelDefinition.FromData(data);

            Assert.AreEqual("level05", definition.Id);
            Assert.AreEqual("위층으로", definition.Name);
            Assert.AreEqual(PieceState.XFall, definition.FixedPieces[0].Piece.State);
            Assert.AreEqual(2, definition.Pedestals.Count);

            var session = new LevelSession(definition);
            Assert.IsTrue(session.TryPlace(new Int3(2, 0, 1), PieceType.LiftMirror));
            Assert.IsTrue(session.IsCleared);
        }

        /// <summary>
        /// 모든 레벨이 풀리고, JSON의 최소 배치 수·Par(최소 배치 해 중 가장 짧은 경로)가 솔버 결과와 같은지 확인한다.
        /// 레벨 규칙(11/16): 별 3개 해(최소 배치 수 + Par 경로)는 레벨마다 정확히 1개.
        /// (11/12의 "8레벨부터 해 3개 이하"는 경로 점수 도입으로 삭제. 해가 여럿인 것은 실패의 자유로 본다.)
        /// 레벨을 추가·수정하면 이 테스트가 먼저 깨진다.
        /// </summary>
        [Test]
        public void T27_EveryLevelIsSolvableAndMatchesSolver()
        {
            var files = System.IO.Directory.GetFiles(LevelFolder, "*.json");
            System.Array.Sort(files, System.StringComparer.Ordinal);
            Assert.IsNotEmpty(files);

            for (var i = 0; i < files.Length; i++)
            {
                var definition = LevelDefinition.FromData(JsonUtility.FromJson<LevelData>(System.IO.File.ReadAllText(files[i])));
                var result = Solver.Solve(definition);
                var summary = Solver.Describe(definition, result, 3);

                Assert.IsFalse(result.HitNodeLimit, summary);
                Assert.IsTrue(result.Solvable, summary);
                Assert.Greater(result.MinPlacements, 0, "거울 없이 풀리는 레벨: " + summary);
                Assert.AreEqual(result.MinPlacements, definition.SolverMinPlacements, "solverMinPlacements 불일치: " + summary);
                Assert.AreEqual(result.ParPath, definition.SolverMinPath, "solverMinPath 불일치: " + summary);
                Assert.AreEqual(1, result.ParSolutionCount, "별 3개 해가 정확히 1개가 아님: " + summary);
            }
        }
    }
}
