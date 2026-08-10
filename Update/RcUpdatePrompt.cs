using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Update
{
    /// <summary>
    /// One-shot EN update prompt. Shown as soon as GitHub latest &gt; AppVersion; silent when offline.
    /// </summary>
    internal sealed class RcUpdatePrompt : MonoBehaviour
    {
        private static bool _offeredThisSession;
        private static RcUpdatePrompt? _instance;

        private bool _visible;
        private bool _dontShowAgain;
        private Rect _window = new Rect(0f, 0f, 440f, 200f);
        private GUIStyle? _boxStyle;
        private GUIStyle? _bodyStyle;

        internal static void EnsureOn(GameObject host)
        {
            if (host == null || _instance != null)
                return;

            _instance = host.GetComponent<RcUpdatePrompt>();
            if (_instance == null)
                _instance = host.AddComponent<RcUpdatePrompt>();
        }

        private void Update()
        {
            if (_offeredThisSession || _visible)
                return;
            if (!RcUpdateChecker.IsCompleted || !RcUpdateChecker.IsOutdated)
                return;
            if (!RcConfig.IsBound
                || !RcConfig.CheckForUpdates.Value
                || RcConfig.UpdatePromptDontShowAgain.Value)
                return;

            _offeredThisSession = true;
            _visible = true;
            _dontShowAgain = false;
            _window.x = (Screen.width - _window.width) * 0.5f;
            // Stack below typical MC prompt if both appear at once.
            _window.y = (Screen.height - _window.height) * 0.52f;
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            EnsureStyles();
            _window = GUI.ModalWindow(
                0x52435550, // RCUP
                _window,
                DrawWindow,
                "MissileCamera Remote Control — Update Available",
                _boxStyle);
        }

        private void DrawWindow(int id)
        {
            string latest = RcUpdateChecker.LatestTag;
            if (string.IsNullOrEmpty(latest))
                latest = "newer";

            GUILayout.Space(6f);
            GUILayout.Label(
                "A newer full release is available on GitHub.\n"
                + "Installed: " + AppVersion.DisplayVersion
                + "    Latest: " + latest,
                _bodyStyle);

            if (McUpdateStatusPeek.TryGet(out _, out bool mcOutdated, out string mcLatest, out string mcInstalled)
                && mcOutdated)
            {
                if (string.IsNullOrEmpty(mcLatest))
                    mcLatest = "newer";
                if (string.IsNullOrEmpty(mcInstalled))
                    mcInstalled = "?";
                GUILayout.Space(6f);
                GUILayout.Label(
                    "Note: Missile Camera also has an update available "
                    + "(installed " + mcInstalled + ", latest " + mcLatest + ").",
                    _bodyStyle);
            }

            GUILayout.Space(10f);
            _dontShowAgain = GUILayout.Toggle(_dontShowAgain, " Don't show again");

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open download page", GUILayout.Height(28f)))
            {
                OpenReleasePage();
                Dismiss(saveDontShow: _dontShowAgain);
            }

            if (GUILayout.Button("Later", GUILayout.Width(90f), GUILayout.Height(28f)))
                Dismiss(saveDontShow: _dontShowAgain);

            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void OpenReleasePage()
        {
            string url = RcUpdateChecker.ReleaseUrl;
            if (string.IsNullOrEmpty(url))
                url = "https://github.com/Mursisru/MissileCamera-Remote-Control/releases/latest";
            try
            {
                Application.OpenURL(url);
            }
            catch
            {
                // ignore
            }
        }

        private void Dismiss(bool saveDontShow)
        {
            _visible = false;
            if (!saveDontShow || !RcConfig.IsBound)
                return;
            try
            {
                RcConfig.UpdatePromptDontShowAgain.Value = true;
            }
            catch
            {
                // ignore
            }
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null)
                return;

            _boxStyle = new GUIStyle(GUI.skin.window);
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = 13
            };
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
