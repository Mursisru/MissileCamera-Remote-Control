using BepInEx;
using BepInEx.Logging;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Control;
using MissileCameraRemoteControl.Update;

namespace MissileCameraRemoteControl
{
    [BepInPlugin(PluginGuid, PluginName, AppVersion.BepInSemVer)]
    // Soft: allow load without MC so we can offer an install prompt instead of a hard BepInEx fail.
    [BepInDependency(MissileCameraGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class RcPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.at747.missilecamera.remotecontrol";
        public const string PluginName = "MissileCamera Remote Control";
        public const string MissileCameraGuid = "com.at747.missilecamera.bepinex";

        internal static ManualLogSource? ModLogger { get; private set; }

        private void Awake()
        {
            ModLogger = Logger;
            RcConfig.Bind(Config);

            if (!RcMcDependency.IsMissileCameraPresent())
            {
                ModLogger.LogWarning(
                    $"{PluginName}: Missile Camera ({MissileCameraGuid}) not found — RC disabled until it is installed.");
                RcMcMissingPrompt.EnsureStandaloneHost();
                return;
            }

            RcHost.Ensure(ModLogger);
            ModLogger.LogInfo($"{PluginName} {AppVersion.DisplayVersion} loaded.");
        }
    }
}
