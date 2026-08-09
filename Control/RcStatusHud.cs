using UnityEngine;
using UnityEngine.UI;
using MissileCameraRemoteControl.Access;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Status banners from actual RC state:
    /// FOLLOW (top, white) when formation on;
    /// AFTERBURNER ACTIVE when AB bind held AND fuel remains (independent of FOLLOW).
    /// </summary>
    internal static class RcStatusHud
    {
        private const int FollowFont = 14;
        private const int AfterburnerFont = 17;

        private static GameObject? _root;
        private static Text? _ab;
        private static Text? _follow;
        private static bool _abOn;
        private static bool _followOn;

        internal static void Tick()
        {
            bool follow = RcFormationFollow.IsActive;
            Missile? lead = RemoteControlSession.Controlled;
            bool wantAb = RemoteControlSession.IsActive
                && ThrottleController.BoostActive
                && MissileAccess.HasMotorFuel(lead);

            if (!wantAb && !follow)
            {
                if (_root != null && _root.activeSelf)
                    _root.SetActive(false);
                _abOn = false;
                _followOn = false;
                return;
            }

            EnsureUi();
            if (_root == null)
                return;

            if (!_root.activeSelf)
                _root.SetActive(true);

            if (_ab != null && wantAb != _abOn)
            {
                _abOn = wantAb;
                _ab.gameObject.SetActive(wantAb);
            }

            if (_follow != null && follow != _followOn)
            {
                _followOn = follow;
                _follow.gameObject.SetActive(follow);
            }
        }

        internal static void DestroyUi()
        {
            _abOn = false;
            _followOn = false;
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                _ab = null;
                _follow = null;
            }
        }

        private static void EnsureUi()
        {
            if (_root != null)
                return;

            _root = new GameObject("MissileCameraRemoteControl.StatusHud");
            Object.DontDestroyOnLoad(_root);
            _root.hideFlags = HideFlags.HideAndDontSave;

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32050;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _root.AddComponent<GraphicRaycaster>().enabled = false;

            _follow = CreateLabel(
                _root.transform, "Follow", TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -28f),
                Color.white, FollowFont, FontStyle.Normal);
            _follow.text = "FOLLOW";
            _follow.gameObject.SetActive(false);

            _ab = CreateLabel(
                _root.transform, "Afterburner", TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Color(1f, 0.2f, 0.15f, 0.95f), AfterburnerFont, FontStyle.Bold);
            _ab.text = "AFTERBURNER ACTIVE";
            _ab.gameObject.SetActive(false);

            _root.SetActive(false);
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            TextAnchor align,
            Vector2 anchorVec,
            Vector2 anchoredPos,
            Color color,
            int fontSize,
            FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorVec;
            rt.anchorMax = anchorVec;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(480f, 28f);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
