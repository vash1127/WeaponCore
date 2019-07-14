using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles;
using static WeaponCore.Projectiles.Projectiles.HitEntity;
namespace WeaponCore
{
    public partial class Session
    {
        internal struct DamageEvent
        {
            internal readonly Projectile Projectile;
            internal readonly List<HitEntity> HitEntity;
            internal readonly MyEntity Attacker;
            internal readonly Vector3D Direction;

            internal DamageEvent(Projectile projectile, Vector3D direction, List<HitEntity> hitEntity, MyEntity attacker)
            {
                Projectile = projectile;
                Direction = direction;
                HitEntity = hitEntity;
                Attacker = attacker;
            }
        }

        internal void ProcessHits()
        {
            DamageEvent damageEvent;
            while (Projectiles.Hits.TryDequeue(out damageEvent))
            {
                var damagePool = damageEvent.Projectile.System.Values.Ammo.DefaultDamage;
                for (int i = 0; i < damageEvent.HitEntity.Count; i++)
                {
                    if (damagePool <= 0) continue;
                    var hitEnt = damageEvent.HitEntity[i];
                    switch (hitEnt.EventType)
                    {
                        case Type.Shield:
                            DamageShield(hitEnt, ref damageEvent);
                            continue;
                        case Type.Grid:
                            DamageGrid(hitEnt, ref damageEvent);
                            break;
                        case Type.Destroyable:
                            DamageDestObj(hitEnt, ref damageEvent);
                            continue;
                        case Type.Voxel:
                            DamageVoxel(hitEnt, ref damageEvent);
                            continue;
                        case Type.Proximity:
                            DamageProximity(hitEnt, ref damageEvent);
                            continue;
                    }
                }
                if (damageEvent.Projectile.DamagePool <= 0) damageEvent.Projectile.State = Projectile.ProjectileState.Depleted;
                damageEvent.HitEntity.Clear();
            }
        }

        private void DamageShield(HitEntity hitEnt, ref DamageEvent dEvent)
        {
            var shield = hitEnt.Entity as IMyTerminalBlock;
            var system = dEvent.Projectile.System;
            if (shield == null || !hitEnt.HitPos.HasValue) return;
            SApi.PointAttackShield(shield, hitEnt.HitPos.Value, dEvent.Attacker.EntityId, dEvent.Projectile.DamagePool, false, true);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce((MyEntity)shield.CubeGrid, hitEnt.HitPos.Value, dEvent.Direction, system.Values.Ammo.Mass * speed);
            }
            dEvent.Projectile.DamagePool = 0;
        }

        private void DamageGrid(HitEntity hitEnt, ref DamageEvent dEvent)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            var system = dEvent.Projectile.System;

            if (grid == null || grid.MarkedForClose || !hitEnt.HitPos.HasValue || hitEnt.Blocks == null)
            {
                Log.Line($"something null/closed in grid damage");
                return;
            }

            for (int i = 0; i < hitEnt.Blocks.Count; i++)
            {
                var block = hitEnt.Blocks[i];
                var blockHp = block.Integrity;
                var damage = blockHp;
                if (dEvent.Projectile.DamagePool <= 0) continue;
                if (dEvent.Projectile.DamagePool < blockHp)
                {
                    damage = dEvent.Projectile.DamagePool;
                    dEvent.Projectile.DamagePool = 0;
                }
                else dEvent.Projectile.DamagePool -= damage;

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

        private void DamageDestObj(HitEntity hitEnt, ref DamageEvent dEvent)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as IMyDestroyableObject;
            var system = dEvent.Projectile.System;
            if (destObj == null || entity == null) return;
            Log.Line("Test");
            var objHp = destObj.Integrity;
            if (dEvent.Projectile.DamagePool < objHp)
            {
                objHp = dEvent.Projectile.DamagePool;
                dEvent.Projectile.DamagePool = 0;
            }
            else dEvent.Projectile.DamagePool -= objHp;

            destObj.DoDamage(objHp, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, dEvent.Direction, (system.Values.Ammo.Mass * speed));
            }
        }

        private void DamageVoxel(HitEntity hitEnt, ref DamageEvent dEvent)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as MyVoxelBase;
            var system = dEvent.Projectile.System;
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

        private void DamageProximity(HitEntity hitEnt, ref DamageEvent dEvent)
        {
            var system = dEvent.Projectile.System;
            if (hitEnt.HitPos.HasValue)
            {
                if (ExplosionReady) UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, dEvent.Direction, dEvent.Attacker, null, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield);
                else UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, dEvent.Direction, dEvent.Attacker, null, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield, true);
            }
            else if (!hitEnt.Hit == false && hitEnt.HitPos.HasValue) UtilsStatic.CreateFakeExplosion(hitEnt.HitPos.Value, system.Values.Ammo.AreaEffectRadius);
        }

        public static void ApplyProjectileForce(MyEntity entity, Vector3D intersectionPosition, Vector3 normalizedDirection, float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic || entity.Physics.Mass / impulse > 500)
                return;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero);
        }
    }
}
