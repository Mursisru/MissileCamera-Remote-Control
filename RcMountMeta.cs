using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl
{
    /// <summary>Stored on cloned mount prefab root — copied onto flying missile at launch.</summary>
    internal sealed class RcMountMeta : MonoBehaviour
    {
        internal RcGuidanceKind Guidance;
        internal RcEngineKind Engine;
        internal string SourceMountKey = string.Empty;
        internal string BackupSeekerHint = string.Empty;
    }
}
