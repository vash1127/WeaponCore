using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
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
                var kind = Fired.WeaponSystem.Kind;
                var baseDamage = kind.Ammo.DefaultDamage;
                var damage = (baseDamage + kind.Ammo.AreaEffectYield) * Hits;
                SApi.PointAttackShield(Shield, HitPos, Fired.FiringCube.EntityId, damage, false, true);
                if (kind.Ammo.Mass > 0) ApplyProjectileForce((MyEntity)Shield.CubeGrid, HitPos, Fired.Direction, (kind.Ammo.Mass * kind.Ammo.Trajectory.DesiredSpeed) / Hits);
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
                var kind = wSystem.Kind;
                var baseDamage = kind.Ammo.DefaultDamage;
                var damage = baseDamage * Hits;
                Block.DoDamage(damage, TestDamage, true, null, Fired.FiringCube.EntityId);
                if (wSystem.AmmoAreaEffect)
                    UtilsStatic.CreateMissileExplosion(HitPos, Fired.Direction,Fired.FiringCube, (MyCubeGrid)Block.CubeGrid, kind.Ammo.AreaEffectRadius, kind.Ammo.AreaEffectYield);
                else if (kind.Ammo.Mass > 0) ApplyProjectileForce((MyEntity)Block.CubeGrid, HitPos, Fired.Direction, (kind.Ammo.Mass * kind.Ammo.Trajectory.DesiredSpeed) / Hits);
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
                var wSystem = Fired.WeaponSystem;
                var kind = wSystem.Kind;
                DestObj.DoDamage(damage, TestDamage, true, null, Fired.FiringCube.EntityId);
                if (kind.Ammo.Mass > 0)
                {
                    var entity = (MyEntity)DestObj;
                    ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, Fired.Direction, (kind.Ammo.Mass * kind.Ammo.Trajectory.DesiredSpeed));
                }
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
                var kind = Fired.WeaponSystem.Kind;
                if (Entity == null)
                {
                    UtilsStatic.CreateFakeExplosion(ActivatePos, kind.Ammo.AreaEffectRadius);
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

        public static void ApplyProjectileForce(MyEntity entity, Vector3D intersectionPosition, Vector3 normalizedDirection, float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic || entity.Physics.Mass / impulse > 500)
                return;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero);
        }
    }
}
