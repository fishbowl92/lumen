using System;
using System.Collections.Generic;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Game
{
    /// <summary>Resources/Levels 안의 JSON을 파일 이름순(level01, level02…)으로 읽는다.</summary>
    public static class LevelCatalog
    {
        public const string ResourceFolder = "Levels";

        public static List<LevelDefinition> LoadAll()
        {
            var assets = Resources.LoadAll<TextAsset>(ResourceFolder);
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
