using System.Collections.Generic;
using Lumen.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumen.Game
{
    /// <summary>
    /// 게임 진행·입력·HUD. 입력은 Input System 패키지만 쓴다(프로젝트 설정이 Input System 전용).
    /// HUD는 IMGUI(OnGUI)로 그린다. 웹 빌드의 기본 글꼴에는 한글이 없어 화면 글자는 영어로 둔다.
    /// </summary>
    public sealed class LumenGame : MonoBehaviour
    {
        private const float TopBarHeight = 44f;
        private const float BottomBarHeight = 56f;
        private const float DragThreshold = 6f;

        private static readonly PieceType[] SelectableTypes =
            { PieceType.PlaneMirror, PieceType.LiftMirror, PieceType.Splitter };

        private readonly Dictionary<string, int> _bestStars = new Dictionary<string, int>();

        private List<LevelDefinition> _levels;
        private int _levelIndex;
        private LevelSession _session;
        private BoardView _board;
        private CameraRig _rig;
        private PieceType _selected = PieceType.PlaneMirror;
        private bool _showLevelList;
        private Vector2 _rightPressPosition;
        private bool _rightDragging;
        private string _message;

        private void Start()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            }

            _rig = new CameraRig(camera);
            _board = new BoardView(transform);
            _levels = LevelCatalog.LoadAll();

            if (_levels.Count == 0)
            {
                _message = "No levels found in Resources/Levels.";
                return;
            }

            LoadLevel(0);
        }

        private void LoadLevel(int index)
        {
            _levelIndex = index;
            _session = new LevelSession(_levels[index]);
            _showLevelList = false;
            _board.Build(_session);

            var size = _session.Definition.Size;
            var extent = new Vector3(size.X, size.Y, size.Z);
            _rig.Frame((extent - Vector3.one) * 0.5f, extent);
            SelectFirstAvailable();
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            HandleKeyboard(Keyboard.current);
            HandleMouse(Mouse.current);
        }

        private void HandleKeyboard(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.zKey.wasPressedThisFrame && _session.Undo())
            {
                Changed();
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                _session.Reset();
                Changed();
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                _board.CycleFloor();
            }

            if (keyboard.digit1Key.wasPressedThisFrame) Select(PieceType.PlaneMirror);
            if (keyboard.digit2Key.wasPressedThisFrame) Select(PieceType.LiftMirror);
            if (keyboard.digit3Key.wasPressedThisFrame) Select(PieceType.Splitter);
        }

        private void HandleMouse(Mouse mouse)
        {
            if (mouse == null)
            {
                return;
            }

            var position = mouse.position.ReadValue();

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverHud(position))
            {
                _rig.Zoom(scroll);
            }

            // 우클릭: 끌면 카메라 회전, 그냥 떼면 거울 회수
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _rightPressPosition = position;
                _rightDragging = false;
            }

            if (mouse.rightButton.isPressed)
            {
                if (!_rightDragging && (position - _rightPressPosition).magnitude > DragThreshold)
                {
                    _rightDragging = true;
                }

                if (_rightDragging)
                {
                    _rig.Orbit(mouse.delta.ReadValue());
                }
            }

            if (mouse.rightButton.wasReleasedThisFrame && !_rightDragging && !IsOverHud(position) &&
                TryPickCell(position, out var removeCell) && _session.TryRemove(removeCell))
            {
                Changed();
            }

            // 좌클릭: 내 거울이면 회전, 빈 받침대면 선택한 거울 배치
            if (mouse.leftButton.wasPressedThisFrame && !IsOverHud(position) && TryPickCell(position, out var cell))
            {
                var changed = _session.IsPlayerPiece(cell)
                    ? _session.TryRotate(cell)
                    : PlaceSelected(cell);
                if (changed)
                {
                    Changed();
                }
            }
        }

        private bool PlaceSelected(Int3 cell)
        {
            if (_session.Remaining(_selected) == 0)
            {
                SelectFirstAvailable();
            }

            return _session.TryPlace(cell, _selected);
        }

        private bool TryPickCell(Vector2 screenPosition, out Int3 cell)
        {
            cell = default;
            var ray = Camera.main.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit, 200f) && hit.collider.TryGetComponent<CellTag>(out var tag))
            {
                cell = tag.Position;
                return true;
            }

            return false;
        }

        private void Changed()
        {
            _board.Refresh(_session);
            if (_session.IsCleared)
            {
                var id = _session.Definition.Id;
                _bestStars.TryGetValue(id, out var best);
                _bestStars[id] = Mathf.Max(best, _session.Stars);
            }

            if (_session.Remaining(_selected) == 0)
            {
                SelectFirstAvailable();
            }
        }

        private void Select(PieceType type)
        {
            if (_session != null && _session.Remaining(type) > 0)
            {
                _selected = type;
            }
        }

        private void SelectFirstAvailable()
        {
            foreach (var type in SelectableTypes)
            {
                if (_session.Remaining(type) > 0)
                {
                    _selected = type;
                    return;
                }
            }
        }

        private bool IsOverHud(Vector2 screenPosition)
        {
            // Input System은 왼쪽 아래가 원점, IMGUI는 왼쪽 위가 원점이다.
            if (screenPosition.y > Screen.height - TopBarHeight || screenPosition.y < BottomBarHeight)
            {
                return true;
            }

            return _showLevelList || (_session != null && _session.IsCleared);
        }

        private void OnGUI()
        {
            var label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            var big = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 14 };

            if (_session == null)
            {
                GUI.Label(new Rect(20, 20, 600, 40), _message ?? "Loading...", label);
                return;
            }

            var definition = _session.Definition;

            // 상단 바: 레벨, 배치 수
            GUI.Box(new Rect(0, 0, Screen.width, TopBarHeight), GUIContent.none);
            GUI.Label(new Rect(16, 0, 360, TopBarHeight),
                $"LEVEL {_levelIndex + 1} / {_levels.Count}   ({definition.Id})", label);
            GUI.Label(new Rect(Screen.width * 0.5f - 120, 0, 300, TopBarHeight),
                $"Mirrors used {_session.EffectivePlacedCount}  /  Best {definition.SolverMinPlacements}", label);
            if (GUI.Button(new Rect(Screen.width - 110, 7, 96, 30), "Levels", button))
            {
                _showLevelList = !_showLevelList;
            }

            // 하단 바: 인벤토리와 조작 버튼
            var barY = Screen.height - BottomBarHeight;
            GUI.Box(new Rect(0, barY, Screen.width, BottomBarHeight), GUIContent.none);
            var x = 12f;
            foreach (var type in SelectableTypes)
            {
                var count = _session.Remaining(type);
                var text = $"{(type == _selected ? "> " : "")}{ShortName(type)}  {count}";
                GUI.enabled = count > 0 && !_session.IsCleared;
                if (GUI.Button(new Rect(x, barY + 10, 132, 36), text, button))
                {
                    Select(type);
                }

                x += 140f;
            }

            GUI.enabled = _session.CanUndo;
            if (GUI.Button(new Rect(Screen.width - 330, barY + 10, 100, 36), "Undo (Z)", button) && _session.Undo())
            {
                Changed();
            }

            GUI.enabled = true;
            if (GUI.Button(new Rect(Screen.width - 222, barY + 10, 100, 36), "Reset (R)", button))
            {
                _session.Reset();
                Changed();
            }

            var floorText = _board.VisibleFloor < 0 ? "Floors: All" : $"Floor {_board.VisibleFloor + 1}";
            if (GUI.Button(new Rect(Screen.width - 114, barY + 10, 104, 36), floorText + " (Tab)", button))
            {
                _board.CycleFloor();
            }

            GUI.Label(new Rect(16, barY - 26, Screen.width - 32, 24),
                "Left click: place / rotate    Right click: remove    Right drag: orbit    Wheel: zoom",
                new GUIStyle(label) { fontSize = 12 });

            if (_session.IsCleared)
            {
                DrawClearPanel(big, label, button);
            }

            if (_showLevelList)
            {
                DrawLevelList(label, button);
            }
        }

        private void DrawClearPanel(GUIStyle big, GUIStyle label, GUIStyle button)
        {
            var rect = new Rect(Screen.width * 0.5f - 170, Screen.height * 0.5f - 110, 340, 200);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x, rect.y + 16, rect.width, 44), "CLEAR!", big);
            var stars = _session.Stars;
            GUI.Label(new Rect(rect.x, rect.y + 62, rect.width, 36),
                new string('*', stars) + new string('-', 3 - stars) + $"   {stars} / 3 stars",
                new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 });

            var hasNext = _levelIndex + 1 < _levels.Count;
            if (GUI.Button(new Rect(rect.x + 20, rect.y + 130, 140, 44), "Retry", button))
            {
                _session.Reset();
                Changed();
            }

            if (GUI.Button(new Rect(rect.x + 180, rect.y + 130, 140, 44), hasNext ? "Next level" : "All clear!", button) &&
                hasNext)
            {
                LoadLevel(_levelIndex + 1);
            }
        }

        private void DrawLevelList(GUIStyle label, GUIStyle button)
        {
            var height = 60 + _levels.Count * 40;
            var rect = new Rect(Screen.width - 260, TopBarHeight + 8, 250, height);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 12, rect.y + 8, 220, 30), "Levels", label);
            for (var i = 0; i < _levels.Count; i++)
            {
                _bestStars.TryGetValue(_levels[i].Id, out var best);
                var text = $"{i + 1}. {_levels[i].Id}   {new string('*', best)}";
                if (GUI.Button(new Rect(rect.x + 12, rect.y + 44 + i * 40, 226, 34), text, button))
                {
                    LoadLevel(i);
                }
            }
        }

        private static string ShortName(PieceType type)
        {
            switch (type)
            {
                case PieceType.PlaneMirror: return "1 Mirror";
                case PieceType.LiftMirror: return "2 Lift";
                default: return "3 Splitter";
            }
        }
    }
}
