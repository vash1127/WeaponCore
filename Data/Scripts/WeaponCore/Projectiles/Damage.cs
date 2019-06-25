using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
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

        internal class ShieldEvent : IThreadHits
        {
            public readonly IMyTerminalBlock Shield;
            public readonly ShieldApi SApi;
            public readonly Vector3D HitPos;
            public readonly int Hits;
            public readonly Fired Fired;
            public ShieldEvent(IMyTerminalBlock shield, ShieldApi sApi, Vector3D hitPos, int hits, Fired fired)
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
                var baseDamage = wDef.AmmoDef.DefaultDamage;
                var damage = (baseDamage  + wDef.AmmoDef.AreaEffectYield) * Hits;
                SApi.PointAttackShield(Shield, HitPos, Fired.FiringCube.EntityId, damage, false, true);
            }
        }

        internal class GridEvent : IThreadHits
        {
            public readonly IMySlimBlock Block;
            public readonly Vector3D HitPos;
            public readonly int Hits;
            public readonly Fired Fired;
            public readonly MyStringHash TestDamage = MyDamageType.Bullet;
            public GridEvent(IMySlimBlock block, Vector3D hitPos, int hits, Fired fired)
            {
                Block = block;
                HitPos = hitPos;
                Hits = hits;
                Fired = fired;
            }

            public void Execute()
            {
                if (Block == null || Block.IsDestroyed || Block.CubeGrid.MarkedForClose) return;
                var wSystem = Fired.WeaponSystem;
                var wDef = wSystem.WeaponType;
                var baseDamage = wDef.AmmoDef.DefaultDamage;
                var damage = baseDamage * Hits;
                Block.DoDamage(damage, TestDamage, true, null, Fired.FiringCube.EntityId);
                if (wSystem.AmmoAreaEffect)
                    UtilsStatic.CreateMissileExplosion(HitPos, Fired.Direction,Fired.FiringCube, (MyCubeGrid)Block.CubeGrid, wDef.AmmoDef.AreaEffectRadius, wDef.AmmoDef.AreaEffectYield);
            }
        }

        internal class DestroyableEvent : IThreadHits
        {
            public readonly IMyDestroyableObject DestObj;
            public readonly MyStringHash TestDamage = MyStringHash.GetOrCompute("TestDamage");
            public readonly Fired Fired;

            internal DestroyableEvent(IMyDestroyableObject destObj, Fired fired)
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

        internal class ProximityEvent : IThreadHits
        {
            public readonly Vector3D ActivatePos;
            public readonly ShieldApi SApi;
            public readonly MyEntity Entity;
            public readonly MyStringHash TestDamage = MyStringHash.GetOrCompute("TestDamage");
            public readonly Fired Fired;

            public ProximityEvent(Fired fired, MyEntity entity, Vector3D activatePos, ShieldApi sApi)
            {
                Fired = fired;
                Entity = entity;
                ActivatePos = activatePos;
                SApi = sApi;
            }

            public void Execute()
            {
                var wepDef = Fired.WeaponSystem.WeaponType;
                if (Entity == null)
                {
                    UtilsStatic.CreateFakeExplosion(ActivatePos, wepDef.AmmoDef.AreaEffectRadius);
                    return;
                }
                var shield = Entity as IMyTerminalBlock;
                var grid = Entity as MyCubeGrid;
                var voxel = Entity as MyVoxelBase;
                var destroyable = Entity as IMyDestroyableObject;
            }
        }

        internal class VoxelEvent : IThreadHits
        {
            public void Execute()
            {

            }
        }
    }
}
