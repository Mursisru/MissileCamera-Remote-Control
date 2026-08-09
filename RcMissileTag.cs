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
        /// <summary>False for passive shells (76mm DLG) — no player T/FS remote stick.</summary>
        internal bool Controllable = true;
        /// <summary>Cached DL/SATCOM string for GetSeekerType (avoid enum→string every UI poll).</summary>
        internal string GuidanceLabel = string.Empty;
    }
}
