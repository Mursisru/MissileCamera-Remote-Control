using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Network;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>Reticle center → Unit under cursor → SetTarget.</summary>
    internal static class RetargetController
    {
        private const float RayDistance = 80000f;

        internal static void Tick(Missile missile)
        {
            if (missile == null || !KeybindPoll.IsDown(RcConfig.Retarget.Value))
                return;

            Camera? cam = Camera.main;
            if (cam == null)
                return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, RayDistance))
                return;

            Unit? unit = hit.collider != null
                ? hit.collider.GetComponentInParent<Unit>()
                : null;

            if (unit == null || unit.disabled || ReferenceEquals(unit, missile))
                return;

            // Do not lock friendlies as strike targets.
            try
            {
                if (AuthorityGate.TryGetLocalHq(out FactionHQ? hq)
                    && hq != null
                    && unit.NetworkHQ == hq)
                    return;
            }
            catch
            {
                // ignore
            }

            try
            {
                missile.SetTarget(unit);
                RcPlugin.ModLogger?.LogInfo($"RC retarget → {unit.unitName}");
            }
            catch
            {
                // ignore
            }
        }
    }
}
