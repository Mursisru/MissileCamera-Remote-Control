using MissileCameraRemoteControl.Config;
using UnityEngine;
using UnityEngine.UI;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>Draws the WT aim circle at a screen position (projection of world aim).</summary>
    internal static class FsAimReticle
    {
        private const float CircleSizePx = 56f;
        private const float RingThickness = 3f;

        private static GameObject? _root;
        private static RectTransform? _circleRt;
        private static Canvas? _canvas;
        private static bool _visible;

        internal static void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible)
            {
                if (_root != null)
                    _root.SetActive(false);
                return;
            }

            EnsureUi();
            if (_root != null)
                _root.SetActive(true);
        }

        /// <summary>Place circle from viewport coords (0..1). Caller keeps aim stable — no behind-cam edge snaps.</summary>
        internal static void SetFromViewport(float vx, float vy, bool inFront)
        {
            if (!_visible)
                return;
            EnsureUi();

            if (!inFront)
                return;

            if (float.IsNaN(vx) || float.IsNaN(vy) || float.IsInfinity(vx) || float.IsInfinity(vy))
                return;

            vx = Mathf.Clamp01(vx);
            vy = Mathf.Clamp01(vy);

            float margin = CircleSizePx * 0.5f;
            float sx = Mathf.Clamp(vx * Screen.width, margin, Screen.width - margin);
            float sy = Mathf.Clamp(vy * Screen.height, margin, Screen.height - margin);
            ApplyScreenPos(new Vector2(sx, sy));
        }

        internal static void DestroyUi()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                _circleRt = null;
                _canvas = null;
            }
            _visible = false;
        }

        private static void ApplyScreenPos(Vector2 screenPos)
        {
            if (_circleRt == null || _canvas == null)
                return;
            RectTransform? canvasRt = _canvas.transform as RectTransform;
            if (canvasRt == null)
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, null, out Vector2 local))
                _circleRt.anchoredPosition = local;
        }

        private static void EnsureUi()
        {
            if (_root != null)
                return;

            _root = new GameObject("MissileCameraRemoteControl.FsAimReticle");
            Object.DontDestroyOnLoad(_root);
            _root.hideFlags = HideFlags.HideAndDontSave;

            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;
            _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _root.AddComponent<GraphicRaycaster>();

            var circleGo = new GameObject("AimCircle");
            circleGo.transform.SetParent(_root.transform, false);
            _circleRt = circleGo.AddComponent<RectTransform>();
            _circleRt.sizeDelta = new Vector2(CircleSizePx, CircleSizePx);

            var ring = circleGo.AddComponent<Image>();
            ring.sprite = CreateRingSprite();
            ring.color = new Color(0.85f, 0.95f, 0.75f, 0.92f);
            ring.raycastTarget = false;

            var pipGo = new GameObject("Pip");
            pipGo.transform.SetParent(circleGo.transform, false);
            var pipRt = pipGo.AddComponent<RectTransform>();
            pipRt.sizeDelta = new Vector2(6f, 6f);
            var pip = pipGo.AddComponent<Image>();
            pip.sprite = CreateFilledSprite();
            pip.color = new Color(0.9f, 1f, 0.7f, 0.95f);
            pip.raycastTarget = false;
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f;
            float outer = c - 1f;
            float inner = outer - RingThickness * (size / CircleSizePx);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = (d <= outer && d >= inner) ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateFilledSprite()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            float c = (size - 1) * 0.5f;
            float r = c - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= r ? 1f : 0f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
