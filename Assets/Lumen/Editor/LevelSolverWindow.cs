using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Lumen.Core;
using UnityEditor;
using UnityEngine;

namespace Lumen.EditorTools
{
    /// <summary>
    /// 메뉴 LUMEN → Level Solver. Resources/Levels의 모든 레벨을 솔버로 풀고 난이도 지표를 표로 보여 준다.
    /// 경고: 풀 수 없음, JSON의 solverMinPlacements와 솔버 최소 배치 수 불일치, 거울 없이 풀림,
    /// 8레벨 이후 해 3개 초과, 탐색 노드 상한 도달.
    /// 같은 검사를 EditMode 테스트 T27도 한다. 이 창은 레벨을 만들며 지표를 바로 보기 위한 도구다.
    /// </summary>
    public sealed class LevelSolverWindow : EditorWindow
    {
        private const string LevelFolder = "Assets/Lumen/Resources/Levels";
        private const int MaxShownSolutions = 20;

        private readonly List<Row> _rows = new List<Row>();
        private readonly HashSet<string> _expanded = new HashSet<string>();
        private Vector2 _scroll;

        [MenuItem("LUMEN/Level Solver")]
        public static void Open()
        {
            var window = GetWindow<LevelSolverWindow>("Level Solver");
            window.minSize = new Vector2(820, 300);
            window.SolveAll();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Solve all", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    SolveAll();
                }

                if (GUILayout.Button("Copy report", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    EditorGUIUtility.systemCopyBuffer = Report();
                    ShowNotification(new GUIContent("Report copied"));
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_rows.Count} levels in {LevelFolder}", EditorStyles.miniLabel);
            }

            Header();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var row in _rows)
            {
                DrawRow(row);
            }

            EditorGUILayout.EndScrollView();
        }

        private void SolveAll()
        {
            _rows.Clear();
            if (!Directory.Exists(LevelFolder))
            {
                return;
            }

            var files = Directory.GetFiles(LevelFolder, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                _rows.Add(SolveFile(file));
            }

            Repaint();
        }

        private static Row SolveFile(string file)
        {
            var row = new Row { File = Path.GetFileName(file) };
            try
            {
                row.Definition = LevelDefinition.FromData(JsonUtility.FromJson<LevelData>(File.ReadAllText(file)));
            }
            catch (Exception e)
            {
                row.Warnings.Add("Load failed: " + e.Message);
                return row;
            }

            var watch = Stopwatch.StartNew();
            row.Result = Solver.Solve(row.Definition);
            row.Milliseconds = watch.ElapsedMilliseconds;

            var r = row.Result;
            var d = row.Definition;
            if (r.HitNodeLimit) row.Warnings.Add($"Node limit hit ({Solver.DefaultNodeLimit}). Results are incomplete.");
            if (!r.Solvable) row.Warnings.Add("Unsolvable.");
            else
            {
                if (r.MinPlacements == 0) row.Warnings.Add("Solvable without placing any mirror.");
                if (r.MinPlacements != d.SolverMinPlacements)
                    row.Warnings.Add($"solverMinPlacements in JSON is {d.SolverMinPlacements}, solver says {r.MinPlacements}.");
                if (LevelNumber(d.Id) >= 8 && r.SolutionCount > 3)
                    row.Warnings.Add($"Level 8+ must have 3 solutions or fewer (has {r.SolutionCount}).");
            }

            if (r.AnyTruncated) row.Warnings.Add("Some placements hit the beam step limit (looping beams).");
            return row;
        }

        private static readonly float[] Widths = { 110, 120, 60, 50, 90, 80, 70, 60, 60, 90, 60 };

        private static void Header()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string[] titles =
                {
                    "File", "Name", "Size", "Peds", "Inventory P/L/S", "Min json/solver", "Solutions", "Floors",
                    "Near miss", "Search space", "ms"
                };
                for (var i = 0; i < titles.Length; i++)
                {
                    GUILayout.Label(titles[i], EditorStyles.boldLabel, GUILayout.Width(Widths[i]));
                }
            }
        }

        private void DrawRow(Row row)
        {
            var bad = row.Warnings.Count > 0;
            var previous = GUI.color;
            GUI.color = bad ? new Color(1f, 0.75f, 0.7f) : previous;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var open = _expanded.Contains(row.File);
                var nowOpen = EditorGUILayout.Foldout(open, row.File, true);
                if (nowOpen != open)
                {
                    if (nowOpen) _expanded.Add(row.File);
                    else _expanded.Remove(row.File);
                }

                if (row.Result != null)
                {
                    var d = row.Definition;
                    var r = row.Result;
                    Cell(d.Name, 1);
                    Cell($"{d.Size.X}x{d.Size.Z}x{d.Size.Y}", 2);
                    Cell(d.Pedestals.Count.ToString(), 3);
                    Cell($"{d.InventoryOf(PieceType.PlaneMirror)} / {d.InventoryOf(PieceType.LiftMirror)} / {d.InventoryOf(PieceType.Splitter)}", 4);
                    Cell($"{d.SolverMinPlacements} / {r.MinPlacements}", 5);
                    Cell($"{r.SolutionCount} ({r.MinSolutionCount} min)", 6);
                    Cell(r.FloorChanges.ToString(), 7);
                    Cell(r.NearMisses.ToString(), 8);
                    Cell(r.SearchSpace.ToString("N0"), 9);
                    Cell(row.Milliseconds.ToString(), 10);
                }
            }

            GUI.color = previous;

            foreach (var warning in row.Warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            if (_expanded.Contains(row.File) && row.Result != null)
            {
                EditorGUI.indentLevel++;
                var solutions = row.Result.Solutions;
                for (var i = 0; i < solutions.Count && i < MaxShownSolutions; i++)
                {
                    EditorGUILayout.LabelField($"[{solutions[i].Count}] floors {solutions[i].FloorChanges}", solutions[i].ToString());
                }

                if (solutions.Count > MaxShownSolutions)
                {
                    EditorGUILayout.LabelField($"... and {solutions.Count - MaxShownSolutions} more");
                }

                EditorGUI.indentLevel--;
            }
        }

        private static void Cell(string text, int column) => GUILayout.Label(text, GUILayout.Width(Widths[column]));

        private string Report()
        {
            var text = new StringBuilder();
            foreach (var row in _rows)
            {
                text.AppendLine(row.Result != null ? Solver.Describe(row.Definition, row.Result) : row.File);
                foreach (var warning in row.Warnings)
                {
                    text.AppendLine("  ! " + warning);
                }
            }

            return text.ToString();
        }

        private static int LevelNumber(string id)
        {
            var digits = new StringBuilder();
            foreach (var c in id)
            {
                if (char.IsDigit(c)) digits.Append(c);
            }

            return digits.Length > 0 ? int.Parse(digits.ToString()) : 0;
        }

        private sealed class Row
        {
            public string File;
            public LevelDefinition Definition;
            public SolverResult Result;
            public long Milliseconds;
            public readonly List<string> Warnings = new List<string>();
        }
    }
}
