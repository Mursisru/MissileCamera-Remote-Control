using Mirage;

namespace MissileCameraRemoteControl.Network
{
    /// <summary>Client → server: “do you run MissileCamera Remote Control?”</summary>
    [NetworkMessage]
    public struct RcPresenceQueryMsg
    {
        public int Magic;
    }

    /// <summary>Server → client: affirmative presence reply.</summary>
    [NetworkMessage]
    public struct RcPresenceReplyMsg
    {
        public int Magic;
    }
}
