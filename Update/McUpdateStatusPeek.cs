using System;
using System.Reflection;

namespace MissileCameraRemoteControl.Update
{
    /// <summary>
    /// Soft-read MissileCamera update checker so RC UI can delay / annotate without fighting MC.
    /// </summary>
    internal static class McUpdateStatusPeek
    {
        private static bool _resolved;
        private static PropertyInfo? _isCompleted;
        private static PropertyInfo? _isOutdated;
        private static PropertyInfo? _latestTag;
        private static FieldInfo? _mcDisplayVersion;

        internal static bool TryGet(out bool completed, out bool outdated, out string latestTag, out string installed)
        {
            completed = true;
            outdated = false;
            latestTag = string.Empty;
            installed = string.Empty;
            EnsureResolved();
            if (_isCompleted == null || _isOutdated == null)
                return false;

            try
            {
                completed = (bool)_isCompleted.GetValue(null);
                outdated = (bool)_isOutdated.GetValue(null);
                if (_latestTag != null)
                    latestTag = _latestTag.GetValue(null) as string ?? string.Empty;
                if (_mcDisplayVersion != null)
                    installed = _mcDisplayVersion.GetValue(null) as string ?? string.Empty;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureResolved()
        {
            if (_resolved)
                return;
            _resolved = true;

            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Assembly a = assemblies[i];
                    if (a == null)
                        continue;
                    string name = a.GetName().Name ?? string.Empty;
                    if (name.IndexOf("MissileCamera", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (name.IndexOf("RemoteControl", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    Type? checker = a.GetType("MissileCamera.MissileCameraUpdateChecker", throwOnError: false);
                    Type? appVer = a.GetType("MissileCamera.AppVersion", throwOnError: false);
                    if (checker != null)
                    {
                        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                        _isCompleted = checker.GetProperty("IsCompleted", flags);
                        _isOutdated = checker.GetProperty("IsOutdated", flags);
                        _latestTag = checker.GetProperty("LatestTag", flags);
                    }

                    if (appVer != null)
                    {
                        _mcDisplayVersion = appVer.GetField(
                            "DisplayVersion",
                            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    }

                    if (_isCompleted != null)
                        return;
                }
            }
            catch
            {
                // MC not loaded / renamed — ignore.
            }
        }
    }
}
