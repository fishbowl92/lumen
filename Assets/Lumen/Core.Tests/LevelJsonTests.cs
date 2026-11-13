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
        /// 모든 레벨이 풀리고, JSON의 최소 배치 수·최소 조작 수(Par)가 솔버 결과와 같은지 확인한다.
        /// 8레벨부터는 해가 3개 이하여야 한다(기획서 엣지 케이스 표: 답이 너무 많으면 우연히 풀린다).
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
                Assert.AreEqual(result.MinActions, definition.SolverMinActions, "solverMinActions 불일치: " + summary);

                var number = LevelNumber(definition.Id);
                if (number >= 8)
                {
                    Assert.LessOrEqual(result.SolutionCount, 3, "8레벨 이후 해 3개 초과: " + summary);
                }
            }
        }

        private static int LevelNumber(string id)
        {
            var digits = new System.Text.StringBuilder();
            foreach (var c in id)
            {
                if (char.IsDigit(c))
                {
                    digits.Append(c);
                }
            }

            return digits.Length > 0 ? int.Parse(digits.ToString()) : 0;
        }
    }
}
