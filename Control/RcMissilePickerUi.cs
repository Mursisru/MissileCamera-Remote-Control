using System.Collections.Generic;
using System.Text;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;
using UnityEngine.UI;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>FS overlay list of allied RC missiles — L to open, Up/Down, Enter to take, Esc closes.</summary>
    internal static class RcMissilePickerUi
    {
        private static GameObject? _root;
        private static TMPro.TextMeshProUGUI? _body;
        private static bool _open;
        private static int _index;
        private static readonly List<Missile> _items = new List<Missile>(16);
        private static readonly StringBuilder _sb = new StringBuilder(512);

        internal static bool IsOpen => _open;

        internal static void Toggle()
        {
            if (!MissileCameraFsAccess.IsFullscreenActive)
                return;
            if (_open)
                Close();
            else
                Open();
        }

        internal static void Open()
        {
            if (!MissileCameraFsAccess.IsFullscreenActive)
                return;
            RemoteControlSession.RefreshPool();
            _items.Clear();
            IReadOnlyList<Missile> pool = RemoteControlSession.Pool;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && !pool[i].disabled)
                    _items.Add(pool[i]);
            }

            if (_items.Count == 0)
            {
                RcPlugin.ModLogger?.LogInfo("RC list: no allied clone missiles.");
                return;
            }

            _index = 0;
            _open = true;
            EnsureUi();
            if (_root != null)
                _root.SetActive(true);
            RefreshText();
        }

        internal static void Close()
        {
            _open = false;
            if (_root != null)
                _root.SetActive(false);
        }

        internal static void DestroyUi()
        {
            Close();
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                _body = null;
            }
            _items.Clear();
        }

        internal static void Tick()
        {
            if (!_open)
                return;

            if (!MissileCameraFsAccess.IsFullscreenActive)
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                _index--;
                if (_index < 0)
                    _index = _items.Count - 1;
                RefreshText();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                _index++;
                if (_index >= _items.Count)
                    _index = 0;
                RefreshText();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_index >= 0 && _index < _items.Count)
                {
                    Missile m = _items[_index];
                    Close();
                    if (m != null && !m.disabled)
                        RemoteControlSession.Take(m);
                }
            }
        }

        private static void RefreshText()
        {
            if (_body == null)
                return;

            Vector3 origin = RemoteControlSession.GetPoolOrigin();
            _sb.Length = 0;
            _sb.AppendLine("RC MISSILES  (Enter=Take  Esc=Close)");
            _sb.AppendLine();

            for (int i = 0; i < _items.Count; i++)
            {
                Missile m = _items[i];
                if (m == null)
                    continue;

                string mark = i == _index ? ">" : " ";
                string name = m.unitName;
                if (string.IsNullOrEmpty(name))
                    name = m.name;

                string guide = "DL";
                RcMissileTag? tag = m.GetComponent<RcMissileTag>();
                if (tag != null)
                    guide = GuidanceLabels.For(tag.Guidance);

                float distKm = (m.transform.position - origin).magnitude * 0.001f;
                RcLinkLevel peek = PeekLink(m, tag);
                string link = peek == RcLinkLevel.Full ? "OK"
                    : peek == RcLinkLevel.Degraded ? "WEAK"
                    : "LOST";

                _sb.Append(mark).Append(' ')
                    .Append(name).Append("  [").Append(guide).Append("]  ")
                    .Append(distKm.ToString("0.0")).Append("km  ")
                    .Append(link)
                    .AppendLine();
            }

            _body.text = _sb.ToString();
        }

        private static RcLinkLevel PeekLink(Missile missile, RcMissileTag? tag)
        {
            if (tag != null && tag.Guidance == RcGuidanceKind.Satcom)
                return RcLinkLevel.Full;

            // Lightweight peek for list — reuse last eval if this is controlled, else assume Full if in mesh scan too heavy.
            if (RemoteControlSession.IsControlling(missile))
                return RcLinkQuality.Current;

            try
            {
                FactionHQ? hq = missile.NetworkHQ;
                if (hq == null)
                    return RcLinkLevel.Lost;
                GlobalPosition mp = missile.GlobalPosition();
                float mesh = Mathf.Max(1000f, RcConfig.MeshRangeM.Value);
                var units = UnitRegistry.allUnits;
                if (units == null)
                    return RcLinkLevel.Lost;
                bool inRange = false;
                for (int i = 0; i < units.Count; i++)
                {
                    Unit? u = units[i];
                    if (u == null || u.disabled || ReferenceEquals(u, missile))
                        continue;
                    if (u.NetworkHQ != hq)
                        continue;
                    if (!FastMath.InRange(mp, u.GlobalPosition(), mesh))
                        continue;
                    inRange = true;
                    if (TargetCalc.LineOfSight(missile.transform, u.transform, 40f))
                        return RcLinkLevel.Full;
                }
                return inRange ? RcLinkLevel.Degraded : RcLinkLevel.Lost;
            }
            catch
            {
                return RcLinkLevel.Full;
            }
        }

        private static void EnsureUi()
        {
            if (_root != null)
                return;

            _root = new GameObject("MissileCameraRemoteControl.MissilePicker");
            Object.DontDestroyOnLoad(_root);
            _root.hideFlags = HideFlags.HideAndDontSave;

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32100;
            _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _root.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_root.transform, false);
            var panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(520f, 360f);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0.05f, 0.07f, 0.08f, 0.88f);
            img.raycastTarget = false;

            var textGo = new GameObject("Body");
            textGo.transform.SetParent(panel.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 16f);
            textRt.offsetMax = new Vector2(-16f, -16f);

            _body = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            _body.fontSize = 18f;
            _body.alignment = TMPro.TextAlignmentOptions.TopLeft;
            _body.color = new Color(0.85f, 0.92f, 0.8f, 1f);
            _body.raycastTarget = false;
            _body.enableWordWrapping = false;
            _body.overflowMode = TMPro.TextOverflowModes.Overflow;

            _root.SetActive(false);
        }
    }
}
