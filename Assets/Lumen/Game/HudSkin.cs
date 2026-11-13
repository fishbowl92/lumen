using UnityEngine;

namespace Lumen.Game
{
    /// <summary>
    /// HUD(IMGUI) 스타일 모음. 기본 회색 스킨 대신 어두운 반투명 바와 단색 버튼을 쓴다.
    /// GUI.skin은 OnGUI 안에서만 쓸 수 있으므로 처음 그릴 때 한 번 만든다.
    /// </summary>
    public sealed class HudSkin
    {
        private static readonly Color Text = new Color(0.92f, 0.93f, 0.96f);
        private static readonly Color Muted = new Color(0.62f, 0.66f, 0.74f);
        private static readonly Color Accent = new Color(1.00f, 0.58f, 0.28f);
        private static readonly Color AccentText = new Color(0.10f, 0.07f, 0.05f);

        public HudSkin()
        {
            var bar = Solid(new Color(0.05f, 0.06f, 0.09f, 0.88f));
            var panel = Solid(new Color(0.09f, 0.10f, 0.14f, 0.96f));
            var normal = Solid(new Color(0.17f, 0.19f, 0.25f));
            var hover = Solid(new Color(0.23f, 0.26f, 0.34f));
            var pressed = Solid(new Color(0.13f, 0.14f, 0.19f));
            var accent = Solid(Accent);
            var accentHover = Solid(new Color(1.00f, 0.66f, 0.40f));

            Bar = new GUIStyle { normal = { background = bar } };
            Panel = new GUIStyle { normal = { background = panel } };

            Label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, alignment = TextAnchor.MiddleLeft, normal = { textColor = Text }, wordWrap = false
            };
            LabelCenter = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            LabelDark = new GUIStyle(Label) { normal = { textColor = AccentText } };
            Hint = new GUIStyle(Label) { fontSize = 12, normal = { textColor = Muted } };
            HintCenter = new GUIStyle(Hint) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            Title = new GUIStyle(LabelCenter) { fontSize = 32, fontStyle = FontStyle.Bold };

            Button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4),
                border = new RectOffset(0, 0, 0, 0),
                normal = { background = normal, textColor = Text },
                hover = { background = hover, textColor = Text },
                active = { background = pressed, textColor = Text },
                focused = { background = normal, textColor = Text }
            };
            Selected = new GUIStyle(Button)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = accent, textColor = AccentText },
                hover = { background = accentHover, textColor = AccentText },
                active = { background = accent, textColor = AccentText },
                focused = { background = accent, textColor = AccentText }
            };
        }

        public GUIStyle Bar { get; }
        public GUIStyle Panel { get; }
        public GUIStyle Label { get; }
        public GUIStyle LabelCenter { get; }
        public GUIStyle LabelDark { get; }
        public GUIStyle Hint { get; }
        public GUIStyle HintCenter { get; }
        public GUIStyle Title { get; }
        public Texture2D StarFilled { get; } = Star(true);
        public Texture2D StarEmpty { get; } = Star(false);
        public GUIStyle Button { get; }
        public GUIStyle Selected { get; }

        /// <summary>
        /// 별 그림(64×64)을 코드로 만든다. 채운 별은 주황으로 칠하고, 빈 별은 테두리만 그린다.
        /// 픽셀마다 4×4 표본을 찍어 가장자리를 부드럽게 한다.
        /// </summary>
        private static Texture2D Star(bool filled)
        {
            const int Size = 64;
            const int Samples = 4;
            var outer = StarPoints(Size * 0.48f, Size * 0.20f);
            var inner = StarPoints(Size * 0.48f - 6f, Size * 0.20f - 3f);
            var empty = new Color(Accent.r, Accent.g, Accent.b, 0.85f);
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < Samples; sy++)
                    {
                        for (var sx = 0; sx < Samples; sx++)
                        {
                            var p = new Vector2(x + (sx + 0.5f) / Samples - Size * 0.5f, y + (sy + 0.5f) / Samples - Size * 0.5f);
                            var inside = Inside(outer, p) && (filled || !Inside(inner, p));
                            if (inside)
                            {
                                hits++;
                            }
                        }
                    }

                    var coverage = hits / (float)(Samples * Samples);
                    var color = filled ? Accent : empty;
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * coverage));
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>꼭짓점이 위를 향하는 별 다각형(바깥 꼭짓점 5개, 안쪽 꼭짓점 5개).</summary>
        private static Vector2[] StarPoints(float outerRadius, float innerRadius)
        {
            var points = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                var radius = i % 2 == 0 ? outerRadius : innerRadius;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            return points;
        }

        private static bool Inside(Vector2[] polygon, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
