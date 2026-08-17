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

        // Widened beyond its name (kept for the many call sites that read it as "is RC allowed to
        // control right now"): true either when the pilot is really in fullscreen, OR when the
        // base mod's public Bridge reports a trackable missile — which is what's true whenever an
        // external consumer (e.g. NOXMFD's browser MFD) is keeping the feed pipeline live headlessly
        // via McBridge.RequestCapture. See Access/McBaseBridgeAccess.cs. Real FS is checked first
        // since it's already cached per-frame and cheaper than the reflected call.
        internal static bool IsFullscreenActive
        {
            get
            {
                BeginFrame();
                if (!_fsQueried)
                {
                    _fsActive = MissileCameraFsAccess.QueryFullscreenActiveRaw()
                        || McBaseBridgeAccess.HasTrackableMissile;
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
