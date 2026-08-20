using UnityEngine;

namespace MissileCameraRemoteControl.Access
{
    /// <summary>Per-frame FS reflection cache — avoid PropertyInfo/Invoke spam in RC hot paths.</summary>
    internal static class RcFrameCache
    {
        private static int _frame = -1;
        private static bool _fsActive;
        private static bool _fsQueried;
        private static Camera? _feedCam;
        private static bool _feedQueried;
        private static RectTransform? _viewRt;
        private static bool _viewQueried;
        private static RectTransform? _hudRoot;
        private static bool _hudQueried;

        internal static void BeginFrame()
        {
            int f = Time.frameCount;
            if (f == _frame)
                return;
            _frame = f;
            _fsQueried = false;
            _feedQueried = false;
            _viewQueried = false;
            _hudQueried = false;
            _feedCam = null;
            _viewRt = null;
            _hudRoot = null;
        }

        // True either when the pilot is really in fullscreen, OR when the base mod's public Bridge
        // reports an active capture (McBaseBridgeAccess.IsCaptureActive) — which is only true
        // between an external consumer's RequestCapture(true)/RequestCapture(false) pair (e.g.
        // NOXMFD's browser MFD actually keeping the feed pipeline live headlessly), not merely
        // whenever some owned missile happens to be trackable. See Access/McBaseBridgeAccess.cs.
        // Real FS is checked first since it's already cached per-frame and cheaper than the
        // reflected call. Renamed from IsFullscreenActive — that name stopped matching once this
        // stopped being FS-only.
        internal static bool IsControlAllowed
        {
            get
            {
                BeginFrame();
                if (!_fsQueried)
                {
                    _fsActive = MissileCameraFsAccess.QueryFullscreenActiveRaw()
                        || McBaseBridgeAccess.IsCaptureActive;
                    _fsQueried = true;
                }

                return _fsActive;
            }
        }

        internal static Camera? FeedCamera
        {
            get
            {
                BeginFrame();
                if (!_feedQueried)
                {
                    _feedCam = MissileCameraFsAccess.QueryFeedCameraRaw();
                    _feedQueried = true;
                }

                return _feedCam;
            }
        }

        internal static RectTransform? FeedViewRect
        {
            get
            {
                BeginFrame();
                if (!_viewQueried)
                {
                    _viewRt = MissileCameraFsAccess.QueryFeedViewRectRaw();
                    _viewQueried = true;
                }

                return _viewRt;
            }
        }

        internal static RectTransform? HudOverlayRoot
        {
            get
            {
                BeginFrame();
                if (!_hudQueried)
                {
                    _hudRoot = MissileCameraFsAccess.QueryHudOverlayRootRaw();
                    _hudQueried = true;
                }

                return _hudRoot;
            }
        }
    }
}
