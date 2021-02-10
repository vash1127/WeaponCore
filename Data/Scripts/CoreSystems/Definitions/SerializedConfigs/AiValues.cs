using System.Collections.Generic;
using ProtoBuf;

namespace CoreSystems
{
    [ProtoContract]
    public class AiDataValues
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public int Version = Session.VersionControl;
        [ProtoMember(3)] public long ActiveTerminal;
        [ProtoMember(4)] public readonly Dictionary<long, long> ControllingPlayers = new Dictionary<long, long>();

        public bool Sync(AiDataValues sync)
        {
            if (sync.Revision > Revision)
            {
                Revision = sync.Revision;
                ActiveTerminal = sync.ActiveTerminal;
                ControllingPlayers.Clear();
                foreach (var s in sync.ControllingPlayers)
                    ControllingPlayers[s.Key] = s.Value;

                return true;
            }

            return false;
        }
    }

}
