using System.Collections.Generic;
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
using static WeaponCore.Projectiles.Projectiles.HitEntity;
namespace WeaponCore
{
    public partial class Session
    {
        internal struct DamageEvent
        {
            internal readonly WeaponSystem System;
            internal readonly List<HitEntity> HitEntity;
            internal readonly MyEntity Attacker;
            internal readonly Vector3D Direction;
            internal readonly int PoolId;

            internal DamageEvent(WeaponSystem system, Vector3D direction, List<HitEntity> hitEntity, MyEntity attacker, int poolId)
            {
                System = system;
                Direction = direction;
                HitEntity = hitEntity;
                Attacker = attacker;
                PoolId = poolId;
            }
        }

        internal void ProcessHits()
        {
            DamageEvent damageEvent;
            while (Projectiles.Hits.TryDequeue(out damageEvent))
            {
                var pId = damageEvent.PoolId;
                var damagePool = damageEvent.System.Values.Ammo.DefaultDamage;
                foreach (var hitEnt in damageEvent.HitEntity)
                {
                    if (damagePool <= 0) continue;
                    switch (hitEnt.EventType)
                    {
                        case Type.Shield:
                            DamageShield(hitEnt, ref damageEvent, ref damagePool);
                            continue;
                        case Type.Grid:
                            DamageGrid(hitEnt, ref damageEvent, ref damagePool);
                            break;
                        case Type.Destroyable:
                            DamageDestObj(hitEnt, ref damageEvent, ref damagePool);
                            continue;
                        case Type.Voxel:
                            DamageVoxel(hitEnt, ref damageEvent, ref damagePool);
                            continue;
                        case Type.Proximity:
                            DamageProximity(hitEnt, ref damageEvent, ref damagePool);
                            continue;
                    }
                    Projectiles.HitEntityPool[pId].Return(hitEnt);
                }
                damageEvent.HitEntity.Clear();
                Projectiles.HitsPool[pId].Return(damageEvent.HitEntity);
            }
        }

        private void DamageShield(HitEntity hitEnt, ref DamageEvent dEvent, ref float damagePool)
        {
            var shield = hitEnt.Entity as IMyTerminalBlock;
            var system = dEvent.System;
            if (shield == null || !hitEnt.HitPos.HasValue) return;
            damagePool = 0;
            SApi.PointAttackShield(shield, hitEnt.HitPos.Value, dEvent.Attacker.EntityId, damagePool, false, true);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce((MyEntity)shield.CubeGrid, hitEnt.HitPos.Value, dEvent.Direction, system.Values.Ammo.Mass * speed);
            }
        }

        private void DamageGrid(HitEntity hitEnt, ref DamageEvent dEvent, ref float damagePool)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            var system = dEvent.System;

            if (grid == null || grid.MarkedForClose || !hitEnt.HitPos.HasValue || hitEnt.Blocks == null) return;

            for (int i = 0; i < hitEnt.Blocks.Count; i++)
            {
                var block = hitEnt.Blocks[i];
                var blockHp = block.Integrity;
                var damage = blockHp;
                if (damagePool < blockHp)
                {
                    damage = damagePool;
                    damagePool = 0;
                }
                else damagePool -= damage;

                if (damagePool <= 0) continue;
                block.DoDamage(damage, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
                if (system.AmmoAreaEffect)
                {
                    if (ExplosionReady) UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, dEvent.Direction, dEvent.Attacker, grid, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield);
                    else UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, dEvent.Direction, dEvent.Attacker, grid, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield, true);
                }
                else if (system.Values.Ammo.Mass > 0)
                {
                    var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                    ApplyProjectileForce(grid, hitEnt.HitPos.Value, dEvent.Direction, (system.Values.Ammo.Mass * speed));
                }
            }
        }

        private void DamageDestObj(HitEntity hitEnt, ref DamageEvent dEvent, ref float damagePool)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as IMyDestroyableObject;
            var system = dEvent.System;
            if (destObj == null || entity == null) return;

            var objHp = destObj.Integrity;
            if (damagePool < objHp)
            {
                objHp = damagePool;
                damagePool = 0;
            }
            else damagePool -= objHp;

            destObj.DoDamage(objHp, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, dEvent.Direction, (system.Values.Ammo.Mass * speed));
            }
        }

        private void DamageVoxel(HitEntity hitEnt, ref DamageEvent dEvent, ref float damagePool)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as MyVoxelBase;
            var system = dEvent.System;
            if (destObj == null || entity == null) return;

            var baseDamage = system.Values.Ammo.DefaultDamage;
            var damage = baseDamage;

            //destObj.DoDamage(damage, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, dEvent.Direction, (system.Values.Ammo.Mass * speed));
            }
        }

        private void DamageProximity(HitEntity hitEnt, ref DamageEvent dEvent, ref float damagePool)
        {
            var system = dEvent.System;
            if (hitEnt.Hit)
            {
                if (ExplosionReady) UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, dEvent.Direction, dEvent.Attacker, null, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield);
                else UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, dEvent.Direction, dEvent.Attacker, null, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield, true);
            }
            else if (hitEnt.HitPos.HasValue) UtilsStatic.CreateFakeExplosion(hitEnt.HitPos.Value, system.Values.Ammo.AreaEffectRadius);
        }

        public static void ApplyProjectileForce(MyEntity entity, Vector3D intersectionPosition, Vector3 normalizedDirection, float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic || entity.Physics.Mass / impulse > 500)
                return;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero);
        }
    }
}
