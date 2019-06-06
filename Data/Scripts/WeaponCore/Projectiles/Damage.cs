using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageMath;
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
            public readonly Fired Fired;
            public TurretShieldEvent(IMyTerminalBlock shield, ShieldApi sApi, Vector3D hitPos, int hits, Fired fired)
            {
                Shield = shield;
                SApi = sApi;
                HitPos = hitPos;
                Hits = hits;
                Fired = fired;
            }

            public void Execute()
            {
                if (Shield == null  || SApi == null) return;
                var wDef = Fired.WeaponSystem.WeaponType;
                var baseDamage = wDef.ComputedBaseDamage;
                var damage = (baseDamage  + wDef.AmmoDef.AreaEffectYield) * Hits;
                SApi.PointAttackShield(Shield, HitPos, Fired.FiringCube.EntityId, damage, false, true);
            }
        }

        internal class TurretGridEvent : IThreadHits
        {
            public readonly IMySlimBlock Block;
            public readonly Vector3D HitPos;
            public readonly int Hits;
            public readonly Fired Fired;
            public readonly MyStringHash TestDamage = MyStringHash.GetOrCompute("TestDamage");
            public TurretGridEvent(IMySlimBlock block, Vector3D hitPos, int hits, Fired fired)
            {
                Block = block;
                HitPos = hitPos;
                Hits = hits;
                Fired = fired;
            }

            public void Execute()
            {
                if (Block == null || Block.IsDestroyed || Block.CubeGrid.MarkedForClose) return;
                var wDef = Fired.WeaponSystem.WeaponType;
                var baseDamage = wDef.ComputedBaseDamage;
                var damage = baseDamage * Hits;
                Block.DoDamage(damage, TestDamage, true, null, Fired.FiringCube.EntityId);
                if (wDef.HasAreaEffect)
                    UtilsStatic.CreateExplosion(HitPos, wDef.AmmoDef.AreaEffectRadius, wDef.AmmoDef.AreaEffectYield);
            }
        }

        internal class TurretDestroyableEvent : IThreadHits
        {
            public readonly IMyDestroyableObject DestObj;
            public readonly MyStringHash TestDamage = MyStringHash.GetOrCompute("TestDamage");
            public readonly Fired Fired;

            internal TurretDestroyableEvent(IMyDestroyableObject destObj, Fired fired)
            {
                DestObj = destObj;
                Fired = fired;
            }

            public void Execute()
            {
                if (DestObj == null) return;
                var damage = 100;
                DestObj.DoDamage(damage, TestDamage, true, null, Fired.FiringCube.EntityId);
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
