using System.Collections.Generic;
using BepInEx.Configuration;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Control;
using MissileCameraRemoteControl.HarmonyPatches;
using UnityEngine;

namespace MissileCameraRemoteControl.Config
{
    /// <summary>Unity Input poll — never KeyboardShortcut.IsDown (Rewired breaks BepInEx UnityInput).</summary>
    internal static class KeybindPoll
    {
        internal static bool IsDown(KeyboardShortcut shortcut)
        {
            KeyCode main = shortcut.MainKey;
            if (main == KeyCode.None)
                return false;
            if (!ReadKeyDown(main))
                return false;
            return ModifiersHeld(shortcut);
        }

        internal static bool IsHeld(KeyboardShortcut shortcut)
        {
            KeyCode main = shortcut.MainKey;
            if (main == KeyCode.None)
                return false;
            if (!ReadKeyHeld(main))
                return false;
            return ModifiersHeld(shortcut);
        }

        private static bool ReadKeyDown(KeyCode key)
        {
            if (ShouldPassthroughEat(key))
            {
                if (RcSpaceKeyEatPatch.RawKeyDown(key))
                    return true;
            }
            else if (Input.GetKeyDown(key))
            {
                return true;
            }

            return RcRewiredInput.IsKeyDown(key);
        }

        private static bool ReadKeyHeld(KeyCode key)
        {
            if (ShouldPassthroughEat(key))
            {
                if (RcSpaceKeyEatPatch.RawKey(key))
                    return true;
            }
            else if (Input.GetKey(key))
            {
                return true;
            }

            return RcRewiredInput.IsKeyHeld(key);
        }

        /// <summary>FS+RC: Space/manual bind is Harmony-eaten — poll via passthrough.</summary>
        private static bool ShouldPassthroughEat(KeyCode key)
        {
            if (!MissileCameraFsAccess.IsControlAllowed || !RemoteControlSession.IsActive)
                return false;

            KeyCode manual = RcConfig.ManualDetonate.Value.MainKey;
            if (manual == KeyCode.None)
                manual = KeyCode.Space;
            return key == manual || key == KeyCode.Space;
        }

        private static bool ModifiersHeld(KeyboardShortcut shortcut)
        {
            IEnumerable<KeyCode> mods = shortcut.Modifiers;
            if (mods is KeyCode[] arr)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    KeyCode mod = arr[i];
                    if (mod != KeyCode.None && !ReadKeyHeld(mod))
                        return false;
                }

                return true;
            }

            if (mods is IList<KeyCode> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    KeyCode mod = list[i];
                    if (mod != KeyCode.None && !ReadKeyHeld(mod))
                        return false;
                }

                return true;
            }

            foreach (KeyCode mod in mods)
            {
                if (mod == KeyCode.None)
                    continue;
                if (!ReadKeyHeld(mod))
                    return false;
            }

            return true;
        }
    }
}
