namespace MissileCameraRemoteControl.Config
{
    /// <summary>How the player steers world-space aim under RC.</summary>
    internal enum RcAimInputMode
    {
        Mouse = 0,
        WASD = 1,
        Arrows = 2,
        NumPadArrows = 3,
        /// <summary>Player-defined AimYaw/Pitch keybinds in config.</summary>
        Custom = 4
    }
}
