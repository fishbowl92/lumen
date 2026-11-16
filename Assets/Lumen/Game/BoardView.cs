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
        private static readonly Color GlowColor = new Color(1.00f, 1.00f, 0.92f);

        private const float BeamThickness = 0.08f;
        private const string BaseMaterialPath = "Materials/LumenBase";

        private readonly Transform _root;
        private readonly Material _baseMaterial;
        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        private readonly List<(GameObject Go, int Floor)> _floorObjects = new List<(GameObject, int)>();
        private readonly Dictionary<Int3, Renderer> _crystals = new Dictionary<Int3, Renderer>();

        private Transform _staticRoot;
        private Transform _dynamicRoot;

        // 클리어 연출(11/16 변경): 빛이 광원에서부터 경로를 따라 순서대로 밝게 지나가고,
        // 크리스털에 닿는 순간 그 크리스털이 돌며 반짝이고 빛 알갱이가 튄다.
        // 빛은 일정한 속도로 지나가므로 경로가 길면 연출도 길어진다(짧은 경로가 정답이라는 것을 몸으로 느끼게).
        private const float GlowSpeed = 12f;        // 초당 칸 수
        private const float MaxTravelTime = 2.6f;   // 아주 긴 경로도 이 시간 안에 끝난다
        private const float CrystalPulseTime = 0.8f;
        private const float GlowThickness = 0.16f;
        private readonly List<Transform> _beams = new List<Transform>();
        private readonly List<BeamInfo> _beamInfos = new List<BeamInfo>();
        private readonly List<Transform> _glows = new List<Transform>();
        private readonly Dictionary<Int3, float> _crystalArrival = new Dictionary<Int3, float>();
        private readonly HashSet<Int3> _burst = new HashSet<Int3>();
        // 클리어하면 벽과 받침대가 작아지며 사라져, 가려졌던 경로가 다 보이게 한다(11/16).
        private const float VanishTime = 0.35f;
        private readonly List<(Transform T, Vector3 Scale)> _vanishers = new List<(Transform, Vector3)>();
        private readonly List<(Transform T, Vector3 Velocity)> _sparks = new List<(Transform, Vector3)>();
        private float _effectTime = -1f;
        private float _glowSpeed = GlowSpeed;
        private float _effectDuration;
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

        /// <summary>클리어 연출 전체 길이(초). PlayClearEffect 뒤에 정해진다. 결과 창은 이 시간이 지난 뒤 뜬다.</summary>
        public float ClearEffectLength => _effectDuration;

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
            _vanishers.Clear();
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
                            _vanishers.Add((tile.transform, tile.transform.localScale));
                        }
                        else if (y == 0)
                        {
                            // 1층 바닥 타일도 클리어하면 벽·받침대와 함께 작아지며 사라진다.
                            var floor = Box(_staticRoot, "Floor", ToWorld(cell) + Vector3.down * 0.47f,
                                new Vector3(0.94f, 0.06f, 0.94f), Quaternion.identity, FloorColor, false);
                            Register(floor, y);
                            _vanishers.Add((floor.transform, floor.transform.localScale));
                        }
                        else
                        {
                            // 위층은 유리 바닥: 시야를 가리지 않도록 칸 중심 아래에 작은 표식만 둔다.
                            // 클리어하면 벽·받침대와 함께 작아지며 사라진다.
                            var marker = Box(_staticRoot, "FloorMarker", ToWorld(cell) + Vector3.down * 0.47f,
                                new Vector3(0.18f, 0.03f, 0.18f), Quaternion.identity, FloorMarkerColor, false);
                            Register(marker, y);
                            _vanishers.Add((marker.transform, marker.transform.localScale));
                        }
                    }
                }
            }

            foreach (var wall in definition.Walls)
            {
                var wallGo = Box(_staticRoot, "Wall", ToWorld(wall), Vector3.one * 0.95f, Quaternion.identity, WallColor, false);
                Register(wallGo, wall.Y);
                _vanishers.Add((wallGo.transform, wallGo.transform.localScale));
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
                // 크리스털은 목표라서 층 보기(Tab)로 다른 층을 볼 때도 숨기지 않는다(Register 하지 않음).
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
            if (!session.IsCleared)
            {
                RestoreVanishers(); // 다시 하기·리셋이면 벽과 받침대를 원래 크기로
            }

            _beams.Clear();
            _beamInfos.Clear();
            _glows.Clear();
            _crystalArrival.Clear();
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

            // 각 구간이 광원에서 몇 칸 떨어져 시작하는지 센다. 꺾이거나 나뉜 칸의 거리를 기억해 두고,
            // 그 칸에서 시작하는 다음 구간(분할기의 꺾인 갈래 포함)이 이어받는다. 광원에서 시작하면 0.
            var distanceAt = new Dictionary<Int3, float>();
            foreach (var segment in session.LastTrace.Segments)
            {
                distanceAt.TryGetValue(segment.From, out var start);
                var end = start + CellDistance(segment.From, segment.To);
                if (!distanceAt.ContainsKey(segment.To))
                {
                    distanceAt[segment.To] = end;
                }

                if (segment.End == SegmentEnd.Absorbed && !_crystalArrival.ContainsKey(segment.To))
                {
                    _crystalArrival[segment.To] = end;
                }

                DrawBeam(segment, start, end);
            }

            foreach (var pair in _crystals)
            {
                var lit = session.LastTrace.IsLit(pair.Key);
                pair.Value.sharedMaterial = MaterialFor(lit ? CrystalOnColor : CrystalOffColor);
                pair.Value.transform.localScale = Vector3.one * (lit ? 0.52f : 0.42f);
            }

            ApplyFloorVisibility();
        }

        /// <summary>메인 메뉴로 돌아갈 때 판을 모두 지운다.</summary>
        public void Clear()
        {
            StopEffect();
            if (_staticRoot != null)
            {
                Object.Destroy(_staticRoot.gameObject);
                _staticRoot = null;
            }

            if (_dynamicRoot != null)
            {
                Object.Destroy(_dynamicRoot.gameObject);
                _dynamicRoot = null;
            }

            _floorObjects.Clear();
            _crystals.Clear();
            _vanishers.Clear();
            _beams.Clear();
            _beamInfos.Clear();
            _glows.Clear();
            _crystalArrival.Clear();
            VisibleFloor = -1;
        }

        /// <summary>층 보기를 처음 상태(전체 층)로 되돌린다. R 리셋에서 쓴다.</summary>
        public void ShowAllFloors()
        {
            VisibleFloor = -1;
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

        private static float CellDistance(Int3 a, Int3 b) =>
            Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) + Mathf.Abs(a.Z - b.Z);

        private void DrawBeam(BeamSegment segment, float startDistance, float endDistance)
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
            var rotation = Quaternion.LookRotation(direction, up);
            var beam = Box(_dynamicRoot, "Beam", (start + end) * 0.5f, new Vector3(BeamThickness, BeamThickness, length),
                rotation, BeamColor, false);
            _beams.Add(beam.transform);
            _beamInfos.Add(new BeamInfo(start, direction, length, rotation, startDistance, endDistance));
            // 광선은 층을 가로지르므로 층 보기와 상관없이 항상 보인다.
        }

        /// <summary>
        /// 클리어 연출을 시작한다. 빛이 광원부터 경로를 따라 밝게 지나가고(경로가 길수록 오래),
        /// 크리스털에 닿을 때마다 그 크리스털이 반짝인다. 이후 Tick이 움직인다.
        /// </summary>
        public void PlayClearEffect()
        {
            StopEffect();

            // 층 보기 중이었어도 전체 층 보기로 돌아가 전체 경로와 크리스털이 보이게 한다.
            VisibleFloor = -1;
            ApplyFloorVisibility();
            _burst.Clear();
            var longest = 0f;
            foreach (var info in _beamInfos)
            {
                longest = Mathf.Max(longest, info.EndDistance);
            }

            // 보통은 일정한 속도. 아주 긴 경로만 상한 시간에 맞춰 빨라진다.
            _glowSpeed = Mathf.Max(GlowSpeed, longest / MaxTravelTime);
            var lastArrival = 0f;
            foreach (var arrival in _crystalArrival.Values)
            {
                lastArrival = Mathf.Max(lastArrival, arrival);
            }

            _effectDuration = Mathf.Max(longest, lastArrival) / _glowSpeed + CrystalPulseTime;

            foreach (var info in _beamInfos)
            {
                var glow = Box(_dynamicRoot, "Glow", info.Start, Vector3.zero, info.Rotation, GlowColor, false);
                _glows.Add(glow.transform);
            }

            _effectTime = 0f;
        }

        /// <summary>매 프레임 호출. 연출 중이 아니면 아무것도 하지 않는다.</summary>
        public void Tick(float deltaTime)
        {
            if (_effectTime < 0f)
            {
                return;
            }

            _effectTime += deltaTime;
            var front = _effectTime * _glowSpeed; // 빛의 앞머리가 광원에서 몇 칸 왔는지

            // 벽과 받침대: 연출 시작과 함께 작아지며 사라진다
            var v = Mathf.Clamp01(_effectTime / VanishTime);
            var shrink = 1f - v * v * (3f - 2f * v);
            foreach (var (t, scale) in _vanishers)
            {
                if (t != null)
                {
                    t.localScale = scale * shrink;
                }
            }

            // 지나간 만큼 밝은 광선을 겹쳐 그린다(분할기의 갈래들은 동시에 나아간다).
            for (var i = 0; i < _beamInfos.Count && i < _glows.Count; i++)
            {
                var info = _beamInfos[i];
                var glow = _glows[i];
                if (glow == null)
                {
                    continue;
                }

                var span = Mathf.Max(0.0001f, info.EndDistance - info.StartDistance);
                var p = Mathf.Clamp01((front - info.StartDistance) / span);
                var shown = info.Length * p;
                glow.localPosition = info.Start + info.Direction * (shown * 0.5f);
                glow.localScale = shown > 0.001f ? new Vector3(GlowThickness, GlowThickness, shown) : Vector3.zero;
            }

            // 크리스털: 빛이 닿은 순간부터 한 바퀴 돌며 크게 튀었다가 점등 크기로 돌아온다
            foreach (var pair in _crystals)
            {
                if (!_crystalArrival.TryGetValue(pair.Key, out var arrival) || front < arrival)
                {
                    continue;
                }

                var crystal = pair.Value.transform;
                if (_burst.Add(pair.Key))
                {
                    SpawnSparks(crystal.localPosition);
                }

                var t = Mathf.Clamp01((_effectTime - arrival / _glowSpeed) / CrystalPulseTime);
                var pulse = 1f + 0.45f * Mathf.Sin(t * Mathf.PI) * (1f - t);
                crystal.localRotation = Quaternion.Euler(45f, 45f + t * 360f, 45f);
                crystal.localScale = Vector3.one * 0.52f * pulse;
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
                spark.localScale *= Mathf.Max(0f, 1f - deltaTime / CrystalPulseTime);
                _sparks[i] = (spark, velocity);
            }

            if (_effectTime >= _effectDuration)
            {
                StopEffect(); // 밝은 광선은 그대로 남겨, 결과 창 뒤에서 지나온 경로가 보이게 한다
            }
        }

        private void RestoreVanishers()
        {
            foreach (var (t, scale) in _vanishers)
            {
                if (t != null)
                {
                    t.localScale = scale;
                }
            }
        }

        private void SpawnSparks(Vector3 center)
        {
            for (var i = 0; i < 18; i++)
            {
                var spark = Box(_dynamicRoot, "Spark", center, Vector3.one * 0.09f, Random.rotation,
                    i % 3 == 0 ? BeamColor : CrystalOnColor, false);
                var velocity = Random.onUnitSphere * Random.Range(1.6f, 3.2f) + Vector3.up * 1.2f;
                _sparks.Add((spark.transform, velocity));
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

        /// <summary>그려진 광선 구간 하나: 화면 위치와 광원에서의 거리(칸).</summary>
        private readonly struct BeamInfo
        {
            public readonly Vector3 Start;
            public readonly Vector3 Direction;
            public readonly float Length;
            public readonly Quaternion Rotation;
            public readonly float StartDistance;
            public readonly float EndDistance;

            public BeamInfo(Vector3 start, Vector3 direction, float length, Quaternion rotation, float startDistance,
                float endDistance)
            {
                Start = start;
                Direction = direction;
                Length = length;
                Rotation = rotation;
                StartDistance = startDistance;
                EndDistance = endDistance;
            }
        }
    }
}
