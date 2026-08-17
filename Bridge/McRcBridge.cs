using System.Collections.Generic;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Control;
using UnityEngine;

namespace MissileCameraRemoteControl.Bridge
{
    /// <summary>
    /// Small, stable, PUBLIC surface for third-party mods (external MFD/HUD displays, streaming
    /// overlays, HOTAS companion apps) to read RC state and inject input without reflecting into
    /// internal types. Everything here is a thin forward onto the existing internal control paths
    /// — Bridge adds no new behavior, it only exposes what RemoteControlSession /
    /// MouseGuidanceController / ThrottleController already do for the physical keyboard/mouse.
    ///
    /// Contract:
    /// - Every member is safe to call every frame, from any thread that already marshals onto the
    ///   Unity main thread (this class itself does no threading — callers are responsible for
    ///   calling in from Update/FixedUpdate or an equivalent main-thread pump).
    /// - Every member no-ops / returns a default when RC is not installed-and-active, a soft
    ///   dependency (missing MissileCamera, not in fullscreen, not currently controlling) rather
    ///   than throwing — callers should not need a try/catch per call.
    /// - ApiVersion bumps on any breaking change to this class's members (not on additions).
    ///   Consumers should check it once at startup and disable integration below the version they
    ///   were built against.
    /// </summary>
    public static class McRcBridge
    {
        /// <summary>Bump on breaking changes only (removed/renamed/resignatured members).</summary>
        public const int ApiVersion = 1;

        // ── State ──────────────────────────────────────────────────────────────

        /// <summary>MissileCamera fullscreen feed is up (independent of whether RC has taken a
        /// missile yet — matches RcFrameCache.IsFullscreenActive).</summary>
        public static bool IsFullscreenActive => RcFrameCache.IsFullscreenActive;

        /// <summary>An RC session is actively controlling a missile right now.</summary>
        public static bool IsControlling => RemoteControlSession.IsActive;

        /// <summary>The feed camera currently driving the in-cockpit MissileCamera panel — same
        /// Camera a consumer would read targetTexture off of to mirror the picture. Null when no
        /// feed is live.</summary>
        public static Camera? FeedCamera => RcFrameCache.FeedCamera;

        /// <summary>Display name of the missile currently under control, or empty string.</summary>
        public static string ControlledMissileName
        {
            get
            {
                Missile? m = RemoteControlSession.Controlled;
                if (m == null) return string.Empty;
                try { return m.unitName ?? m.name ?? string.Empty; }
                catch { return string.Empty; }
            }
        }

        /// <summary>Current UI throttle, 0..1 (matches the MissileCamera THR gauge).</summary>
        public static float Throttle01 => ThrottleController.UiThrottle;

        /// <summary>Afterburner currently applying (physical OR external hold, AND'd with fuel —
        /// same value the in-cockpit VFX binds to).</summary>
        public static bool BoostActive => ThrottleController.BoostActive;

        /// <summary>"Full" | "Degraded" | "Lost" — mirrors RcStatusHud's own label so a browser
        /// readout matches the in-cockpit one exactly.</summary>
        public static string LinkQuality => RcLinkQuality.Current.ToString();

        /// <summary>Commanded aim reticle position in feed-camera viewport space (0..1, 0..1) —
        /// the same point FsAimReticle draws in-cockpit. Use this to overlay a synced cursor on a
        /// mirrored feed image rather than reprojecting aim yourself.</summary>
        public static Vector2 ReticleViewport => MouseGuidanceController.GetReticleViewport();

        /// <summary>Formation follow is currently engaged from the controlled missile.</summary>
        public static bool FormationFollowActive => RcFormationFollow.IsActive;

        /// <summary>Display names of missiles available to take control of right now (the same
        /// pool RcMissilePickerUi lists). Call RefreshPool() first if you want a fresh scan —
        /// this getter does not implicitly rescan, so repeated reads in one frame are cheap and
        /// stay index-consistent with TakeAt.</summary>
        public static IReadOnlyList<string> ControllablePool
        {
            get
            {
                IReadOnlyList<Missile> pool = RemoteControlSession.Pool;
                List<string> names = new List<string>(pool.Count);
                for (int i = 0; i < pool.Count; i++)
                {
                    Missile m = pool[i];
                    string name = string.Empty;
                    if (m != null)
                    {
                        try { name = m.unitName ?? m.name ?? string.Empty; }
                        catch { name = string.Empty; }
                    }
                    names.Add(name);
                }
                return names;
            }
        }

        // ── Commands ───────────────────────────────────────────────────────────

        /// <summary>Rescan the world for controllable allied missiles — call before reading
        /// ControllablePool / TakeAt if you want it current (e.g. right before opening a picker
        /// UI); RC itself keeps the pool fresh continuously while a session is active.</summary>
        public static void RefreshPool() => RemoteControlSession.RefreshPool();

        /// <summary>Take the best available missile (prefers whatever MissileCamera's feed is
        /// already following). Returns false if fullscreen isn't up or nothing is available —
        /// never releases an existing session first, unlike the in-game toggle key.</summary>
        public static bool TakeNearest() => RemoteControlSession.TryTakeNearest();

        /// <summary>Take the missile at this index into ControllablePool (call RefreshPool first
        /// for a current list). Returns false on an out-of-range or no-longer-valid index.</summary>
        public static bool TakeAt(int index) => RemoteControlSession.TryTakeAt(index);

        /// <summary>Release the current RC session, if any (no-op otherwise).</summary>
        public static void Release() => RemoteControlSession.Release();

        /// <summary>Add to the pending aim command, same buffer the physical mouse feeds
        /// (PollMouse) — degrees, yaw right positive, pitch up negative. Call this from a
        /// pointer-drag handler with the frame's delta; small deltas at high frequency read
        /// exactly like physical mouse motion. No-op while nothing is being controlled.</summary>
        public static void InjectAimDelta(float yawDeltaDeg, float pitchDeltaDeg) =>
            MouseGuidanceController.InjectExternal(yawDeltaDeg, pitchDeltaDeg);

        /// <summary>Set throttle to an absolute 0..1 value. Persists until you change it again or
        /// a physical throttle key is pressed — no need to resend every frame.</summary>
        public static void SetThrottle01(float value01) => ThrottleController.SetExternal(value01);

        /// <summary>Nudge throttle by a relative amount, clamped to 0..1.</summary>
        public static void AdjustThrottle(float delta) => ThrottleController.AdjustExternal(delta);

        /// <summary>Afterburner hold state — level-triggered like a physical keybind hold. Call
        /// with true on press/touch-start and false on release/touch-end; do not need to resend
        /// while held.</summary>
        public static void SetBoostHeld(bool held) => ThrottleController.SetExternalBoost(held);

        /// <summary>Toggle formation-follow from the currently controlled missile (no-op if
        /// nothing is controlled).</summary>
        public static void ToggleFormationFollow() => RcFormationFollow.ToggleFromControlled();

        /// <summary>Manual detonate on the controlled missile, same guards (nuclear-airburst
        /// block, authority, armed state) as the physical key. Returns false if the guards
        /// rejected it or nothing is controlled.</summary>
        public static bool ManualDetonate() => RcManualDetonate.TriggerExternal();
    }
}
