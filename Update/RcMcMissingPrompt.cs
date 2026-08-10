using BepInEx.Bootstrap;
using UnityEngine;

namespace MissileCameraRemoteControl.Update
{
    /// <summary>Soft dependency probe for Missile Camera (GUID).</summary>
    internal static class RcMcDependency
    {
        internal static bool IsMissileCameraPresent()
        {
            try
            {
                return Chainloader.PluginInfos != null
                    && Chainloader.PluginInfos.ContainsKey(RcPlugin.MissileCameraGuid);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// EN prompt when Missile Camera is missing. Decline → RC stays off until MC is installed.
    /// </summary>
    internal sealed class RcMcMissingPrompt : MonoBehaviour
    {
        private const string McReleasesUrl = "https://github.com/Mursisru/MissileCamera/releases/latest";
        private const float MinSecondsInGame = 1.5f;

        private static bool _offeredThisSession;
        private static RcMcMissingPrompt? _instance;

        private bool _visible;
        private Rect _window = new Rect(0f, 0f, 460f, 176f);
        private GUIStyle? _boxStyle;
        private GUIStyle? _bodyStyle;

        internal static void EnsureOn(GameObject host)
        {
            if (host == null || _instance != null)
                return;

            _instance = host.GetComponent<RcMcMissingPrompt>();
            if (_instance == null)
                _instance = host.AddComponent<RcMcMissingPrompt>();
        }

        /// <summary>Lightweight DDOL host when full RC bootstrap is skipped.</summary>
        internal static void EnsureStandaloneHost()
        {
            if (_instance != null)
                return;

            var go = new GameObject("MissileCameraRemoteControl.McMissingUi");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            EnsureOn(go);
        }

        private void Update()
        {
            if (_offeredThisSession || _visible)
                return;
            if (Time.unscaledTime < MinSecondsInGame)
                return;

            // MC appeared mid-session (rare) — nothing to show.
            if (RcMcDependency.IsMissileCameraPresent())
            {
                _offeredThisSession = true;
                return;
            }

            _offeredThisSession = true;
            _visible = true;
            _window.x = (Screen.width - _window.width) * 0.5f;
            _window.y = (Screen.height - _window.height) * 0.32f;
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            EnsureStyles();
            _window = GUI.ModalWindow(
                0x52434D43, // RCMC
                _window,
                DrawWindow,
                "MissileCamera Remote Control — Dependency Missing",
                _boxStyle);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Space(6f);
            GUILayout.Label(
                "Missile Camera is required but was not found.\n"
                + "Remote Control will stay disabled until Missile Camera is installed.\n\n"
                + "Open the Missile Camera download page?",
                _bodyStyle);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open download page", GUILayout.Height(28f)))
            {
                try { Application.OpenURL(McReleasesUrl); }
                catch { /* ignore */ }
                _visible = false;
            }

            if (GUILayout.Button("Not now", GUILayout.Width(100f), GUILayout.Height(28f)))
                _visible = false;

            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
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
