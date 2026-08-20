using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    internal static partial class ThrottleController
    {
        private static bool _externalBoostHeld;

        /// <summary>External throttle channel (Bridge) — absolute set, same clamp as the keybind
        /// ramp. Persists until changed again (Tick only moves _throttle while a physical key is
        /// actually held), so callers set-and-forget rather than resending every frame.</summary>
        internal static void SetExternal(float value01)
        {
            _throttle = Mathf.Clamp01(value01);
        }

        /// <summary>External throttle channel (Bridge) — relative nudge, mirrors the tap step.</summary>
        internal static void AdjustExternal(float delta)
        {
            _throttle = Mathf.Clamp01(_throttle + delta);
        }

        /// <summary>External afterburner hold (Bridge). Level-triggered like a physical keybind
        /// hold — caller reports held/released, ResolveBoost ORs it with the keybind each tick.</summary>
        internal static void SetExternalBoost(bool held)
        {
            _externalBoostHeld = held;
        }
    }
}
