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
        /// <summary>False only if stamped Controllable=false (legacy); 76mm DLG is controllable.</summary>
        internal bool Controllable = true;
        /// <summary>True when stamped from our RcMountMeta / clone key — not third-party munitions.</summary>
        internal bool OfficialClone;
        /// <summary>Cached DL/SATCOM string for GetSeekerType (avoid enum→string every UI poll).</summary>
        internal string GuidanceLabel = string.Empty;
    }
}
