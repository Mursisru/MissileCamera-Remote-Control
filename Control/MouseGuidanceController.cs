using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// World-space WT aim. Mouse queues deltas; Steering Prefix consumes + writes aimPoint.
    /// LateProject = reticle only.
    /// Anti-flip vs stock Steering: clamp to steer-ref (fwd/vel blend), rate-limit, no upright yank.
    /// </summary>
    internal static class MouseGuidanceController
    {
        private const float MouseDegPerUnit = 1.25f;
        private const float MouseDeadzone = 0.02f;
        private const float KeyAimDegPerSec = 55f;
        private const float MaxPitchSin = 0.995f;
        private const float ProjectDistance = 2000f;
        /// <summary>Hard cap per Fixed consume (extra mouse stays queued).</summary>
        private const float MaxAimStepDeg = 10f;
        /// <summary>~deg/s stick ceiling — prevents one-frame 180° dumps into Steering.</summary>
        private const float MaxAimDegPerSec = 140f;
        /// <summary>
        /// Stock Steering yanks when Dot(aim, steerRef)&lt;0.71 (~45°).
        /// steerRef becomes 0.5*fwd+0.5*vel after lock — cone must sit under that.
        /// </summary>
        private const float MaxOffSteerDeg = 34f;
        private const float MinSteerSpeed = 5f;

        private static Vector3 _worldAimDir = Vector3.forward;
        private static bool _initialized;
        private static Vector2 _lastStableViewport = new Vector2(0.5f, 0.5f);
        private static Missile? _lastMissile;
        private static float _pendingYawDeg;
        private static float _pendingPitchDeg;
        private static Vector3 _lastAimLocal;
        private static bool _hasLastAim;
        private static readonly Vector3[] _viewCorners = new Vector3[4];

        internal static Vector2 GetReticleViewport() => _lastStableViewport;

        internal static Vector3 WorldAimDir => _worldAimDir;

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
            _worldAimDir = Vector3.forward;
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

        internal static void LateProject()
        {
            Missile? missile = _lastMissile;
            if (missile == null || missile.disabled)
                return;
            if (!RemoteControlSession.IsControlling(missile))
                return;

            Camera? feed = MissileCameraFsAccess.TryGetFeedCamera();
            Transform mt = missile.transform;

            Vector3 aimDir = _worldAimDir.sqrMagnitude > 1e-8f ? _worldAimDir.normalized : mt.forward;
            Vector3 projectPoint = mt.position + aimDir * ProjectDistance;

            if (feed != null)
            {
                Vector3 vp = Vector3.zero;
                bool projected = false;
                if (Input.GetMouseButton(1)
                    && MissileCameraFsAccess.TryWorldToBoreViewport(feed, projectPoint, out vp))
                {
                    projected = true;
                }
                else
                {
                    projectPoint = feed.transform.position + aimDir * ProjectDistance;
                    vp = feed.WorldToViewportPoint(projectPoint);
                    projected = vp.z > 0.05f
                        && !float.IsNaN(vp.x) && !float.IsNaN(vp.y)
                        && !float.IsInfinity(vp.x) && !float.IsInfinity(vp.y);
                }

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
            ConsumePendingAndEnsureInit(missile, mt);
            WriteAimpoint(missile, mt);
        }

        private static void ConsumePendingAndEnsureInit(Missile missile, Transform mt)
        {
            if (!_initialized)
            {
                Vector3 fwd = ResolveSteerRef(missile, mt);
                _worldAimDir = ClampPitch(fwd, fwd);
                _initialized = true;
            }

            float dt = Mathf.Max(Time.fixedDeltaTime, 1f / 120f);
            float maxStep = Mathf.Min(MaxAimStepDeg, MaxAimDegPerSec * dt);

            float yaw = Mathf.Clamp(_pendingYawDeg, -maxStep, maxStep);
            float pitch = Mathf.Clamp(_pendingPitchDeg, -maxStep, maxStep);
            _pendingYawDeg -= yaw;
            _pendingPitchDeg -= pitch;
            // Drop pathological backlog (Alt-Tab / hitch) instead of catching up with a whip.
            if (Mathf.Abs(_pendingYawDeg) > MaxAimStepDeg * 4f)
                _pendingYawDeg = Mathf.Clamp(_pendingYawDeg, -MaxAimStepDeg, MaxAimStepDeg);
            if (Mathf.Abs(_pendingPitchDeg) > MaxAimStepDeg * 4f)
                _pendingPitchDeg = Mathf.Clamp(_pendingPitchDeg, -MaxAimStepDeg, MaxAimStepDeg);

            Vector3 prev = _worldAimDir;
            if (yaw * yaw + pitch * pitch > 1e-10f)
                ApplyMouseToWorldAim(mt, yaw, pitch);

            if (_worldAimDir.sqrMagnitude > 1e-8f)
                _worldAimDir = _worldAimDir.normalized;
            else
                _worldAimDir = ResolveSteerRef(missile, mt);

            if (prev.sqrMagnitude > 1e-6f && Vector3.Angle(prev, _worldAimDir) > maxStep + 0.5f)
                _worldAimDir = Vector3.RotateTowards(prev.normalized, _worldAimDir, maxStep * Mathf.Deg2Rad, 0f);

            // Always pull into steer cone before write (sideslip-safe).
            _worldAimDir = ClampToSteerCone(_worldAimDir, ResolveSteerRef(missile, mt));
        }

        private static void WriteAimpoint(Missile missile, Transform mt)
        {
            float dist = Mathf.Max(200f, RcConfig.AimDistance.Value);
            Vector3 origin = mt.position;
            Vector3 steerRef = ResolveSteerRef(missile, mt);
            Vector3 dir = _worldAimDir.sqrMagnitude > 1e-8f ? _worldAimDir.normalized : steerRef;
            dir = ClampToSteerCone(dir, steerRef);
            _worldAimDir = dir;

            Vector3 aimLocal = RcBallisticImpactSafety.ResolveAimPoint(origin, dir, dist);
            Vector3 toAim = aimLocal - origin;
            if (toAim.sqrMagnitude < 1f || Vector3.Dot(toAim.normalized, steerRef) < 0.5f)
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

        /// <summary>Matches stock Steering reference after reachedOnTarget (fwd/vel blend).</summary>
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

        private static Vector3 ClampToSteerCone(Vector3 dir, Vector3 steerRef)
        {
            if (dir.sqrMagnitude < 1e-8f)
                return steerRef;
            dir.Normalize();
            if (steerRef.sqrMagnitude < 1e-8f)
                return dir;

            float ang = Vector3.Angle(steerRef, dir);
            if (ang <= MaxOffSteerDeg)
                return dir;

            return Vector3.RotateTowards(steerRef, dir, MaxOffSteerDeg * Mathf.Deg2Rad, 0f).normalized;
        }

        private static void ApplyMouseToWorldAim(Transform mt, float yawDeltaDeg, float pitchDeltaDeg)
        {
            Vector3 dir = _worldAimDir.sqrMagnitude > 1e-8f ? _worldAimDir.normalized : mt.forward.normalized;
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

            _worldAimDir = ClampPitch(dir.normalized, mt.forward);
        }

        private static Vector3 ClampPitch(Vector3 dir, Vector3 headingFallback)
        {
            dir.Normalize();
            if (dir.y <= MaxPitchSin && dir.y >= -MaxPitchSin)
                return dir;

            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-8f)
            {
                Vector3 prevFlat = new Vector3(_worldAimDir.x, 0f, _worldAimDir.z);
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
