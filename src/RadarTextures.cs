using UnityEngine;

namespace RadarScanner
{
    /// <summary>
    /// Draws the overlay's textures in code, so the mod ships no image files and the colours can
    /// follow the config.
    /// </summary>
    internal static class RadarTextures
    {
        /// <summary>
        /// Radius of the scanner's round mask, as a fraction of the scanner image's half-width.
        /// The game sizes the scanner camera to 1.12 x scanner range, so at 100% zoom this is also
        /// where the scanner range ends.
        /// </summary>
        public const float VisibleRadius = 0.894f;

        private const float TrailAlpha = 0.5f;
        private const float LineHalfWidth = 1.5f;
        // How fast the dimming sets in behind the scan line: higher = most of it in the first quarter turn.
        private const float FadeDecay = 3f;
        private const float GridAlpha = 0.3f;
        private const float TintBrightness = 0.12f;

        public static Texture2D Create(int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>
        /// The scan line, pointing right from the centre, with everything that follows it round:
        /// the bright afterglow just behind it, and a black veil that thickens the longer it has
        /// been since the line passed. Turning this one image makes the scanner underneath light
        /// up under the line and fade until the next pass. Drawn for a clockwise sweep.
        /// </summary>
        public static Color32[] SweepPixels(int size, Color color, float trailDegrees, float fadeStrength)
        {
            var pixels = new Color32[size * size];
            float centre = size * 0.5f;
            trailDegrees = Mathf.Clamp(trailDegrees, 1f, 359f);
            fadeStrength = Mathf.Clamp01(fadeStrength);
            float fadeScale = fadeStrength / (1f - Mathf.Exp(-FadeDecay));
            for (int y = 0; y < size; y++)
            {
                float dy = y + 0.5f - centre;
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - centre;
                    // Degrees since the line passed this pixel.
                    float since = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (since < 0f)
                    {
                        since += 360f;
                    }
                    float glow = 0f;
                    if (since < trailDegrees)
                    {
                        float left = 1f - since / trailDegrees;
                        glow = TrailAlpha * left * left;
                    }
                    if (dx >= 0f)
                    {
                        glow = Mathf.Max(glow, Edge(Mathf.Abs(dy), LineHalfWidth));
                    }
                    float veil = fadeScale * (1f - Mathf.Exp(-FadeDecay * since / 360f));
                    pixels[y * size + x] = Over(color, glow, Color.black, veil);
                }
            }
            return pixels;
        }

        /// <summary>The still part of the screen: a dark phosphor tint, range rings, a crosshair and bearing ticks.</summary>
        public static Color32[] ScreenPixels(int size, Color color, float tint, bool grid)
        {
            var pixels = new Color32[size * size];
            float centre = size * 0.5f;
            float edge = centre * VisibleRadius;
            tint = Mathf.Clamp01(tint);
            var tintColor = new Color(color.r * TintBrightness, color.g * TintBrightness, color.b * TintBrightness);
            for (int y = 0; y < size; y++)
            {
                float dy = y + 0.5f - centre;
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - centre;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float rn = r / edge;
                    // A little darker towards the rim, like a curved tube.
                    float tintAlpha = tint * Mathf.Clamp01(0.55f + 0.45f * rn * rn);
                    float line = 0f;
                    if (grid && rn <= 1.02f)
                    {
                        line = Mathf.Max(Edge(Mathf.Abs(r - edge / 3f), 1f), Edge(Mathf.Abs(r - edge * 2f / 3f), 1f));
                        line = Mathf.Max(line, 0.7f * Edge(Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)), 1f));
                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (rn > 0.93f)
                        {
                            line = Mathf.Max(line, Edge(TickDistance(r, angle, 30f), 1f));
                        }
                        if (rn > 0.965f)
                        {
                            line = Mathf.Max(line, Edge(TickDistance(r, angle, 10f), 1f));
                        }
                    }
                    pixels[y * size + x] = Over(color, GridAlpha * line, tintColor, tintAlpha);
                }
            }
            return pixels;
        }

        /// <summary>A soft white ring with a faint glow inside it. A ripple is this ring growing and fading.</summary>
        public static Color32[] RingPixels(int size)
        {
            var pixels = new Color32[size * size];
            float centre = size * 0.5f;
            float ring = centre * 0.82f;
            for (int y = 0; y < size; y++)
            {
                float dy = y + 0.5f - centre;
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - centre;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(r - ring) / 3f);
                    alpha *= alpha;
                    if (r < ring)
                    {
                        float inner = r / ring;
                        alpha = Mathf.Max(alpha, 0.14f * inner * inner);
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            return pixels;
        }

        /// <summary>Coverage of a pixel <paramref name="distance"/> from the middle of a line, with a one-pixel soft edge.</summary>
        private static float Edge(float distance, float halfWidth)
        {
            return Mathf.Clamp01(halfWidth + 0.5f - distance);
        }

        /// <summary>Distance from a point at (r, angle) to the nearest spoke of a wheel with a spoke every <paramref name="step"/> degrees.</summary>
        private static float TickDistance(float r, float angle, float step)
        {
            float off = Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Round(angle / step) * step));
            return r * Mathf.Sin(off * Mathf.Deg2Rad);
        }

        /// <summary>One colour laid over another, as a single colour with straight alpha.</summary>
        private static Color32 Over(Color top, float topAlpha, Color bottom, float bottomAlpha)
        {
            float under = bottomAlpha * (1f - topAlpha);
            float alpha = topAlpha + under;
            if (alpha <= 0f)
            {
                return new Color(top.r, top.g, top.b, 0f);
            }
            return new Color(
                (top.r * topAlpha + bottom.r * under) / alpha,
                (top.g * topAlpha + bottom.g * under) / alpha,
                (top.b * topAlpha + bottom.b * under) / alpha,
                alpha);
        }
    }
}
