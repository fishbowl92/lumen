namespace Lumen.Core.Tests
{
    /// <summary>테스트용 레벨 데이터. Level01Data·Level05Data는 Assets/Lumen/Resources/Levels의 같은 이름 파일과 같은 내용이다.</summary>
    internal static class TestLevels
    {
        public static LevelData Level05Data() => new LevelData
        {
            id = "level05",
            name = "위층으로",
            sizeX = 3,
            sizeY = 2,
            sizeZ = 3,
            sources = new[] { new SourceData { x = 0, y = 0, z = 1, dir = "East" } },
            crystals = new[] { new CellData { x = 0, y = 1, z = 1 } },
            walls = new CellData[0],
            fixedPieces = new[] { new PieceData { x = 2, y = 1, z = 1, type = "Lift", state = "XFall" } },
            pedestals = new[] { new CellData { x = 2, y = 0, z = 1 }, new CellData { x = 1, y = 0, z = 2 } },
            inventory = new InventoryData { plane = 1, lift = 1, splitter = 0 },
            solverMinPlacements = 1,
            solverMinActions = 1
        };

        public static LevelData Level01Data() => new LevelData
        {
            id = "level01",
            name = "첫 빛",
            sizeX = 3,
            sizeY = 1,
            sizeZ = 3,
            sources = new[] { new SourceData { x = 0, y = 0, z = 1, dir = "East" } },
            crystals = new[] { new CellData { x = 1, y = 0, z = 2 } },
            walls = new CellData[0],
            fixedPieces = new PieceData[0],
            pedestals = new[] { new CellData { x = 1, y = 0, z = 1 }, new CellData { x = 2, y = 0, z = 1 } },
            inventory = new InventoryData { plane = 1, lift = 0, splitter = 0 },
            solverMinPlacements = 1,
            solverMinActions = 1
        };

        public static LevelSession Level05() => new LevelSession(LevelDefinition.FromData(Level05Data()));
    }
}
