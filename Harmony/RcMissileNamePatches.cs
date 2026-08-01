using System;
using HarmonyLib;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Control;
using Mirage;
using UnityEngine;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// Spawner always sets NetworkunitName from shared vanilla prefab definition.
    /// Rename to RC display name immediately before Mirage Spawn so initial SyncVar + PersistentUnit stick.
    /// Mirage param is named <c>obj</c> (not prefab) — wrong names abort PatchAll and kill Motor.Thrust.
    /// </summary>
    internal static class RcMissileNamePatches
    {
        [HarmonyPatch(typeof(ServerObjectManagerExtensions), nameof(ServerObjectManagerExtensions.Spawn),
            new Type[] { typeof(ServerObjectManager), typeof(GameObject), typeof(INetworkPlayer) })]
        private static class ServerSpawnGoPlayerPatch
        {
            private static void Prefix(GameObject obj)
            {
                LaunchRcCapture.TryRenameBeforeNetworkSpawn(obj);
            }
        }

        [HarmonyPatch(typeof(ServerObjectManagerExtensions), nameof(ServerObjectManagerExtensions.Spawn),
            new Type[] { typeof(ServerObjectManager), typeof(GameObject), typeof(int), typeof(INetworkPlayer) })]
        private static class ServerSpawnGoOwnerPatch
        {
            private static void Prefix(GameObject obj)
            {
                LaunchRcCapture.TryRenameBeforeNetworkSpawn(obj);
            }
        }

        [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile), new Type[]
        {
            typeof(MissileDefinition), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Unit)
        })]
        private static class SpawnerSpawnMissileDefPatch
        {
            private static void Prefix(MissileDefinition missile)
            {
                try
                {
                    if (missile != null)
                        LaunchRcCapture.PushForcedDisplayName(missile.unitName);
                }
                catch
                {
                    // ignore
                }
            }

            private static void Postfix(Missile __result)
            {
                try
                {
                    LaunchRcCapture.TryApplyToSpawned(__result);
                }
                catch
                {
                    // ignore
                }
            }
        }

        [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissileEncyclopedia))]
        private static class SpawnerSpawnMissileEncPatch
        {
            private static void Prefix(MissileDefinition missile)
            {
                try
                {
                    if (missile != null)
                        LaunchRcCapture.PushForcedDisplayName(missile.unitName);
                }
                catch
                {
                    // ignore
                }
            }

            private static void Postfix(Missile __result)
            {
                try
                {
                    LaunchRcCapture.TryApplyToSpawned(__result);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
