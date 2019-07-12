using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore
{
    public partial class Session
    {
        internal struct DamageEvent
        {
            internal enum Type
            {
                Shield,
                Grid,
                Voxel,
                Proximity,
                Destroyable,
            }

            internal readonly Type DamageType;
            internal readonly WeaponSystem System;
            internal readonly MyEntity HitEntity;
            internal readonly MyEntity Attacker;
            internal readonly IMySlimBlock Block;
            internal readonly Vector3D HitPos;
            internal readonly Vector3D Direction;
            internal readonly int Hits;

            internal DamageEvent(Type damageType, WeaponSystem system, Vector3D hitPos, Vector3D direction, int hits, MyEntity hitEntity, IMySlimBlock block, MyEntity attacker)
            {
                DamageType = damageType;
                System = system;
                HitPos = hitPos;
                Direction = direction;
                Hits = hits;
                HitEntity = hitEntity;
                Block = block;
                Attacker = attacker;
            }
        }

        internal void ProcessHits()
        {
            DamageEvent damageEvent;
            while (Projectiles.Hits.TryDequeue(out damageEvent))
            {
                switch (damageEvent.DamageType)
                {
                    case DamageEvent.Type.Shield:
                        DamageShield(ref damageEvent);
                        continue;
                    case DamageEvent.Type.Grid:
                        DamageGrid(ref damageEvent);
                        break;
                    case DamageEvent.Type.Destroyable:
                        DamageDestObj(ref damageEvent);
                        continue;
                    case DamageEvent.Type.Voxel:
                        DamageVoxel(ref damageEvent);
                        continue;
                    case DamageEvent.Type.Proximity:
                        DamageProximity(ref damageEvent);
                        continue;
                }
            }
        }

        private void DamageShield(ref DamageEvent dEvent)
        {
            var shield = dEvent.HitEntity as IMyTerminalBlock;
            var system = dEvent.System;
            if (shield == null) return;
            var baseDamage = system.Values.Ammo.DefaultDamage;
            var damage = (baseDamage + system.Values.Ammo.AreaEffectYield) * dEvent.Hits;
            SApi.PointAttackShield(shield, dEvent.HitPos, dEvent.Attacker.EntityId, damage, false, true);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce((MyEntity)shield.CubeGrid, dEvent.HitPos, dEvent.Direction, (system.Values.Ammo.Mass * speed) / dEvent.Hits);
            }
        }

        private void DamageGrid(ref DamageEvent dEvent)
        {
            var grid = dEvent.HitEntity as MyCubeGrid;
            var block = dEvent.Block;
            var system = dEvent.System;

            if (block == null || grid == null || block.IsDestroyed || grid.MarkedForClose) return;

            var baseDamage = system.Values.Ammo.DefaultDamage;
            var damage = baseDamage * dEvent.Hits;
            block.DoDamage(damage, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
            if (system.AmmoAreaEffect)
            {
                if (ExplosionReady) UtilsStatic.CreateMissileExplosion(dEvent.HitPos, dEvent.Direction, dEvent.Attacker, grid, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield);
                else UtilsStatic.CreateMissileExplosion(dEvent.HitPos, dEvent.Direction, dEvent.Attacker, grid, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield, true);
            }
            else if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce(grid, dEvent.HitPos, dEvent.Direction, (system.Values.Ammo.Mass * speed) / dEvent.Hits);
            }
        }

        private void DamageDestObj(ref DamageEvent dEvent)
        {
            var entity = dEvent.HitEntity;
            var destObj = dEvent.HitEntity as IMyDestroyableObject;
            var system = dEvent.System;
            if (destObj == null || entity == null) return;

            var baseDamage = system.Values.Ammo.DefaultDamage;
            var damage = baseDamage * dEvent.Hits;

            destObj.DoDamage(damage, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, dEvent.Direction, (system.Values.Ammo.Mass * speed));
            }
        }

        private void DamageVoxel(ref DamageEvent dEvent)
        {
            var entity = dEvent.HitEntity;
            var destObj = dEvent.HitEntity as MyVoxelBase;
            var system = dEvent.System;
            if (destObj == null || entity == null) return;

            var baseDamage = system.Values.Ammo.DefaultDamage;
            var damage = baseDamage * dEvent.Hits;

            //destObj.DoDamage(damage, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, dEvent.Direction, (system.Values.Ammo.Mass * speed));
            }
        }

        private void DamageProximity(ref DamageEvent dEvent)
        {
            var system = dEvent.System;
            if (dEvent.HitEntity != null)
            {
                if (ExplosionReady) UtilsStatic.CreateMissileExplosion(dEvent.HitPos, dEvent.Direction, dEvent.Attacker, null, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield);
                else UtilsStatic.CreateMissileExplosion(dEvent.HitPos, dEvent.Direction, dEvent.Attacker, null, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield, true);
            }
            else UtilsStatic.CreateFakeExplosion(dEvent.HitPos, system.Values.Ammo.AreaEffectRadius);
        }

        public static void ApplyProjectileForce(MyEntity entity, Vector3D intersectionPosition, Vector3 normalizedDirection, float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic || entity.Physics.Mass / impulse > 500)
                return;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero);
        }
    }
}
