using UnityEngine;
using UnityEngine.UI;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// RC aim marker: four white corner brackets with gaps + center pip.
    /// Parent under MC HUD; viewport 0.5 ⇒ FLIR optical center.
    /// </summary>
    internal static class FsAimReticle
    {
        private const float BracketOuterPx = 52f;
        private const float BracketArmPx = 14f;
        private const float BracketThicknessPx = 2.5f;
        private const float GapInsetPx = 10f;
        private const float PipSizePx = 4f;

        private static readonly Color MarkerWhite = new Color(1f, 1f, 1f, 0.95f);

        private static GameObject? _root;
        private static RectTransform? _markerRt;
        private static RectTransform? _layoutSpace;
        private static Canvas? _fallbackCanvas;
        private static bool _visible;

        internal static void SetVisible(bool visible)
        {
            if (_visible == visible && _root != null && _root.activeSelf == visible)
            {
                _visible = visible;
                return;
            }

            _visible = visible;
            if (!visible)
            {
                if (_root != null && _root.activeSelf)
                    _root.SetActive(false);
                return;
            }

            EnsureUi();
            if (_root != null && !_root.activeSelf)
                _root.SetActive(true);
        }

        internal static void SetFromViewport(float vx, float vy, bool inFront)
        {
            if (!_visible)
                return;
            EnsureUi();
            if (!inFront || _markerRt == null)
                return;
            if (float.IsNaN(vx) || float.IsNaN(vy) || float.IsInfinity(vx) || float.IsInfinity(vy))
                return;

            ReparentToHudIfNeeded();

            RectTransform? space = Access.MissileCameraFsAccess.TryGetFeedViewRect() ?? _layoutSpace;
            if (space != null)
            {
                Vector2 size = space.rect.size;
                if (size.x < 1f || size.y < 1f)
                    return;

                _markerRt.anchoredPosition = new Vector2(
                    (vx - 0.5f) * size.x,
                    (vy - 0.5f) * size.y);
                return;
            }

            float margin = BracketOuterPx * 0.5f;
            float sx = Mathf.Clamp(vx * Screen.width, margin, Screen.width - margin);
            float sy = Mathf.Clamp(vy * Screen.height, margin, Screen.height - margin);
            if (_fallbackCanvas != null
                && _fallbackCanvas.transform is RectTransform canvasRt
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt, new Vector2(sx, sy), null, out Vector2 local))
            {
                _markerRt.anchoredPosition = local;
            }
        }

        internal static void DestroyUi()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                _markerRt = null;
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

            if (_markerRt != null)
            {
                _markerRt.anchorMin = new Vector2(0.5f, 0.5f);
                _markerRt.anchorMax = new Vector2(0.5f, 0.5f);
                _markerRt.pivot = new Vector2(0.5f, 0.5f);
                _markerRt.sizeDelta = new Vector2(BracketOuterPx, BracketOuterPx);
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

            var markerGo = new GameObject("AimBracket");
            markerGo.transform.SetParent(_root.transform, false);
            _markerRt = markerGo.AddComponent<RectTransform>();
            _markerRt.anchorMin = new Vector2(0.5f, 0.5f);
            _markerRt.anchorMax = new Vector2(0.5f, 0.5f);
            _markerRt.pivot = new Vector2(0.5f, 0.5f);
            _markerRt.sizeDelta = new Vector2(BracketOuterPx, BracketOuterPx);

            // Four corners: TL, TR, BL, BR — L-brackets with gap toward center.
            float half = BracketOuterPx * 0.5f;
            float inset = GapInsetPx;
            AddCornerArm(markerGo.transform, "TL_H", new Vector2(-half + inset, half - inset), new Vector2(BracketArmPx, BracketThicknessPx), new Vector2(0f, 1f));
            AddCornerArm(markerGo.transform, "TL_V", new Vector2(-half + inset, half - inset), new Vector2(BracketThicknessPx, BracketArmPx), new Vector2(0f, 1f));
            AddCornerArm(markerGo.transform, "TR_H", new Vector2(half - inset, half - inset), new Vector2(BracketArmPx, BracketThicknessPx), new Vector2(1f, 1f));
            AddCornerArm(markerGo.transform, "TR_V", new Vector2(half - inset, half - inset), new Vector2(BracketThicknessPx, BracketArmPx), new Vector2(1f, 1f));
            AddCornerArm(markerGo.transform, "BL_H", new Vector2(-half + inset, -half + inset), new Vector2(BracketArmPx, BracketThicknessPx), new Vector2(0f, 0f));
            AddCornerArm(markerGo.transform, "BL_V", new Vector2(-half + inset, -half + inset), new Vector2(BracketThicknessPx, BracketArmPx), new Vector2(0f, 0f));
            AddCornerArm(markerGo.transform, "BR_H", new Vector2(half - inset, -half + inset), new Vector2(BracketArmPx, BracketThicknessPx), new Vector2(1f, 0f));
            AddCornerArm(markerGo.transform, "BR_V", new Vector2(half - inset, -half + inset), new Vector2(BracketThicknessPx, BracketArmPx), new Vector2(1f, 0f));

            var pipGo = new GameObject("Pip");
            pipGo.transform.SetParent(markerGo.transform, false);
            var pipRt = pipGo.AddComponent<RectTransform>();
            pipRt.anchorMin = new Vector2(0.5f, 0.5f);
            pipRt.anchorMax = new Vector2(0.5f, 0.5f);
            pipRt.pivot = new Vector2(0.5f, 0.5f);
            pipRt.anchoredPosition = Vector2.zero;
            pipRt.sizeDelta = new Vector2(PipSizePx, PipSizePx);
            var pip = pipGo.AddComponent<Image>();
            pip.sprite = CreateFilledSprite();
            pip.color = MarkerWhite;
            pip.raycastTarget = false;

            if (hud != null)
                _root.transform.SetAsLastSibling();
        }

        private static void AddCornerArm(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            Vector2 pivot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = MarkerWhite;
            img.raycastTarget = false;
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
