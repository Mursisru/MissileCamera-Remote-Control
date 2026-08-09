using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// World-space WT aim. Mouse queues deltas; LateProject updates reticle after SyncPose.
    /// ReinforceAimpoint runs on Steering prefix (after Seek) so ballistic/cruise Seek cannot steal stick.
    /// Anti-flip: nose init, zenith heading hold, per-step aim cap, never write aim behind nose.
    /// </summary>
    internal static class MouseGuidanceController
    {
        private const float MouseDegPerUnit = 1.25f;
        private const float MouseDeadzone = 0.02f;
        private const float KeyAimDegPerSec = 55f;
        private const float MaxPitchSin = 0.9998f;
        private const float ProjectDistance = 2000f;
        /// <summary>Caps Alt-Tab / focus spikes that would spin aim ~180° in one Consume.</summary>
        private const float MaxAimStepDeg = 70f;

        private static Vector3 _worldAimDir = Vector3.forward;
        private static bool _initialized;
        private static Vector2 _lastStableViewport = new Vector2(0.5f, 0.5f);
        private static Missile? _lastMissile;
        private static float _pendingYawDeg;
        private static float _pendingPitchDeg;
        private static int _aimWrittenFrame = -1;
        private static readonly Vector3[] _viewCorners = new Vector3[4];

        internal static Vector2 GetReticleViewport() => _lastStableViewport;

        internal static Vector3 WorldAimDir => _worldAimDir;

        internal static Vector2 GetReticleScreenPosition()
        {
            RectTransform? view = MissileCameraFsAccess.TryGetFeedViewRect();
            if (view != null)
            {
                view.GetWorldCorners(_viewCorners);
                float x = Mathf.Lerp(_viewCorners[0].x, _viewCorners[2].x, _lastStableViewport.x);
                float y = Mathf.Lerp(_viewCorners[0].y, _viewCorners[2].y, _lastStableViewport.y);
                return new Vector2(x, y);
            }

            return new Vector2(_lastStableViewport.x * Screen.width, _lastStableViewport.y * Screen.height);
        }

        internal static void Reset()
        {
            _initialized = false;
            _worldAimDir = Vector3.forward;
            _lastStableViewport = new Vector2(0.5f, 0.5f);
            _lastMissile = null;
            _pendingYawDeg = 0f;
            _pendingPitchDeg = 0f;
            _aimWrittenFrame = -1;
            FsAimReticle.SetVisible(false);
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            _lastMissile = missile;

            RcAimInputMode mode = RcConfig.AimInputMode.Value;
            if (mode == RcAimInputMode.Mouse)
            {
                PollMouse();
            }
            else
            {
                float rate = KeyAimDegPerSec
                    * Mathf.Max(0.05f, RcConfig.KeyAimSensitivity.Value)
                    * Time.unscaledDeltaTime;
                PollKeyScheme(mode, rate);
            }
        }

        private static void PollMouse()
        {
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            if (mx * mx + my * my < MouseDeadzone * MouseDeadzone)
                return;
            float sens = Mathf.Max(0.02f, RcConfig.MouseSensitivity.Value) * MouseDegPerUnit;
            _pendingYawDeg += mx * sens;
            _pendingPitchDeg += -my * sens;
        }

        private static void PollKeyScheme(RcAimInputMode mode, float rate)
        {
            switch (mode)
            {
                case RcAimInputMode.WASD:
                    if (Input.GetKey(KeyCode.A)) _pendingYawDeg -= rate;
                    if (Input.GetKey(KeyCode.D)) _pendingYawDeg += rate;
                    if (Input.GetKey(KeyCode.W)) _pendingPitchDeg -= rate;
                    if (Input.GetKey(KeyCode.S)) _pendingPitchDeg += rate;
                    break;
                case RcAimInputMode.Arrows:
                    if (Input.GetKey(KeyCode.LeftArrow)) _pendingYawDeg -= rate;
                    if (Input.GetKey(KeyCode.RightArrow)) _pendingYawDeg += rate;
                    if (Input.GetKey(KeyCode.UpArrow)) _pendingPitchDeg -= rate;
                    if (Input.GetKey(KeyCode.DownArrow)) _pendingPitchDeg += rate;
                    break;
                case RcAimInputMode.NumPadArrows:
                    if (Input.GetKey(KeyCode.Keypad4)) _pendingYawDeg -= rate;
                    if (Input.GetKey(KeyCode.Keypad6)) _pendingYawDeg += rate;
                    if (Input.GetKey(KeyCode.Keypad8)) _pendingPitchDeg -= rate;
                    // Keypad2 = down; Keypad5 also common as “down” on some layouts.
                    if (Input.GetKey(KeyCode.Keypad2) || Input.GetKey(KeyCode.Keypad5))
                        _pendingPitchDeg += rate;
                    break;
                case RcAimInputMode.Custom:
                    if (KeybindPoll.IsHeld(RcConfig.AimYawLeft.Value)) _pendingYawDeg -= rate;
                    if (KeybindPoll.IsHeld(RcConfig.AimYawRight.Value)) _pendingYawDeg += rate;
                    if (KeybindPoll.IsHeld(RcConfig.AimPitchUp.Value)) _pendingPitchDeg -= rate;
                    if (KeybindPoll.IsHeld(RcConfig.AimPitchDown.Value)) _pendingPitchDeg += rate;
                    break;
            }
        }

        /// <summary>After MC SyncPose — project reticle; WriteAim only if mouse pending (Steering owns aim).</summary>
        internal static void LateProject()
        {
            Missile? missile = _lastMissile;
            if (missile == null || missile.disabled)
                return;
            if (!RemoteControlSession.IsControlling(missile))
                return;

            Camera? feed = MissileCameraFsAccess.TryGetFeedCamera();
            Transform mt = missile.transform;
            Transform view = feed != null ? feed.transform : mt;

            bool hadPending = _pendingYawDeg * _pendingYawDeg + _pendingPitchDeg * _pendingPitchDeg > 1e-10f;
            ConsumePendingAndEnsureInit(view, mt);

            // Steering Prefix is aim authority vs GSN; EOF only rewrites if new stick input since Fixed.
            if (hadPending || _aimWrittenFrame != Time.frameCount)
                WriteAimpoint(missile, mt);

            Vector3 projectPoint = view.position + _worldAimDir * ProjectDistance;
            if (feed != null)
            {
                Vector3 vp = feed.WorldToViewportPoint(projectPoint);
                if (vp.z > 0.05f
                    && !float.IsNaN(vp.x) && !float.IsNaN(vp.y)
                    && !float.IsInfinity(vp.x) && !float.IsInfinity(vp.y))
                {
                    _lastStableViewport.x = vp.x;
                    _lastStableViewport.y = vp.y;
                    FsAimReticle.SetFromViewport(vp.x, vp.y, inFront: true);
                    return;
                }

                FsAimReticle.SetFromViewport(_lastStableViewport.x, _lastStableViewport.y, inFront: true);
                return;
            }

            FsAimReticle.SetFromViewport(0.5f, 0.5f, inFront: true);
        }

        /// <summary>
        /// Called from Missile.Steering Prefix — after Seek in ServerFixedUpdate.
        /// Restores player aim so ballistic/cruise Seek cannot own the stick.
        /// </summary>
        internal static void ReinforceAimpoint(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;
            if (!RemoteControlSession.OwnsMissile(missile))
                return;

            Transform mt = missile.transform;
            Camera? feed = MissileCameraFsAccess.TryGetFeedCamera();
            Transform view = feed != null ? feed.transform : mt;
            ConsumePendingAndEnsureInit(view, mt);
            WriteAimpoint(missile, mt);
        }

        private static void ConsumePendingAndEnsureInit(Transform view, Transform mt)
        {
            if (!_initialized)
            {
                // Prefer nose: feed can briefly face opposite and seed a 180° stick.
                Vector3 fwd = mt.forward;
                if (fwd.sqrMagnitude < 1e-6f)
                    fwd = view.forward;
                _worldAimDir = ClampPitch(fwd.normalized);
                _initialized = true;
            }

            float yaw = Mathf.Clamp(_pendingYawDeg, -MaxAimStepDeg, MaxAimStepDeg);
            float pitch = Mathf.Clamp(_pendingPitchDeg, -MaxAimStepDeg, MaxAimStepDeg);
            _pendingYawDeg = 0f;
            _pendingPitchDeg = 0f;

            Vector3 prev = _worldAimDir;
            if (yaw * yaw + pitch * pitch > 1e-10f)
                ApplyMouseToWorldAim(view, yaw, pitch);

            if (_worldAimDir.sqrMagnitude > 1e-8f)
                _worldAimDir = _worldAimDir.normalized;
            else
                _worldAimDir = mt.forward.sqrMagnitude > 1e-6f
                    ? mt.forward.normalized
                    : view.forward.normalized;

            // Absolute flip guard (weird view axes / near-zenith math).
            if (prev.sqrMagnitude > 1e-6f)
            {
                float stepRad = MaxAimStepDeg * Mathf.Deg2Rad;
                if (Vector3.Angle(prev, _worldAimDir) > MaxAimStepDeg)
                    _worldAimDir = Vector3.RotateTowards(prev.normalized, _worldAimDir, stepRad, 0f);
            }
        }

        private static void WriteAimpoint(Missile missile, Transform mt)
        {
            float dist = Mathf.Max(200f, RcConfig.AimDistance.Value);
            Vector3 origin = mt.position;
            Vector3 nose = mt.forward.sqrMagnitude > 1e-6f ? mt.forward.normalized : Vector3.forward;
            Vector3 dir = _worldAimDir.sqrMagnitude > 1e-8f ? _worldAimDir.normalized : nose;

            // Steering yanks hard if aimPoint is behind the nose — fold to forward hemisphere.
            dir = EnsureForwardHemisphere(dir, nose);

            // Ray-resolve: looking down hits terrain along look — does not flatten dive.
            Vector3 aimLocal = RcBallisticImpactSafety.ResolveAimPoint(origin, dir, dist);
            Vector3 toAim = aimLocal - origin;
            if (toAim.sqrMagnitude < 1f
                || Vector3.Dot(toAim, nose) < 0f
                || Vector3.Dot(toAim, dir) < 0f)
                aimLocal = origin + dir * dist;

            try
            {
                missile.SetAimpoint(aimLocal.ToGlobalPosition(), Vector3.zero);
                _aimWrittenFrame = Time.frameCount;
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>Never command reverse of nose (stock Steering rotates 45°/tick into a flip).</summary>
        private static Vector3 EnsureForwardHemisphere(Vector3 dir, Vector3 nose)
        {
            if (Vector3.Dot(dir, nose) >= 0.02f)
                return dir;

            Vector3 folded = Vector3.ProjectOnPlane(dir, nose);
            if (folded.sqrMagnitude < 1e-6f)
                return nose;
            return (folded.normalized + nose * 0.05f).normalized;
        }

        private static void ApplyMouseToWorldAim(Transform view, float yawDeltaDeg, float pitchDeltaDeg)
        {
            Vector3 up = view.up;
            Vector3 right = view.right;
            if (up.sqrMagnitude < 1e-8f || right.sqrMagnitude < 1e-8f)
                return;

            Vector3 dir = _worldAimDir;
            dir = Quaternion.AngleAxis(yawDeltaDeg, up.normalized) * dir;
            dir = Quaternion.AngleAxis(pitchDeltaDeg, right.normalized) * dir;
            if (dir.sqrMagnitude < 1e-6f)
                return;

            _worldAimDir = ClampPitch(dir.normalized);
        }

        private static Vector3 ClampPitch(Vector3 dir)
        {
            dir.Normalize();
            if (dir.y > MaxPitchSin || dir.y < -MaxPitchSin)
            {
                Vector3 flat = new Vector3(dir.x, 0f, dir.z);
                if (flat.sqrMagnitude < 1e-8f)
                {
                    // World +Z snap = instant heading reverse near zenith/nadir.
                    Vector3 prevFlat = new Vector3(_worldAimDir.x, 0f, _worldAimDir.z);
                    flat = prevFlat.sqrMagnitude > 1e-8f ? prevFlat : Vector3.forward;
                }

                flat.Normalize();
                float y = Mathf.Sign(dir.y) * MaxPitchSin;
                float horiz = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                dir = flat * horiz + Vector3.up * y;
            }

            return dir.normalized;
        }
    }
}
