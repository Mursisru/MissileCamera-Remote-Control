using Mirage;
using NuclearOption.Networking;

namespace MissileCameraRemoteControl.Network
{
    /// <summary>Host → clients afterburner VFX flag (no Mirage weaver NetworkBehaviour).</summary>
    [NetworkMessage]
    public struct RcBoostNetMsg
    {
        public uint NetId;
        public bool Boost;
    }
}
