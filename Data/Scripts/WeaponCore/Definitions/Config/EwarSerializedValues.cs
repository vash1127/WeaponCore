using System.Collections.Generic;
using ProtoBuf;
namespace WeaponCore
{
    [ProtoContract]
    public class EwarData
    {
        [ProtoMember(1)] public List<long> EwaredBlocks = new List<long>();

        public void Sync(Session session, EwarData data)
        {
            session.CurrentClientEwaredCubes.Clear();
            foreach (var ids in data.EwaredBlocks)
                session.CurrentClientEwaredCubes.Add(ids);
        }
    }
}
