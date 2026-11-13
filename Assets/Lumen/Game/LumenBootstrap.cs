using UnityEngine;

namespace Lumen.Game
{
    /// <summary>
    /// 씬 파일을 고치지 않고 게임을 띄우는 진입점. 씬이 열리면 LumenGame을 하나 만든다.
    /// 씬에는 카메라·조명·볼륨만 두고, 나머지는 모두 코드로 만든다(프로젝트 CLAUDE.md 규칙).
    /// </summary>
    public static class LumenBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<LumenGame>() != null)
            {
                return;
            }

            new GameObject("LUMEN").AddComponent<LumenGame>();
        }
    }
}
