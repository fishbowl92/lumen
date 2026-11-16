using System;
using System.Collections.Generic;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Game
{
    /// <summary>
    /// Resources 아래 폴더의 레벨 JSON을 파일 이름순(level01, level02…)으로 읽는다.
    /// Fast Play는 Resources/Levels, Normal Play는 Resources/LevelsNormal.
    /// (Resources.LoadAll은 하위 폴더까지 읽으므로 Normal을 Levels 안에 두지 않는다.)
    /// </summary>
    public static class LevelCatalog
    {
        public const string FastFolder = "Levels";
        public const string NormalFolder = "LevelsNormal";

        public static List<LevelDefinition> LoadAll(string folder = FastFolder)
        {
            var assets = Resources.LoadAll<TextAsset>(folder);
            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));

            var levels = new List<LevelDefinition>();
            foreach (var asset in assets)
            {
                try
                {
                    var data = JsonUtility.FromJson<LevelData>(asset.text);
                    levels.Add(LevelDefinition.FromData(data));
                }
                catch (Exception e) when (e is FormatException || e is ArgumentException)
                {
                    Debug.LogError($"레벨 {asset.name}을(를) 읽지 못했다: {e.Message}");
                }
            }

            return levels;
        }
    }
}
