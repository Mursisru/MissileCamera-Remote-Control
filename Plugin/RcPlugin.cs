using BepInEx;
using BepInEx.Logging;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl
{
    [BepInPlugin(PluginGuid, PluginName, AppVersion.BepInSemVer)]
    [BepInDependency(MissileCameraGuid, BepInDependency.DependencyFlags.HardDependency)]
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
            RcHost.Ensure(ModLogger);
            ModLogger.LogInfo($"{PluginName} {AppVersion.DisplayVersion} loaded.");
        }
    }
}
