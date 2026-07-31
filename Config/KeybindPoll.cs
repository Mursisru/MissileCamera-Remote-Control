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
            foreach (KeyCode mod in shortcut.Modifiers)
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
