using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        internal readonly List<FiredBeam> FiredBeams = new List<FiredBeam>();
        internal readonly MyConcurrentPool<List<MyEntity>> CheckPoolBeam = new MyConcurrentPool<List<MyEntity>>();

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
                        List<MyEntity> checkEnts = CheckPoolBeam.Get();

                        GetAllEntitiesInLine(checkEnts, fired, beam);
                        var hitInfo = GetHitEntities(checkEnts, fired, beam);
                        GetDamageInfo(fired, beam, hitInfo, i, true);

                        checkEnts.Clear();
                        CheckPoolBeam.Return(checkEnts);
                    }
                    DamageEntities(fired);
                });
                FiredBeams.Clear();
            }
        }
    }
}
