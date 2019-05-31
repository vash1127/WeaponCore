using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        internal interface IThreadHits
        {
            void Execute();
        }

        internal class TurretShieldEvent : IThreadHits
        {
            public readonly IMyTerminalBlock Shield;
            public readonly ShieldApi SApi;
            public readonly Vector3D HitPos;
            public readonly int Hits;
            public readonly Weapon Weapon;
            public TurretShieldEvent(IMyTerminalBlock shield, ShieldApi sApi, Vector3D hitPos, int hits, Weapon weapon)
            {
                Shield = shield;
                SApi = sApi;
                HitPos = hitPos;
                Hits = hits;
                Weapon = weapon;
            }

            public void Execute()
            {
                if (Shield == null || Weapon == null || SApi == null) return;
                var wDef = Weapon.WeaponType;
                var baseDamage = wDef.ComputedBaseDamage;
                var damage = (baseDamage  + wDef.AreaEffectYield) * Hits;
                SApi.PointAttackShield(Shield, HitPos, Weapon.Comp.MyCube.EntityId, damage, false, true);
            }
        }

        internal class TurretGridEvent : IThreadHits
        {
            public readonly IMySlimBlock Block;
            public readonly Vector3D HitPos;
            public readonly int Hits;
            public readonly Weapon Weapon;
            public readonly MyStringHash TestDamage = MyStringHash.GetOrCompute("TestDamage");
            public TurretGridEvent(IMySlimBlock block, Vector3D hitPos, int hits, Weapon weapon)
            {
                Block = block;
                HitPos = hitPos;
                Hits = hits;
                Weapon = weapon;
            }

            public void Execute()
            {
                if (Block == null || Block.IsDestroyed || Block.CubeGrid.MarkedForClose) return;
                var wDef = Weapon.WeaponType;
                var baseDamage = wDef.ComputedBaseDamage;
                var damage = baseDamage * Hits;
                Block.DoDamage(damage, TestDamage, true, null, Weapon.Comp.MyCube.EntityId);
                if (wDef.HasAreaEffect)
                    UtilsStatic.CreateExplosion(HitPos, wDef.AreaEffectRadius, wDef.AreaEffectYield);
            }
        }

        internal class TurretDestroyableEvent : IThreadHits
        {
            public readonly IMyDestroyableObject DestObj;
            public readonly MyStringHash TestDamage = MyStringHash.GetOrCompute("TestDamage");
            public readonly Weapon Weapon;

            internal TurretDestroyableEvent(IMyDestroyableObject destObj, Weapon weapon)
            {
                DestObj = destObj;
                Weapon = weapon;
            }

            public void Execute()
            {
                if (DestObj == null) return;
                var damage = 100;
                DestObj.DoDamage(damage, TestDamage, true, null, Weapon.Comp.MyCube.EntityId);
            }
        }

        internal class TurretVoxelEvent : IThreadHits
        {
            public void Execute()
            {

            }
        }
    }
}
