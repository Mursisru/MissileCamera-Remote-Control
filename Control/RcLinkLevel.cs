using MissileCameraRemoteControl.Config;

namespace MissileCameraRemoteControl.Control
{
    internal enum RcLinkLevel : byte
    {
        Full = 0,
        Degraded = 1,
        Lost = 2
    }
}
