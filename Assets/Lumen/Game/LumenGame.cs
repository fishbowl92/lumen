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
        private const float ClearPanelDelay = 1.2f; // 클리어 연출을 보여 준 뒤 결과 창을 띄운다
        private const float ClearPanelFade = 0.25f;

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
        private HudSkin _skin;
        private float _clearedAt = -1f;

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
            _clearedAt = -1f;
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

            _board.Tick(Time.deltaTime);
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
                if (_clearedAt < 0f)
                {
                    _clearedAt = Time.time;
                    _board.PlayClearEffect();
                }
            }
            else
            {
                _clearedAt = -1f;
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
            _skin ??= new HudSkin();
            var skin = _skin;

            if (_session == null)
            {
                GUI.Label(new Rect(20, 20, 600, 40), _message ?? "Loading...", skin.Label);
                return;
            }

            var definition = _session.Definition;
            var width = Screen.width;

            // 상단 바: 레벨 번호, 배치 수, 레벨 목록
            GUI.Box(new Rect(0, 0, width, TopBarHeight), GUIContent.none, skin.Bar);
            GUI.Label(new Rect(16, 0, 260, TopBarHeight), $"LEVEL {_levelIndex + 1} / {_levels.Count}", skin.Label);
            GUI.Label(new Rect(width * 0.5f - 160, 0, 320, TopBarHeight),
                $"Moves  {_session.MoveCount}   ·   Par  {definition.SolverMinActions}", skin.LabelCenter);
            if (GUI.Button(new Rect(width - 112, 7, 100, 30), _showLevelList ? "Close" : "Levels",
                    _showLevelList ? skin.Selected : skin.Button))
            {
                _showLevelList = !_showLevelList;
            }

            // 하단 바: 왼쪽은 거울 선택(선택한 것은 주황), 오른쪽은 되돌리기·리셋·층 보기
            var barY = Screen.height - BottomBarHeight;
            var buttonY = barY + 10;
            const float ButtonHeight = 36f;
            GUI.Box(new Rect(0, barY, width, BottomBarHeight), GUIContent.none, skin.Bar);

            var x = 12f;
            for (var i = 0; i < SelectableTypes.Length; i++)
            {
                var type = SelectableTypes[i];
                var count = _session.Remaining(type);
                GUI.enabled = count > 0 && !_session.IsCleared;
                var style = type == _selected && GUI.enabled ? skin.Selected : skin.Button;
                if (GUI.Button(new Rect(x, buttonY, 144, ButtonHeight), $"[{i + 1}]  {PieceName(type)}  x{count}", style))
                {
                    Select(type);
                }

                x += 152f;
            }

            var right = width - 12f;
            var floorText = _board.VisibleFloor < 0 ? "Floor: All" : $"Floor: {_board.VisibleFloor + 1}";
            right -= 156f;
            GUI.enabled = true;
            if (GUI.Button(new Rect(right, buttonY, 156, ButtonHeight), floorText + "  [Tab]",
                    _board.VisibleFloor < 0 ? skin.Button : skin.Selected))
            {
                _board.CycleFloor();
            }

            right -= 120f;
            if (GUI.Button(new Rect(right, buttonY, 112, ButtonHeight), "Reset  [R]", skin.Button))
            {
                _session.Reset();
                Changed();
            }

            right -= 120f;
            GUI.enabled = _session.CanUndo;
            if (GUI.Button(new Rect(right, buttonY, 112, ButtonHeight), "Undo  [Z]", skin.Button) && _session.Undo())
            {
                Changed();
            }

            GUI.enabled = true;
            GUI.Label(new Rect(16, barY - 24, width - 32, 22),
                "Left click: place / rotate     Right click: remove     Right drag: orbit     Wheel: zoom", skin.Hint);

            if (_session.IsCleared && _clearedAt >= 0f)
            {
                var shown = Time.time - _clearedAt - ClearPanelDelay;
                if (shown >= 0f)
                {
                    var previous = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(shown / ClearPanelFade));
                    DrawClearPanel(skin);
                    GUI.color = previous;
                }
            }

            if (_showLevelList)
            {
                DrawLevelList(skin);
            }
        }

        private void DrawClearPanel(HudSkin skin)
        {
            var rect = new Rect(Screen.width * 0.5f - 170, Screen.height * 0.5f - 110, 340, 200);
            GUI.Box(rect, GUIContent.none, skin.Panel);
            GUI.Label(new Rect(rect.x, rect.y + 14, rect.width, 44), "CLEAR!", skin.Title);
            DrawStars(skin, new Vector2(rect.center.x, rect.y + 82), _session.Stars, 40f, 8f);
            GUI.Label(new Rect(rect.x, rect.y + 102, rect.width, 22),
                $"Moves {_session.MoveCount}   ·   Par {_session.Definition.SolverMinActions}", skin.HintCenter);

            var hasNext = _levelIndex + 1 < _levels.Count;
            if (GUI.Button(new Rect(rect.x + 20, rect.y + 130, 140, 44), "Retry", skin.Button))
            {
                _session.Reset();
                Changed();
            }

            if (GUI.Button(new Rect(rect.x + 180, rect.y + 130, 140, 44), hasNext ? "Next level" : "All clear!",
                    skin.Selected) && hasNext)
            {
                LoadLevel(_levelIndex + 1);
            }
        }

        private void DrawLevelList(HudSkin skin)
        {
            // 레벨 10개가 960×540 화면의 하단 바 위에 들어가도록 줄 간격을 34로 둔다.
            var height = 52 + _levels.Count * 34;
            var rect = new Rect(Screen.width - 262, TopBarHeight + 8, 250, height);
            GUI.Box(rect, GUIContent.none, skin.Panel);
            GUI.Label(new Rect(rect.x + 14, rect.y + 8, 220, 30), "Levels", skin.Label);
            for (var i = 0; i < _levels.Count; i++)
            {
                _bestStars.TryGetValue(_levels[i].Id, out var best);
                var style = i == _levelIndex ? skin.Selected : skin.Button;
                var row = new Rect(rect.x + 12, rect.y + 44 + i * 34, 226, 30);
                if (GUI.Button(row, GUIContent.none, style))
                {
                    LoadLevel(i);
                }

                GUI.Label(new Rect(row.x + 14, row.y, 60, row.height), $"{i + 1}", i == _levelIndex ? skin.LabelDark : skin.Label);
                if (best > 0)
                {
                    DrawStars(skin, new Vector2(row.xMax - 48, row.center.y), best, 18f, 3f);
                }
            }
        }

        /// <summary>별을 가운데 정렬로 그린다. 얻은 별은 채운 별, 나머지는 빈 별(글꼴에 ★가 없어 그림으로 그린다).</summary>
        private static void DrawStars(HudSkin skin, Vector2 center, int earned, float size, float gap)
        {
            var total = 3 * size + 2 * gap;
            var x = center.x - total * 0.5f;
            for (var i = 0; i < 3; i++)
            {
                GUI.DrawTexture(new Rect(x, center.y - size * 0.5f, size, size), i < earned ? skin.StarFilled : skin.StarEmpty);
                x += size + gap;
            }
        }

        private static string PieceName(PieceType type)
        {
            switch (type)
            {
                case PieceType.PlaneMirror: return "Mirror";
                case PieceType.LiftMirror: return "Lift";
                default: return "Splitter";
            }
        }
    }
}
