using UnityEngine;
using UnityEngine.UI;

namespace WildLens.UI
{
    /// <summary>
    /// One look for everything that is not the journal book: warm off-white type, a soft drop shadow, a faint dark pill
    /// where text sits on bright ground, and the fade timings. The HUD, the prompts, the notices, the viewfinder and the
    /// controls card all read their colours, sizes and timings from here (1.9). Canvas reference: 1920 x 1080, match 0.5
    /// (see <see cref="UIFactory.CreateCanvas"/>), so the visible canvas is never narrower than about 1660 or shorter
    /// than about 935 reference pixels (4:3 to 21:9): keep a 56 px margin and nothing important sits beyond it.
    /// </summary>
    public static class UiStyle
    {
        // ---- colour
        /// <summary>Warm off-white, the colour of all HUD type.</summary>
        public static readonly Color Text = new Color(0.97f, 0.95f, 0.89f, 1f);
        public static readonly Color TextSoft = new Color(0.97f, 0.95f, 0.89f, 0.72f);
        public static readonly Color TextFaint = new Color(0.97f, 0.95f, 0.89f, 0.45f);
        /// <summary>Quiet grade colours for the viewfinder (Poor stays white).</summary>
        public static readonly Color Good = new Color(0.67f, 0.89f, 0.6f, 1f);
        public static readonly Color Excellent = new Color(0.97f, 0.81f, 0.45f, 1f);
        public static readonly Color Alert = new Color(1f, 0.74f, 0.32f, 1f);
        public static readonly Color PillFill = new Color(0.05f, 0.07f, 0.05f, 0.4f);
        public static readonly Color PillFillAlert = new Color(0.10f, 0.07f, 0.03f, 0.62f);

        // ---- sizes (reference pixels)
        public const float Margin = 56f;
        public const int SizeSmall = 18;
        public const int SizeBody = 22;
        public const int SizeTitle = 26;
        public const float PillHeight = 48f;

        // ---- timings (unscaled seconds)
        public const float Fade = 0.25f;

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>A soft drop shadow: lighter than an outline, enough to read on bright ground with a pill behind it.</summary>
        public static Shadow SoftShadow(Graphic g, float strength = 0.55f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, strength);
            s.effectDistance = new Vector2(1f, -1.5f);
            return s;
        }

        private static Sprite _pill;

        /// <summary>A white fully rounded rectangle with 9-slice borders; tint it with Image.color.</summary>
        public static Sprite PillSprite
        {
            get
            {
                if (_pill != null) return _pill;
                const int size = 52;
                const float r = 24f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
                };
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(px);
                tex.Apply();
                _pill = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0u,
                    SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                _pill.hideFlags = HideFlags.HideAndDontSave;
                return _pill;
            }
        }

        /// <summary>A faint dark rounded pill (an Image with the pill sprite), no border.</summary>
        public static Image CreatePill(string name, Transform parent, Color fill)
        {
            var img = UIFactory.CreateImage(name, parent, fill);
            img.sprite = PillSprite;
            img.type = Image.Type.Sliced;
            return img;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // With domain reload off, the hidden texture and sprite would outlive the play session: free them, then forget.
            if (_pill != null)
            {
                var tex = _pill.texture;
                if (Application.isPlaying) { Object.Destroy(_pill); if (tex != null) Object.Destroy(tex); }
                else { Object.DestroyImmediate(_pill); if (tex != null) Object.DestroyImmediate(tex); }
            }
            _pill = null;
        }
    }
}
