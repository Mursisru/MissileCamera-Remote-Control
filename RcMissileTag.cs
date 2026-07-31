using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl
{
    /// <summary>Marks a flying missile prefab / instance as Remote-Control capable.</summary>
    internal sealed class RcMissileTag : MonoBehaviour
    {
        internal RcGuidanceKind Guidance;
        internal RcEngineKind Engine;
        internal string BackupSeekerType = string.Empty;
        internal string SourceMountKey = string.Empty;
    }
}
