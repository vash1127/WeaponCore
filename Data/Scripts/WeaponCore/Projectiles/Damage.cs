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
                var damage = 100 * Hits;
                SApi.PointAttackShield(Shield, HitPos, Weapon.Logic.Turret.EntityId, damage, true, false);
            }
        }

        internal class TurretGridEvent : IThreadHits
        {
            public readonly IMySlimBlock Block;
            public readonly int Hits;
            public readonly Weapon Weapon;
            public readonly MyStringHash TestDamage = MyStringHash.GetOrCompute("TestDamage");
            public TurretGridEvent(IMySlimBlock block, int hits, Weapon weapon)
            {
                Block = block;
                Hits = hits;
                Weapon = weapon;
            }

            public void Execute()
            {
                if (Block == null || Block.IsDestroyed || Block.CubeGrid.MarkedForClose) return;
                var damage = 1 * Hits;
                Block.DoDamage(damage, TestDamage, true, null, Weapon.Logic.Turret.EntityId);
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
                DestObj.DoDamage(damage, TestDamage, true, null, Weapon.Logic.Turret.EntityId);
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
