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

        internal static bool IsFullscreenActive
        {
            get
            {
                BeginFrame();
                if (!_fsQueried)
                {
                    _fsActive = MissileCameraFsAccess.QueryFullscreenActiveRaw();
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
