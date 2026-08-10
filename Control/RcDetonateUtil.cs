using System.Reflection;
using MissileCameraRemoteControl.HarmonyPatches;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Force Detonate through RcDetonateGate.
    /// Also invokes local RpcDetonate UserCode same frame — Mirage ClientRpc can lag visuals/blast.
    /// </summary>
    internal static class RcDetonateUtil
    {
        private static readonly MethodInfo? UserCodeRpcDetonate =
            typeof(Missile).GetMethod(
                "UserCode_RpcDetonate_897349600",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo? DelayedDestroyMethod =
            typeof(Missile).GetMethod(
                "DelayedDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Force(Missile? missile, Vector3? normal = null, bool hitTerrain = false)
        {
            if (missile == null || missile.disabled)
                return;

            Vector3 n = normal ?? (missile.rb != null ? missile.rb.velocity : Vector3.up);
            if (n.sqrMagnitude < 1e-6f)
                n = Vector3.up;

            bool armed = false;
            try
            {
                if (!missile.IsArmed())
                    missile.Arm();
                armed = missile.IsArmed();
            }
            catch
            {
                armed = true;
            }

            Vector3 pos;
            try
            {
                pos = missile.GlobalPosition().AsVector3();
            }
            catch
            {
                pos = missile.transform.position;
            }

            RcDetonateGate.AllowManual(missile);
            RcDetonateGate.AllowBegin();
            try
            {
                try
                {
                    missile.Detonate(n, hitArmor: false, hitTerrain: hitTerrain);
                }
                catch
                {
                    // ignore — still try local UserCode below
                }

                // Same-frame local explosion (stock ClientRpc may run later).
                InvokeLocalRpcDetonate(missile, pos, armed, hitTerrain, n);
            }
            finally
            {
                RcDetonateGate.AllowEnd();
                RcDetonateGate.ClearManual();
            }
        }

        private static void InvokeLocalRpcDetonate(
            Missile missile,
            Vector3 pos,
            bool armed,
            bool hitTerrain,
            Vector3 normal)
        {
            if (UserCodeRpcDetonate == null)
                return;

            try
            {
                UserCodeRpcDetonate.Invoke(
                    missile,
                    new object?[]
                    {
                        null,
                        false,
                        pos,
                        armed,
                        false,
                        hitTerrain,
                        normal
                    });
            }
            catch
            {
                // ignore
            }

            // If Detonate was gated out, still tear down the unit.
            try
            {
                if (!missile.disabled)
                {
                    missile.Networkdisabled = true;
                    DelayedDestroyMethod?.Invoke(missile, new object[] { 2f });
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
