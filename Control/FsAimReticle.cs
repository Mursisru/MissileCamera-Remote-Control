using MissileCameraRemoteControl.Config;
using UnityEngine;
using UnityEngine.UI;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>War Thunder-style aim circle drawn over MissileCamera fullscreen.</summary>
    internal static class FsAimReticle
    {
        private const float CircleSizePx = 56f;
        private const float RingThickness = 3f;

        private static GameObject? _root;
        private static RectTransform? _circleRt;
        private static Canvas? _canvas;
        private static Vector2 _screenPos;
        private static bool _visible;

        internal static Vector2 ScreenPosition => _screenPos;

        internal static void ResetToCenter()
        {
            _screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ApplyTransform();
        }

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
            ApplyTransform();
        }

        internal static void TickMove()
        {
            if (!_visible)
                return;

            EnsureUi();
            float sens = Mathf.Max(0.01f, RcConfig.MouseSensitivity.Value) * 80f;
            _screenPos.x += Input.GetAxisRaw("Mouse X") * sens;
            _screenPos.y += Input.GetAxisRaw("Mouse Y") * sens;

            float margin = CircleSizePx * 0.5f;
            _screenPos.x = Mathf.Clamp(_screenPos.x, margin, Screen.width - margin);
            _screenPos.y = Mathf.Clamp(_screenPos.y, margin, Screen.height - margin);
            ApplyTransform();
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

            // Outer ring
            var ring = circleGo.AddComponent<Image>();
            ring.sprite = CreateRingSprite();
            ring.color = new Color(0.85f, 0.95f, 0.75f, 0.92f);
            ring.raycastTarget = false;

            // Center pip
            var pipGo = new GameObject("Pip");
            pipGo.transform.SetParent(circleGo.transform, false);
            var pipRt = pipGo.AddComponent<RectTransform>();
            pipRt.sizeDelta = new Vector2(6f, 6f);
            pipRt.anchoredPosition = Vector2.zero;
            var pip = pipGo.AddComponent<Image>();
            pip.sprite = CreateFilledSprite();
            pip.color = new Color(0.9f, 1f, 0.7f, 0.95f);
            pip.raycastTarget = false;

            ResetToCenter();
        }

        private static void ApplyTransform()
        {
            if (_circleRt == null || _canvas == null)
                return;

            RectTransform? canvasRt = _canvas.transform as RectTransform;
            if (canvasRt == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt,
                    _screenPos,
                    null,
                    out Vector2 local))
            {
                _circleRt.anchoredPosition = local;
            }
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
                    float a = d <= r ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
