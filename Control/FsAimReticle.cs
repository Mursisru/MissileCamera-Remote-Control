using UnityEngine;
using UnityEngine.UI;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// RC aim circle. Parent under MC HUD; place with FeedScreenProjector math on viewRect
    /// (same space as MC intercept). Viewport 0.5 ⇒ anchored 0 ⇒ FLIR optical center.
    /// </summary>
    internal static class FsAimReticle
    {
        private const float CircleSizePx = 48f;
        private const float RingThickness = 2.5f;

        private static GameObject? _root;
        private static RectTransform? _circleRt;
        private static RectTransform? _layoutSpace;
        private static Canvas? _fallbackCanvas;
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

        internal static void SetFromViewport(float vx, float vy, bool inFront)
        {
            if (!_visible)
                return;
            EnsureUi();
            if (!inFront || _circleRt == null)
                return;
            if (float.IsNaN(vx) || float.IsNaN(vy) || float.IsInfinity(vx) || float.IsInfinity(vy))
                return;

            ReparentToHudIfNeeded();

            // Prefer feed viewRect size — identical to MissileCamera.FeedScreenProjector.
            RectTransform? space = Access.MissileCameraFsAccess.TryGetFeedViewRect() ?? _layoutSpace;
            if (space != null)
            {
                Vector2 size = space.rect.size;
                if (size.x < 1f || size.y < 1f)
                    return;

                _circleRt.anchoredPosition = new Vector2(
                    (vx - 0.5f) * size.x,
                    (vy - 0.5f) * size.y);
                return;
            }

            float margin = CircleSizePx * 0.5f;
            float sx = Mathf.Clamp(vx * Screen.width, margin, Screen.width - margin);
            float sy = Mathf.Clamp(vy * Screen.height, margin, Screen.height - margin);
            if (_fallbackCanvas != null
                && _fallbackCanvas.transform is RectTransform canvasRt
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt, new Vector2(sx, sy), null, out Vector2 local))
            {
                _circleRt.anchoredPosition = local;
            }
        }

        internal static void DestroyUi()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                _circleRt = null;
                _layoutSpace = null;
                _fallbackCanvas = null;
            }
            _visible = false;
        }

        private static void ReparentToHudIfNeeded()
        {
            RectTransform? hud = Access.MissileCameraFsAccess.TryGetHudOverlayRoot();
            if (hud == null || _root == null)
                return;

            if (_layoutSpace == hud && _root.transform.parent == hud)
                return;

            _layoutSpace = hud;
            _fallbackCanvas = null;
            _root.transform.SetParent(hud, false);

            if (_root.TryGetComponent(out Canvas c))
                Object.Destroy(c);
            if (_root.TryGetComponent(out CanvasScaler s))
                Object.Destroy(s);
            if (_root.TryGetComponent(out GraphicRaycaster g))
                Object.Destroy(g);

            var rt = _root.GetComponent<RectTransform>() ?? _root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            if (_circleRt != null)
            {
                _circleRt.anchorMin = new Vector2(0.5f, 0.5f);
                _circleRt.anchorMax = new Vector2(0.5f, 0.5f);
                _circleRt.pivot = new Vector2(0.5f, 0.5f);
                _circleRt.sizeDelta = new Vector2(CircleSizePx, CircleSizePx);
            }

            _root.transform.SetAsLastSibling();
        }

        private static void EnsureUi()
        {
            if (_root != null)
            {
                ReparentToHudIfNeeded();
                return;
            }

            _root = new GameObject("MissileCameraRemoteControl.FsAimReticle");
            _root.hideFlags = HideFlags.HideAndDontSave;

            RectTransform? hud = Access.MissileCameraFsAccess.TryGetHudOverlayRoot();
            if (hud != null)
            {
                _layoutSpace = hud;
                _root.transform.SetParent(hud, false);
                var rt = _root.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
            }
            else
            {
                Object.DontDestroyOnLoad(_root);
                _fallbackCanvas = _root.AddComponent<Canvas>();
                _fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _fallbackCanvas.sortingOrder = 32000;
                _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                _root.AddComponent<GraphicRaycaster>();
            }

            var circleGo = new GameObject("AimCircle");
            circleGo.transform.SetParent(_root.transform, false);
            _circleRt = circleGo.AddComponent<RectTransform>();
            _circleRt.anchorMin = new Vector2(0.5f, 0.5f);
            _circleRt.anchorMax = new Vector2(0.5f, 0.5f);
            _circleRt.pivot = new Vector2(0.5f, 0.5f);
            _circleRt.sizeDelta = new Vector2(CircleSizePx, CircleSizePx);

            var ring = circleGo.AddComponent<Image>();
            ring.sprite = CreateRingSprite();
            // Distinct from MC intercept green (0,1,0) — pale yellow-green.
            ring.color = new Color(0.95f, 1f, 0.55f, 0.95f);
            ring.raycastTarget = false;

            var pipGo = new GameObject("Pip");
            pipGo.transform.SetParent(circleGo.transform, false);
            var pipRt = pipGo.AddComponent<RectTransform>();
            pipRt.sizeDelta = new Vector2(5f, 5f);
            var pip = pipGo.AddComponent<Image>();
            pip.sprite = CreateFilledSprite();
            pip.color = new Color(1f, 1f, 0.7f, 1f);
            pip.raycastTarget = false;

            if (hud != null)
                _root.transform.SetAsLastSibling();
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
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, (d <= outer && d >= inner) ? 1f : 0f));
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
