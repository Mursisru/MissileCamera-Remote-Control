using System.Collections.Generic;
using BepInEx.Configuration;
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
            if (!Input.GetKeyDown(main))
                return false;
            return ModifiersHeld(shortcut);
        }

        internal static bool IsHeld(KeyboardShortcut shortcut)
        {
            KeyCode main = shortcut.MainKey;
            if (main == KeyCode.None)
                return false;
            if (!Input.GetKey(main))
                return false;
            return ModifiersHeld(shortcut);
        }

        private static bool ModifiersHeld(KeyboardShortcut shortcut)
        {
            IEnumerable<KeyCode> mods = shortcut.Modifiers;
            if (mods is KeyCode[] arr)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    KeyCode mod = arr[i];
                    if (mod != KeyCode.None && !Input.GetKey(mod))
                        return false;
                }

                return true;
            }

            if (mods is IList<KeyCode> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    KeyCode mod = list[i];
                    if (mod != KeyCode.None && !Input.GetKey(mod))
                        return false;
                }

                return true;
            }

            foreach (KeyCode mod in mods)
            {
                if (mod == KeyCode.None)
                    continue;
                if (!Input.GetKey(mod))
                    return false;
            }

            return true;
        }
    }
}
