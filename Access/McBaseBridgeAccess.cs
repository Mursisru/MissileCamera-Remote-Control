using System;
using System.Reflection;
using UnityEngine;

namespace MissileCameraRemoteControl.Access
{
    // Soft dependency on the BASE "Missile Camera" mod's PUBLIC Bridge (MissileCamera.Bridge.McBridge)
    // — separate from MissileCameraFsAccess.cs, which reflects into that mod's private
    // fullscreen-specific internals. This one only reads the one thing needed here: is there a
    // trackable missile the feed pipeline could show, independent of whether Fullscreen happens to
    // be open. Used by RcFrameCache to widen "IsFullscreenActive" so RC control isn't gated on the
    // pilot actually being in fullscreen when an external consumer (e.g. NOXMFD) is already keeping
    // the feed pipeline live headlessly via that same Bridge's RequestCapture.
    //
    // Same resolve-once-with-cooldown shape as every other Bridge locator in this project (RC's own
    // McRcBridge is the mirror-image of this on the other side).
    internal static class McBaseBridgeAccess
    {
        private const int MinApiVersion = 1;

        private static bool  _resolved;
        private static float _nextAttempt;

        private static Func<bool>? _hasTrackableMissile;

        internal static bool Available => EnsureResolved();
        internal static bool HasTrackableMissile => Available && _hasTrackableMissile!();

        private static bool EnsureResolved()
        {
            if (_resolved) return true;
            if (Time.unscaledTime < _nextAttempt) return false;
            _nextAttempt = Time.unscaledTime + 2f;

            try
            {
                Assembly? asm = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name == "MissileCamera") { asm = a; break; }
                }
                if (asm == null) return false;   // base mod not installed — RcFrameCache falls back to real FS only

                Type? t = asm.GetType("MissileCamera.Bridge.McBridge");
                if (t == null) return false;   // base mod too old to have a Bridge — same fallback

                FieldInfo? verField = t.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static);
                int ver = verField != null ? (int)verField.GetValue(null) : 0;
                if (ver < MinApiVersion) return false;

                MethodInfo? get = t.GetProperty("HasTrackableMissile", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
                if (get == null) return false;
                _hasTrackableMissile = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), get);

                _resolved = true;
                return true;
            }
            catch
            {
                return false;   // resolve failures here are non-fatal — RcFrameCache still has real FS
            }
        }
    }
}
