using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RadarScanner
{
    /// <summary>
    /// The radar look on the scanner in the bottom left corner: a phosphor tint and grid, a
    /// turning scan line whose afterglow fades behind it, and a ripple around each detected
    /// object as the line passes it.
    ///
    /// The overlay is a child of the scanner's own image, so the game's round mask clips it and
    /// the game shows and hides it together with the scanner. The scanner border, the zoom
    /// buttons and the station labels are separate objects that are drawn after it, so they stay
    /// on top.
    /// </summary>
    internal sealed class RadarOverlay : MonoBehaviour
    {
        private const string RootName = "RadarScannerOverlay";
        private const string ScannerImagePath = "Scanner/Images/MaskedImage/Image";
        private const int SweepTextureSize = 512;
        private const int ScreenTextureSize = 512;
        private const int RingTextureSize = 64;
        private const float ScanSeconds = 0.25f;
        private const int MaxRipples = 96;
        // The game lays the scanner out 256 wide. Ripple sizes in the config are in those units.
        private const float ScannerWidth = 256f;
        // A second ring follows the first, this far into the ripple.
        private const float EchoDelay = 0.22f;
        // Too close to the centre to have a bearing worth the name.
        private const float MinRange = 0.03f;

        public static RadarOverlay Instance;

        private readonly RadarContacts contacts = new RadarContacts();
        private readonly List<Ripple> ripples = new List<Ripple>();
        private readonly Stack<Ripple> pool = new Stack<Ripple>();
        private Camera minimapCam;
        private RectTransform rect;
        private RawImage screen;
        private RawImage sweep;
        private RectTransform rippleLayer;
        private Texture2D screenTexture;
        private Texture2D sweepTexture;
        private Texture2D ringTexture;
        private float sweepAngle;
        private float nextScan;

        /// <summary>One ring of a ripple, growing and fading where its contact is on the scanner.</summary>
        private sealed class Ripple
        {
            public RectTransform Rect;
            public RawImage Image;
            public Contact Contact;
            public Vector2 MapPos;
            public Color Color;
            public float Age;
            public float Duration;
            public float Size;
        }

        public static void Create(MinimapControl minimap)
        {
            GameObject canvas = GameObject.FindGameObjectWithTag("MainCanvas");
            Transform image = canvas != null ? canvas.transform.Find(ScannerImagePath) : null;
            if (image == null)
            {
                Plugin.Log.LogError("The scanner has changed; can't add the radar overlay.");
                return;
            }
            if (image.Find(RootName) != null)
            {
                return;
            }
            var go = new GameObject(RootName, typeof(RectTransform));
            go.layer = image.gameObject.layer;
            go.transform.SetParent(image, false);
            Stretch((RectTransform)go.transform);
            Instance = go.AddComponent<RadarOverlay>();
            Instance.Build(GameAccess.MinimapCam(minimap));
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            Destroy(screenTexture);
            Destroy(sweepTexture);
            Destroy(ringTexture);
        }

        private void Build(Camera camera)
        {
            minimapCam = camera;
            rect = (RectTransform)transform;
            screen = AddImage("Screen", transform);
            sweep = AddImage("Sweep", transform);

            var layer = new GameObject("Ripples", typeof(RectTransform));
            layer.layer = gameObject.layer;
            layer.transform.SetParent(transform, false);
            rippleLayer = (RectTransform)layer.transform;
            Stretch(rippleLayer);

            ringTexture = RadarTextures.Create(RingTextureSize, RadarTextures.RingPixels(RingTextureSize));
            ApplyConfig();
        }

        /// <summary>Redraws the textures from the config. Called when the overlay is built and when a setting changes.</summary>
        public void ApplyConfig()
        {
            Color color = PhosphorColor();
            Destroy(screenTexture);
            Destroy(sweepTexture);
            screenTexture = RadarTextures.Create(ScreenTextureSize,
                RadarTextures.ScreenPixels(ScreenTextureSize, color, Plugin.ScreenTint.Value, Plugin.ShowGrid.Value));
            sweepTexture = RadarTextures.Create(SweepTextureSize,
                RadarTextures.SweepPixels(SweepTextureSize, color, Plugin.TrailDegrees.Value, Plugin.FadeStrength.Value));
            screen.texture = screenTexture;
            sweep.texture = sweepTexture;
            // The sweep texture is drawn for a clockwise sweep; mirror it for the other way.
            sweep.rectTransform.localScale = new Vector3(1f, Plugin.Clockwise.Value ? 1f : -1f, 1f);
            gameObject.SetActive(Plugin.Enabled.Value);
        }

        private static Color PhosphorColor()
        {
            Color color = Plugin.PhosphorColor.Value;
            color.a = 1f;
            return color;
        }

        private RawImage AddImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            RawImage image = go.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private void OnDisable()
        {
            // The scanner was hidden (sector map, docking...). Don't leave half-faded ripples for when it comes back.
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                Release(i);
            }
            for (int i = 0; i < contacts.Detected.Count; i++)
            {
                contacts.Detected[i].Phase = -1f;
            }
            nextScan = 0f;
        }

        private void Update()
        {
            if (minimapCam == null)
            {
                return;
            }
            try
            {
                Step(Time.deltaTime);
            }
            catch (Exception e)
            {
                // Leave the game's own scanner working rather than log an error every frame.
                Plugin.Log.LogError("The radar overlay failed and is switched off: " + e);
                gameObject.SetActive(false);
            }
        }

        private void Step(float deltaTime)
        {
            float period = Mathf.Max(0.5f, Plugin.SweepSeconds.Value);
            float direction = Plugin.Clockwise.Value ? -1f : 1f;
            sweepAngle = Mathf.Repeat(sweepAngle + 360f * deltaTime / period, 360f);
            // Where the scan line points, in degrees anticlockwise from "right" on the scanner.
            float lineAngle = direction * sweepAngle;
            sweep.rectTransform.localEulerAngles = new Vector3(0f, 0f, lineAngle);

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + ScanSeconds;
                contacts.Refresh();
            }

            // Ripples must not come round faster than the line does, even if a contact sits on the line and jitters.
            float minRippleGap = period * 0.5f;
            List<Contact> detected = contacts.Detected;
            for (int i = 0; i < detected.Count; i++)
            {
                Contact contact = detected[i];
                if (contact.Transform == null || !MapPosition(contact.Transform.position, out contact.MapPos))
                {
                    contact.OnScope = false;
                    contact.Phase = -1f;
                    continue;
                }
                contact.OnScope = true;
                float bearing = Mathf.Atan2(contact.MapPos.y, contact.MapPos.x) * Mathf.Rad2Deg;
                float phase = Mathf.Repeat(direction * (lineAngle - bearing), 360f);
                // The phase climbs towards 360 as the line comes round and drops back to 0 as it passes.
                bool passed = contact.Phase >= 0f && contact.Phase - phase > 180f;
                contact.Phase = phase;
                if (passed && Time.time - contact.LastRipple >= minRippleGap)
                {
                    contact.LastRipple = Time.time;
                    StartRipple(contact);
                }
            }

            UpdateRipples(deltaTime);
        }

        /// <summary>
        /// Where a point in space is on the scanner, from -1 to 1 across the scanner image.
        /// False if it is outside the round part that shows.
        /// </summary>
        private bool MapPosition(Vector3 worldPosition, out Vector2 mapPos)
        {
            Vector3 viewport = minimapCam.WorldToViewportPoint(worldPosition);
            mapPos = new Vector2(viewport.x * 2f - 1f, viewport.y * 2f - 1f);
            float range = mapPos.magnitude;
            return range >= MinRange && range <= RadarTextures.VisibleRadius;
        }

        private void StartRipple(Contact contact)
        {
            float size = Plugin.RippleSize.Value;
            float alpha = 1f;
            switch (contact.Kind)
            {
                case ContactKind.Station:
                    size *= 1.5f;
                    break;
                case ContactKind.Loot:
                    size *= 0.75f;
                    break;
                case ContactKind.Asteroid:
                    // Asteroids come in dozens; keep them from drowning out everything else.
                    size *= 0.5f;
                    alpha = 0.5f;
                    break;
            }
            Color color = PhosphorColor();
            if (Plugin.RelationColors.Value)
            {
                color = contact.RelationColor(color);
            }
            color.a = alpha;
            float duration = Mathf.Max(0.1f, Plugin.RippleSeconds.Value);
            AddRing(contact, color, size, duration, 0f);
            AddRing(contact, color, size, duration, -EchoDelay * duration);
        }

        private void AddRing(Contact contact, Color color, float size, float duration, float age)
        {
            if (ripples.Count >= MaxRipples)
            {
                return;
            }
            Ripple ripple;
            if (pool.Count > 0)
            {
                ripple = pool.Pop();
            }
            else
            {
                ripple = new Ripple();
                ripple.Image = AddImage("Ripple", rippleLayer);
                ripple.Image.texture = ringTexture;
                ripple.Rect = ripple.Image.rectTransform;
                ripple.Rect.anchorMin = ripple.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            }
            ripple.Contact = contact;
            ripple.MapPos = contact.MapPos;
            ripple.Color = color;
            ripple.Size = size;
            ripple.Duration = duration;
            ripple.Age = age;
            ripple.Image.enabled = false;
            ripple.Rect.gameObject.SetActive(true);
            ripples.Add(ripple);
        }

        private void UpdateRipples(float deltaTime)
        {
            float halfWidth = rect.rect.width * 0.5f;
            float unit = rect.rect.width / ScannerWidth;
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                Ripple ripple = ripples[i];
                ripple.Age += deltaTime;
                if (ripple.Age >= ripple.Duration)
                {
                    Release(i);
                    continue;
                }
                if (ripple.Age < 0f)
                {
                    continue;
                }
                // Stay on the object while it is on the scanner; if it is destroyed or lost, fade out where it was.
                if (ripple.Contact.OnScope)
                {
                    ripple.MapPos = ripple.Contact.MapPos;
                }
                float t = ripple.Age / ripple.Duration;
                // Spread fast, then slow down.
                float spread = 1f - (1f - t) * (1f - t);
                float size = Mathf.Lerp(0.25f, 1f, spread) * ripple.Size * unit;
                Color color = ripple.Color;
                color.a *= (1f - t) * (1f - t);
                ripple.Rect.anchoredPosition = ripple.MapPos * halfWidth;
                ripple.Rect.sizeDelta = new Vector2(size, size);
                ripple.Image.color = color;
                ripple.Image.enabled = true;
            }
        }

        private void Release(int index)
        {
            Ripple ripple = ripples[index];
            ripples.RemoveAt(index);
            ripple.Contact = null;
            // Null while the scene is being torn down: the ring can be destroyed before this overlay is.
            if (ripple.Rect != null)
            {
                ripple.Rect.gameObject.SetActive(false);
                pool.Push(ripple);
            }
        }
    }
}
