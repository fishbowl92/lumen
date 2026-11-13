using Lumen.Core;
using UnityEngine;

namespace Lumen.Game
{
    /// <summary>클릭할 수 있는 물체(받침대 타일, 플레이어 거울)가 어느 칸인지 알려 주는 표식.</summary>
    public sealed class CellTag : MonoBehaviour
    {
        public Int3 Position { get; private set; }

        public void Set(Int3 position) => Position = position;
    }
}
