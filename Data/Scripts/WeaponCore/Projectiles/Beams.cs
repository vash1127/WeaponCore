using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        internal readonly List<FiredBeam> FiredBeams = new List<FiredBeam>();

        internal struct FiredBeam
        {
            public readonly List<LineD> Beams;
            public readonly Weapon Weapon;

            public FiredBeam(Weapon weapon, List<LineD> beams)
            {
                Weapon = weapon;
                Beams = beams;
            }
        }

        internal void RunBeams()
        {
            lock (FiredBeams)
            {
                MyAPIGateway.Parallel.ForEach(FiredBeams, fired =>
                {
                    for (int i = 0; i < fired.Beams.Count; i++)
                    {
                        var beam = fired.Beams[i];
                        var checkEnts = CheckPool.Get();

                        GetAllEntitiesInLine(checkEnts, fired, beam);
                        var hitInfo = GetHitEntities(checkEnts, beam);
                        GetDamageInfo(fired, beam, hitInfo, i, true);

                        checkEnts.Clear();
                        CheckPool.Return(checkEnts);
                    }
                    DamageEntities(fired);
                });
                FiredBeams.Clear();
            }
        }
    }
}
