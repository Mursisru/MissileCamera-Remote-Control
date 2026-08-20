using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// WT aim: reticle = desired; SetAimpoint = command (gLimit slew, full converge to marker).
    /// </summary>
    // Bridge/MouseGuidanceController.Bridge.cs holds the external-consumer half (InjectExternal)
    // as a partial-class extension. This file keeps only the one-line gate in Tick() where
    // existing logic itself needs to widen to respect PhysicalAimEnabled.
    internal static partial class MouseGuidanceController
    {
        private const float MouseDegPerUnit = 1.25f;
        private const float MouseDeadzone = 0.02f;
        private const float KeyAimDegPerSec = 55f;
        private const float MaxPitchSin = 0.995f;
        private const float ProjectDistance = 2000f;
        /// <summary>Desired stick cap per Fixed (reticle). Command is slower via lag+G.</summary>
        private const float MaxDesiredStepDeg = 25f;
        private const float SnapArriveDeg = 0.35f;
        private const float MinSteerSpeed = 5f;

        private static Vector3 _desiredAimDir = Vector3.forward;
        private static Vector3 _commandAimDir = Vector3.forward;
        private static bool _initialized;
        private static Vector2 _lastStableViewport = new Vector2(0.5f, 0.5f);
        private static Missile? _lastMissile;
        private static float _pendingYawDeg;
        private static float _pendingPitchDeg;
        private static Vector3 _lastAimLocal;
        private static bool _hasLastAim;
        private static readonly Vector3[] _viewCorners = new Vector3[4];

        /// <summary>Commanded aim (nose / SetAimpoint) — formation shares this.</summary>
        internal static Vector3 WorldAimDir => _commandAimDir;

        internal static Vector3 DesiredAimDir => _desiredAimDir;

        internal static Vector2 GetReticleViewport() => _lastStableViewport;

        internal static bool TryGetLastAimLocal(out Vector3 aimLocal)
        {
            aimLocal = _lastAimLocal;
            return _hasLastAim && _lastAimLocal.sqrMagnitude > 1f;
        }

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
            _desiredAimDir = Vector3.forward;
            _commandAimDir = Vector3.forward;
            _lastStableViewport = new Vector2(0.5f, 0.5f);
            _lastMissile = null;
            _pendingYawDeg = 0f;
            _pendingPitchDeg = 0f;
            _hasLastAim = false;
            _lastAimLocal = Vector3.zero;
            FsAimReticle.SetVisible(false);
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            _lastMissile = missile;

            if (!RcConfig.PhysicalAimEnabled.Value)
                return;   // external aim (InjectExternal) is a separate channel — unaffected by this

            if (Input.GetMouseButton(1))
                return;

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

        /// <summary>
        /// Reticle = looking feed viewport of desired aim (same plane as MC bore HUD).
        /// Free-look must NOT use bore-viewport math — that glued the marker to screen center.
        /// </summary>
        internal static void LateProject()
        {
            Missile? missile = _lastMissile;
            if (missile == null || missile.disabled)
                return;
            if (!RemoteControlSession.IsControlling(missile))
                return;

            Camera? feed = MissileCameraFsAccess.TryGetFeedCamera();
            Transform mt = missile.transform;

            Vector3 aimDir = _desiredAimDir.sqrMagnitude > 1e-8f ? _desiredAimDir.normalized : mt.forward;

            if (feed != null)
            {
                // Looking camera (incl. RMB free-look offset) — marker stays with MC main reticle.
                Vector3 projectPoint = feed.transform.position + aimDir * ProjectDistance;
                Vector3 vp = feed.WorldToViewportPoint(projectPoint);
                bool projected = vp.z > 0.05f
                    && !float.IsNaN(vp.x) && !float.IsNaN(vp.y)
                    && !float.IsInfinity(vp.x) && !float.IsInfinity(vp.y);

                if (projected)
                {
                    vp.x = Mathf.Clamp(vp.x, -0.05f, 1.05f);
                    vp.y = Mathf.Clamp(vp.y, -0.05f, 1.05f);
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

        internal static void ReinforceAimpoint(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;
            if (!RemoteControlSession.OwnsMissile(missile))
                return;

            Transform mt = missile.transform;
            ConsumeDesired(missile, mt);
            SlewCommandTowardDesired(missile, mt);
            WriteAimpoint(missile, mt);
        }

        private static void ConsumeDesired(Missile missile, Transform mt)
        {
            if (!_initialized)
            {
                Vector3 fwd = ResolveSteerRef(missile, mt);
                _desiredAimDir = ClampPitch(fwd, fwd);
                _commandAimDir = _desiredAimDir;
                _initialized = true;
            }

            float yaw = Mathf.Clamp(_pendingYawDeg, -MaxDesiredStepDeg, MaxDesiredStepDeg);
            float pitch = Mathf.Clamp(_pendingPitchDeg, -MaxDesiredStepDeg, MaxDesiredStepDeg);
            _pendingYawDeg -= yaw;
            _pendingPitchDeg -= pitch;
            if (Mathf.Abs(_pendingYawDeg) > MaxDesiredStepDeg * 4f)
                _pendingYawDeg = Mathf.Clamp(_pendingYawDeg, -MaxDesiredStepDeg, MaxDesiredStepDeg);
            if (Mathf.Abs(_pendingPitchDeg) > MaxDesiredStepDeg * 4f)
                _pendingPitchDeg = Mathf.Clamp(_pendingPitchDeg, -MaxDesiredStepDeg, MaxDesiredStepDeg);

            Vector3 prev = _desiredAimDir;
            if (yaw * yaw + pitch * pitch > 1e-10f)
                ApplyMouseToDesired(mt, yaw, pitch);

            if (_desiredAimDir.sqrMagnitude > 1e-8f)
                _desiredAimDir = _desiredAimDir.normalized;
            else
                _desiredAimDir = ResolveSteerRef(missile, mt);

            if (prev.sqrMagnitude > 1e-6f)
            {
                float ang = Vector3.Angle(prev, _desiredAimDir);
                if (ang > MaxDesiredStepDeg + 0.5f)
                    _desiredAimDir = Vector3.RotateTowards(
                        prev.normalized, _desiredAimDir, MaxDesiredStepDeg * Mathf.Deg2Rad, 0f);
            }
        }

        /// <summary>
        /// Command always drives to the marker at stock gLimit rate (no soft-lag asymptote).
        /// Reticle can lead the nose while the stick moves; idle ⇒ full converge.
        /// </summary>
        private static void SlewCommandTowardDesired(Missile missile, Transform mt)
        {
            float dt = Mathf.Max(Time.fixedDeltaTime, 1f / 120f);
            Vector3 desired = _desiredAimDir.sqrMagnitude > 1e-8f
                ? _desiredAimDir.normalized
                : ResolveSteerRef(missile, mt);
            Vector3 cmd = _commandAimDir.sqrMagnitude > 1e-8f
                ? _commandAimDir.normalized
                : desired;

            float omegaMax = MissileAccess.GetMaxTurnRateRad(missile);
            // Optional ease: AimLagSeconds slows command without leaving a permanent gap.
            float lag = Mathf.Max(0f, RcConfig.AimLagSeconds.Value);
            if (lag > 0.01f)
                omegaMax /= 1f + lag * 2f;

            float maxRad = Mathf.Max(omegaMax * dt, 1e-5f);
            float errDeg = Vector3.Angle(cmd, desired);

            Vector3 next = errDeg <= SnapArriveDeg || errDeg * Mathf.Deg2Rad <= maxRad + 1e-4f
                ? desired
                : Vector3.RotateTowards(cmd, desired, maxRad, 0f).normalized;

            Vector3 steerRef = ResolveSteerRef(missile, mt);
            next = EnsureForwardHemisphere(next, steerRef);
            _commandAimDir = next;
        }

        private static void WriteAimpoint(Missile missile, Transform mt)
        {
            float dist = Mathf.Max(200f, RcConfig.AimDistance.Value);
            Vector3 origin = mt.position;
            Vector3 steerRef = ResolveSteerRef(missile, mt);
            Vector3 dir = _commandAimDir.sqrMagnitude > 1e-8f ? _commandAimDir.normalized : steerRef;
            dir = EnsureForwardHemisphere(dir, steerRef);
            _commandAimDir = dir;

            Vector3 aimLocal = RcBallisticImpactSafety.ResolveAimPoint(origin, dir, dist);
            Vector3 toAim = aimLocal - origin;
            if (toAim.sqrMagnitude < 1f || Vector3.Dot(toAim.normalized, steerRef) < 0.02f)
                aimLocal = origin + dir * dist;

            try
            {
                missile.SetAimpoint(aimLocal.ToGlobalPosition(), Vector3.zero);
                _lastAimLocal = aimLocal;
                _hasLastAim = true;
            }
            catch
            {
                // ignore
            }
        }

        private static Vector3 ResolveSteerRef(Missile missile, Transform mt)
        {
            Vector3 fwd = mt.forward.sqrMagnitude > 1e-6f ? mt.forward.normalized : Vector3.forward;
            if (missile.rb == null)
                return fwd;

            Vector3 vel = missile.rb.velocity;
            if (vel.sqrMagnitude < MinSteerSpeed * MinSteerSpeed)
                return fwd;

            return (fwd * 0.5f + vel.normalized * 0.5f).normalized;
        }

        /// <summary>Block rear-hemisphere aim (Steering reverse whip) without capping short of the marker.</summary>
        private static Vector3 EnsureForwardHemisphere(Vector3 dir, Vector3 steerRef)
        {
            if (dir.sqrMagnitude < 1e-8f)
                return steerRef;
            dir.Normalize();
            if (steerRef.sqrMagnitude < 1e-8f)
                return dir;
            if (Vector3.Dot(dir, steerRef) >= 0.02f)
                return dir;

            Vector3 folded = Vector3.ProjectOnPlane(dir, steerRef);
            if (folded.sqrMagnitude < 1e-6f)
                return steerRef;
            return (folded.normalized + steerRef * 0.05f).normalized;
        }

        private static void ApplyMouseToDesired(Transform mt, float yawDeltaDeg, float pitchDeltaDeg)
        {
            Vector3 dir = _desiredAimDir.sqrMagnitude > 1e-8f ? _desiredAimDir.normalized : mt.forward.normalized;
            dir = Quaternion.AngleAxis(yawDeltaDeg, Vector3.up) * dir;

            Vector3 pitchAxis = Vector3.Cross(Vector3.up, dir);
            if (pitchAxis.sqrMagnitude < 1e-6f)
            {
                pitchAxis = Vector3.ProjectOnPlane(mt.right, Vector3.up);
                if (pitchAxis.sqrMagnitude < 1e-6f)
                    pitchAxis = Vector3.right;
            }

            pitchAxis.Normalize();
            dir = Quaternion.AngleAxis(pitchDeltaDeg, pitchAxis) * dir;
            if (dir.sqrMagnitude < 1e-6f)
                return;

            _desiredAimDir = ClampPitch(dir.normalized, mt.forward);
        }

        private static Vector3 ClampPitch(Vector3 dir, Vector3 headingFallback)
        {
            dir.Normalize();
            if (dir.y <= MaxPitchSin && dir.y >= -MaxPitchSin)
                return dir;

            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-8f)
            {
                Vector3 prevFlat = new Vector3(_desiredAimDir.x, 0f, _desiredAimDir.z);
                if (prevFlat.sqrMagnitude > 1e-8f)
                    flat = prevFlat;
                else
                {
                    Vector3 fb = new Vector3(headingFallback.x, 0f, headingFallback.z);
                    flat = fb.sqrMagnitude > 1e-8f ? fb : Vector3.forward;
                }
            }

            flat.Normalize();
            float y = Mathf.Sign(dir.y) * MaxPitchSin;
            float horiz = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return (flat * horiz + Vector3.up * y).normalized;
        }
    }
}
