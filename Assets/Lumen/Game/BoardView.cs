using System.Collections.Generic;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Game
{
    /// <summary>
    /// LevelSession을 기본 도형으로 그린다. 바닥·벽·광원·크리스털·받침대는 레벨을 열 때 한 번,
    /// 거울과 광선은 세션이 바뀔 때마다(Refresh) 다시 만든다. 격자가 작아 매번 다시 만들어도 가볍다.
    /// 칸 (x, y, z)의 중심은 월드 좌표 (x, y, z)다.
    /// </summary>
    public sealed class BoardView
    {
        private static readonly Color FloorColor = new Color(0.22f, 0.24f, 0.30f);
        private static readonly Color FloorMarkerColor = new Color(0.35f, 0.38f, 0.46f);
        private static readonly Color PedestalColor = new Color(0.25f, 0.55f, 0.95f);
        private static readonly Color WallColor = new Color(0.45f, 0.45f, 0.48f);
        private static readonly Color SourceColor = new Color(1.00f, 0.78f, 0.25f);
        private static readonly Color CrystalOffColor = new Color(0.78f, 0.27f, 0.30f); // 꺼짐: 붉은색(바닥과 구분)
        private static readonly Color CrystalOnColor = new Color(0.45f, 0.95f, 1.00f);
        private static readonly Color PlayerMirrorColor = new Color(1.00f, 0.50f, 0.25f);
        private static readonly Color FixedMirrorColor = new Color(0.50f, 0.36f, 0.85f);       // 고정 거울: 보라
        private static readonly Color FixedMirrorActiveColor = new Color(0.86f, 0.74f, 1.00f); // 빛을 꺾는 중: 밝은 보라
        private static readonly Color SplitterColor = new Color(0.55f, 0.90f, 0.75f);
        private static readonly Color BeamColor = new Color(1.00f, 0.95f, 0.55f);

        private const float BeamThickness = 0.08f;
        private const string BaseMaterialPath = "Materials/LumenBase";

        private readonly Transform _root;
        private readonly Material _baseMaterial;
        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        private readonly List<(GameObject Go, int Floor)> _floorObjects = new List<(GameObject, int)>();
        private readonly Dictionary<Int3, Renderer> _crystals = new Dictionary<Int3, Renderer>();

        private Transform _staticRoot;
        private Transform _dynamicRoot;

        // 클리어 연출: 크리스털이 돌며 커졌다 작아지고, 빛 알갱이가 튀고, 광선이 한 번 굵어진다.
        private const float EffectDuration = 1.2f;
        private readonly List<Transform> _beams = new List<Transform>();
        private readonly List<(Transform T, Vector3 Velocity)> _sparks = new List<(Transform, Vector3)>();
        private float _effectTime = -1f;
        private int _floorCount = 1;

        public BoardView(Transform parent)
        {
            _root = new GameObject("Board").transform;
            _root.SetParent(parent, false);

            // 런타임에 만든 기본 도형은 빌드에서 내장 파이프라인용 기본 머티리얼을 받아 URP에서 분홍색으로 나온다.
            // (에디터에서는 URP 기본 머티리얼을 받아 정상으로 보여 문제가 드러나지 않는다.)
            // Resources 폴더의 에셋은 빌드에 반드시 들어가므로, 거기 둔 URP Lit 머티리얼을 복사해 색만 바꿔 쓴다.
            _baseMaterial = Resources.Load<Material>(BaseMaterialPath);
            if (_baseMaterial == null)
            {
                Debug.LogError($"Missing material Resources/{BaseMaterialPath}.mat (URP Lit). Objects may render pink.");
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _baseMaterial = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }
        }

        /// <summary>-1이면 모든 층, 0 이상이면 그 층만 보인다.</summary>
        public int VisibleFloor { get; private set; } = -1;

        public static Vector3 ToWorld(Int3 cell) => new Vector3(cell.X, cell.Y, cell.Z);

        public static Vector3 ToVector(Direction direction)
        {
            var o = direction.ToOffset();
            return new Vector3(o.X, o.Y, o.Z);
        }

        /// <summary>레벨을 처음 열 때 정적인 부분을 만든다.</summary>
        public void Build(LevelSession session)
        {
            if (_staticRoot != null)
            {
                Object.Destroy(_staticRoot.gameObject);
            }

            if (_dynamicRoot != null)
            {
                Object.Destroy(_dynamicRoot.gameObject);
            }

            _floorObjects.Clear();
            _crystals.Clear();
            StopEffect();
            _staticRoot = NewChild("Static");
            _dynamicRoot = null;

            var definition = session.Definition;
            var size = definition.Size;
            _floorCount = size.Y;
            VisibleFloor = -1;

            for (var y = 0; y < size.Y; y++)
            {
                for (var x = 0; x < size.X; x++)
                {
                    for (var z = 0; z < size.Z; z++)
                    {
                        var cell = new Int3(x, y, z);
                        if (definition.IsPedestal(cell))
                        {
                            var tile = Box(_staticRoot, "Pedestal " + cell, ToWorld(cell) + Vector3.down * 0.45f,
                                new Vector3(0.9f, 0.1f, 0.9f), Quaternion.identity, PedestalColor, true);
                            tile.AddComponent<CellTag>().Set(cell);
                            Register(tile, y);
                        }
                        else if (y == 0)
                        {
                            Register(Box(_staticRoot, "Floor", ToWorld(cell) + Vector3.down * 0.47f,
                                new Vector3(0.94f, 0.06f, 0.94f), Quaternion.identity, FloorColor, false), y);
                        }
                        else
                        {
                            // 위층은 유리 바닥: 시야를 가리지 않도록 칸 중심 아래에 작은 표식만 둔다.
                            Register(Box(_staticRoot, "FloorMarker", ToWorld(cell) + Vector3.down * 0.47f,
                                new Vector3(0.18f, 0.03f, 0.18f), Quaternion.identity, FloorMarkerColor, false), y);
                        }
                    }
                }
            }

            foreach (var wall in definition.Walls)
            {
                Register(Box(_staticRoot, "Wall", ToWorld(wall), Vector3.one * 0.95f, Quaternion.identity, WallColor, false),
                    wall.Y);
            }

            foreach (var source in definition.Sources)
            {
                var center = ToWorld(source.Position);
                Register(Box(_staticRoot, "Source", center, Vector3.one * 0.6f, Quaternion.identity, SourceColor, false),
                    source.Position.Y);
                Register(Box(_staticRoot, "SourceNozzle", center + ToVector(source.Direction) * 0.38f, Vector3.one * 0.25f,
                    Quaternion.identity, SourceColor, false), source.Position.Y);
            }

            foreach (var crystal in definition.Crystals)
            {
                var go = Box(_staticRoot, "Crystal", ToWorld(crystal), Vector3.one * 0.42f, Quaternion.Euler(45f, 0f, 45f),
                    CrystalOffColor, false);
                _crystals[crystal] = go.GetComponent<Renderer>();
                Register(go, crystal.Y);
            }

            Refresh(session);
        }

        /// <summary>거울·광선·크리스털 점등을 세션 상태에 맞춘다.</summary>
        public void Refresh(LevelSession session)
        {
            if (_dynamicRoot != null)
            {
                RemoveRegistered(_dynamicRoot);
                Object.Destroy(_dynamicRoot.gameObject);
            }

            _dynamicRoot = NewChild("Dynamic");
            _beams.Clear();
            StopEffect();
            var size = session.Definition.Size;

            for (var y = 0; y < size.Y; y++)
            {
                for (var x = 0; x < size.X; x++)
                {
                    for (var z = 0; z < size.Z; z++)
                    {
                        var cell = new Int3(x, y, z);
                        if (session.Grid.GetKind(cell) == CellKind.Piece && session.Grid.TryGetPiece(cell, out var piece))
                        {
                            DrawPiece(cell, piece, session.IsPlayerPiece(cell), session.LastTrace.IsDeflecting(cell));
                        }
                    }
                }
            }

            foreach (var segment in session.LastTrace.Segments)
            {
                DrawBeam(segment);
            }

            foreach (var pair in _crystals)
            {
                var lit = session.LastTrace.IsLit(pair.Key);
                pair.Value.sharedMaterial = MaterialFor(lit ? CrystalOnColor : CrystalOffColor);
                pair.Value.transform.localScale = Vector3.one * (lit ? 0.52f : 0.42f);
            }

            ApplyFloorVisibility();
        }

        /// <summary>층 보기: 전체 → 1층 → 2층 … → 전체.</summary>
        public void CycleFloor()
        {
            VisibleFloor = VisibleFloor + 1 >= _floorCount ? -1 : VisibleFloor + 1;
            ApplyFloorVisibility();
        }

        private void DrawPiece(Int3 cell, Piece piece, bool isPlayerPiece, bool isDeflecting)
        {
            Vector3 scale;
            Quaternion rotation;
            switch (piece.Type)
            {
                case PieceType.LiftMirror:
                    // 얇은 판(y가 두께)을 수직면에서 45° 기울인다. 반사 표와 같은 방향이 되도록 맞춘 각도.
                    scale = new Vector3(0.9f, 0.06f, 0.9f);
                    rotation = LiftRotation(piece.State);
                    break;
                default:
                    // 평면 거울·분할기: 세운 판(z가 두께)을 y축으로 ±45° 돌린다. '/'는 남서-북동 방향.
                    scale = new Vector3(1.0f, 0.8f, piece.Type == PieceType.Splitter ? 0.04f : 0.07f);
                    rotation = Quaternion.Euler(0f, piece.State == PieceState.Slash ? -45f : 45f, 0f);
                    break;
            }

            // 고정 거울은 빛을 꺾거나 나누는 동안 밝게 바뀐다(어디서 빛이 꺾였는지 보이게).
            var color = !isPlayerPiece ? (isDeflecting ? FixedMirrorActiveColor : FixedMirrorColor) :
                piece.Type == PieceType.Splitter ? SplitterColor : PlayerMirrorColor;
            var go = Box(_dynamicRoot, piece.ToString(), ToWorld(cell), scale, rotation, color, isPlayerPiece);
            if (isPlayerPiece)
            {
                go.AddComponent<CellTag>().Set(cell);
            }

            Register(go, cell.Y);
        }

        private static Quaternion LiftRotation(PieceState state)
        {
            switch (state)
            {
                case PieceState.XRise: return Quaternion.Euler(0f, 0f, 45f);  // 동→위
                case PieceState.XFall: return Quaternion.Euler(0f, 0f, -45f); // 동→아래
                case PieceState.ZRise: return Quaternion.Euler(-45f, 0f, 0f); // 북→위
                default: return Quaternion.Euler(45f, 0f, 0f);                // ZFall: 북→아래
            }
        }

        private void DrawBeam(BeamSegment segment)
        {
            var direction = ToVector(segment.Direction);
            var start = ToWorld(segment.From);
            var end = ToWorld(segment.To);
            if (segment.End == SegmentEnd.Blocked || segment.End == SegmentEnd.OutOfBounds)
            {
                end -= direction * 0.5f; // 벽 면·격자 경계에서 멈춘 것처럼 보이게
            }

            var length = Vector3.Distance(start, end);
            if (length < 0.01f)
            {
                return;
            }

            var up = segment.Direction.IsVertical() ? Vector3.forward : Vector3.up;
            var beam = Box(_dynamicRoot, "Beam", (start + end) * 0.5f, new Vector3(BeamThickness, BeamThickness, length),
                Quaternion.LookRotation(direction, up), BeamColor, false);
            _beams.Add(beam.transform);
            // 광선은 층을 가로지르므로 층 보기와 상관없이 항상 보인다.
        }

        /// <summary>클리어 연출을 시작한다. 이후 Tick이 1.2초 동안 움직인다.</summary>
        public void PlayClearEffect()
        {
            StopEffect();
            _effectTime = 0f;
            foreach (var pair in _crystals)
            {
                var center = pair.Value.transform.localPosition;
                for (var i = 0; i < 18; i++)
                {
                    var spark = Box(_dynamicRoot, "Spark", center, Vector3.one * 0.09f, Random.rotation,
                        i % 3 == 0 ? BeamColor : CrystalOnColor, false);
                    var velocity = Random.onUnitSphere * Random.Range(1.6f, 3.2f) + Vector3.up * 1.2f;
                    _sparks.Add((spark.transform, velocity));
                }
            }
        }

        /// <summary>매 프레임 호출. 연출 중이 아니면 아무것도 하지 않는다.</summary>
        public void Tick(float deltaTime)
        {
            if (_effectTime < 0f)
            {
                return;
            }

            _effectTime += deltaTime;
            var t = Mathf.Clamp01(_effectTime / EffectDuration);

            // 크리스털: 한 바퀴 돌며 크게 튀었다가 점등 크기로 돌아온다
            var pulse = 1f + 0.45f * Mathf.Sin(t * Mathf.PI) * (1f - t);
            foreach (var pair in _crystals)
            {
                var crystal = pair.Value.transform;
                crystal.localRotation = Quaternion.Euler(45f, 45f + t * 360f, 45f);
                crystal.localScale = Vector3.one * 0.52f * pulse;
            }

            // 광선: 처음 0.4초 동안 한 번 굵어졌다 돌아온다
            var flash = 1f + 1.5f * Mathf.Sin(Mathf.Clamp01(_effectTime / 0.4f) * Mathf.PI);
            foreach (var beam in _beams)
            {
                if (beam != null)
                {
                    var scale = beam.localScale;
                    beam.localScale = new Vector3(BeamThickness * flash, BeamThickness * flash, scale.z);
                }
            }

            // 빛 알갱이: 퍼지며 떨어지고 작아진다
            for (var i = 0; i < _sparks.Count; i++)
            {
                var (spark, velocity) = _sparks[i];
                if (spark == null)
                {
                    continue;
                }

                velocity += Vector3.down * (4f * deltaTime);
                spark.localPosition += velocity * deltaTime;
                spark.localScale = Vector3.one * (0.09f * (1f - t));
                _sparks[i] = (spark, velocity);
            }

            if (_effectTime >= EffectDuration)
            {
                StopEffect();
            }
        }

        private void StopEffect()
        {
            foreach (var (spark, _) in _sparks)
            {
                if (spark != null)
                {
                    Object.Destroy(spark.gameObject);
                }
            }

            _sparks.Clear();
            foreach (var beam in _beams)
            {
                if (beam != null)
                {
                    beam.localScale = new Vector3(BeamThickness, BeamThickness, beam.localScale.z);
                }
            }

            _effectTime = -1f;
        }

        private GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation,
            Color color, bool clickable)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            if (!clickable)
            {
                Object.Destroy(go.GetComponent<Collider>());
            }

            return go;
        }

        private Material MaterialFor(Color color)
        {
            if (!_materials.TryGetValue(color, out var material))
            {
                material = new Material(_baseMaterial) { color = color };
                _materials[color] = material;
            }

            return material;
        }

        private Transform NewChild(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(_root, false);
            return child;
        }

        private void Register(GameObject go, int floor) => _floorObjects.Add((go, floor));

        private void RemoveRegistered(Transform parent)
        {
            _floorObjects.RemoveAll(entry => entry.Go == null || entry.Go.transform.parent == parent);
        }

        private void ApplyFloorVisibility()
        {
            foreach (var entry in _floorObjects)
            {
                if (entry.Go != null)
                {
                    entry.Go.SetActive(VisibleFloor < 0 || entry.Floor == VisibleFloor);
                }
            }
        }
    }
}
