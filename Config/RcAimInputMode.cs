using System;
using System.Globalization;

namespace MissileCameraRemoteControl.Config
{
    /// <summary>How the player steers world-space aim under RC.</summary>
    public enum RcAimInputMode
    {
        Mouse = 0,
        WASD = 1,
        Arrows = 2,
        NumPadArrows = 3,
        /// <summary>Player-defined AimYaw/Pitch keybinds in config.</summary>
        Custom = 4
    }

    /// <summary>Case-insensitive AimInputMode parse — invalid values fall back to Mouse with a log line.</summary>
    internal static class RcAimInputModeParser
    {
        internal static RcAimInputMode Parse(string? raw, RcAimInputMode fallback = RcAimInputMode.Mouse)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            string key = raw.Trim();
            if (Enum.TryParse(key, true, out RcAimInputMode direct))
                return direct;

            switch (key.ToUpperInvariant())
            {
                case "M":
                case "MOUSE":
                    return RcAimInputMode.Mouse;
                case "KEYBOARD":
                case "KEYS":
                case "WASD":
                    return RcAimInputMode.WASD;
                case "ARROW":
                case "ARROWS":
                case "ARROWKEYS":
                case "CURSOR":
                    return RcAimInputMode.Arrows;
                case "NUMPAD":
                case "KEYPAD":
                case "NUMPADARROWS":
                case "NUMPAD_ARROWS":
                    return RcAimInputMode.NumPadArrows;
                case "CUSTOM":
                    return RcAimInputMode.Custom;
                default:
                    if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                        && Enum.IsDefined(typeof(RcAimInputMode), n))
                    {
                        return (RcAimInputMode)n;
                    }

                    RcPlugin.ModLogger?.LogWarning(
                        $"AimInputMode '{raw}' invalid — use Mouse/WASD/Arrows/NumPadArrows/Custom. Using {fallback}.");
                    return fallback;
            }
        }
    }
}
