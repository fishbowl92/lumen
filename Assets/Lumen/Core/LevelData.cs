using System;

namespace Lumen.Core
{
    // 레벨 JSON 형식. Unity JsonUtility가 읽을 수 있도록 public 필드와 [Serializable] 클래스만 쓴다.
    // JsonUtility는 중첩 배열·딕셔너리·문자열 enum을 지원하지 않으므로 방향·종류·상태는 문자열로 받는다.
    // 예시: Assets/Lumen/Resources/Levels/level05.json (Resources에 두어야 게임이 런타임에 읽는다)

    [Serializable]
    public class LevelData
    {
        public string id;
        public string name;
        public int sizeX;
        public int sizeY;
        public int sizeZ;
        public SourceData[] sources;
        public CellData[] crystals;
        public CellData[] walls;
        public PieceData[] fixedPieces;
        public CellData[] pedestals;
        public InventoryData inventory;
        public int solverMinPlacements;
    }

    [Serializable]
    public class CellData
    {
        public int x;
        public int y;
        public int z;
    }

    [Serializable]
    public class SourceData
    {
        public int x;
        public int y;
        public int z;
        public string dir; // East, West, North, South, Up, Down
    }

    [Serializable]
    public class PieceData
    {
        public int x;
        public int y;
        public int z;
        public string type;  // Plane, Lift, Splitter
        public string state; // "/", "\\", XRise, ZRise, XFall, ZFall
    }

    [Serializable]
    public class InventoryData
    {
        public int plane;
        public int lift;
        public int splitter;
    }
}
