using NUnit.Framework;
using UnityEngine;

namespace Lumen.Core.Tests
{
    /// <summary>
    /// T16: 실제 레벨 JSON 파일을 Unity JsonUtility로 읽는다.
    /// JsonUtility가 필요해 Unity 안에서만 실행된다.
    /// </summary>
    [TestFixture]
    public class LevelJsonTests
    {
        private const string Level05Path = "Assets/Lumen/Resources/Levels/level05.json";

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
    }
}
