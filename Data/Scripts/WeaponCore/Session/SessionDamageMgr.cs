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
        internal void ProcessHits()
        {
            Projectile projectile;
            while (Projectiles.Hits.TryDequeue(out projectile))
            {
                var maxObjects = projectile.System.MaxObjectsHit;
                for (int i = 0; i < projectile.HitList.Count; i++)
                {
                    var hitEnt = projectile.HitList[i];
                    if (projectile.DamagePool <= 0 || projectile.ObjectsHit >= maxObjects)
                    {
                        projectile.State = Projectile.ProjectileState.Depleted;
                        Projectiles.HitEntityPool[projectile.PoolId].Return(hitEnt);
                        continue;
                    }
                    switch (hitEnt.EventType)
                    {
                        case Type.Shield:
                            DamageShield(hitEnt, projectile);
                            continue;
                        case Type.Grid:
                            DamageGrid(hitEnt, projectile);
                            continue;
                        case Type.Destroyable:
                            DamageDestObj(hitEnt, projectile);
                            continue;
                        case Type.Voxel:
                            DamageVoxel(hitEnt, projectile);
                            continue;
                        case Type.Proximity:
                            DamageProximity(hitEnt, projectile);
                            continue;
                    }
                    Projectiles.HitEntityPool[projectile.PoolId].Return(hitEnt);
                }
                if (projectile.DamagePool <= 0) projectile.State = Projectile.ProjectileState.Depleted;
                projectile.HitList.Clear();
            }
        }

        private void DamageShield(HitEntity hitEnt, Projectile projectile)
        {
            var shield = hitEnt.Entity as IMyTerminalBlock;
            var system = projectile.System;
            if (shield == null || !hitEnt.HitPos.HasValue) return;
            projectile.ObjectsHit++;
            SApi.PointAttackShield(shield, hitEnt.HitPos.Value, projectile.FiringCube.EntityId, projectile.DamagePool, false, true);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce((MyEntity)shield.CubeGrid, hitEnt.HitPos.Value, projectile.Direction, system.Values.Ammo.Mass * speed);
            }
            projectile.DamagePool = 0;
        }

        private void DamageGrid(HitEntity hitEnt, Projectile projectile)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            var system = projectile.System;

            if (grid == null || grid.MarkedForClose || !hitEnt.HitPos.HasValue || hitEnt.Blocks == null)
            {
                Log.Line("grid something is null");
                hitEnt.Blocks?.Clear();
                return;
            }
            var maxObjects = projectile.System.MaxObjectsHit;
            var largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
            var d = system.Values.DamageScales;
            var integrityCheck = d.MaxIntegrity > 0;
            for (int i = 0; i < hitEnt.Blocks.Count; i++)
            {
                var block = hitEnt.Blocks[i];
                var blockHp = block.Integrity;
                if (block.IsDestroyed || blockHp <= 0) continue;
                float damageScale = 1;
                if (system.DamageScaling)
                {
                    if (integrityCheck && blockHp > d.MaxIntegrity) continue;

                    if (d.Grids.Large >= 0 && largeGrid) damageScale *= d.Grids.Large;
                    else if (d.Grids.Small >= 0 && !largeGrid) damageScale *= d.Grids.Small;

                    MyDefinitionBase blockDef = null;
                    if (system.ArmorScaling)
                    {
                        blockDef = block.BlockDefinition;
                        var isArmor = AllArmorBaseDefinitions.Contains(blockDef);
                        if (isArmor && d.Armor.Armor >= 0) damageScale *= d.Armor.Armor;
                        else if (!isArmor && d.Armor.NonArmor >= 0) damageScale *= d.Armor.NonArmor;

                        if (isArmor && (d.Armor.Light >= 0 || d.Armor.Heavy >= 0))
                        {
                            var isHeavy = HeavyArmorBaseDefinitions.Contains(blockDef);
                            if (isHeavy && d.Armor.Heavy >= 0) damageScale *= d.Armor.Heavy;
                            else if (!isHeavy && d.Armor.Light >= 0) damageScale *= d.Armor.Light;
                        }
                    }
                    if (system.CustomDamageScales)
                    {
                        if (blockDef == null) blockDef = block.BlockDefinition;
                        float modifier;
                        var found = system.CustomBlockDefinitionBasesToScales.TryGetValue(blockDef, out modifier);

                        if (found) damageScale *= modifier;
                        else if (system.Values.DamageScales.Custom.IgnoreAllOthers) continue;
                    }
                }
                if (projectile.DamagePool <= 0 || projectile.ObjectsHit >= maxObjects) break;
                projectile.ObjectsHit++;

                var scaledDamage = projectile.DamagePool * damageScale;

                if (scaledDamage < blockHp) projectile.DamagePool = 0;
                else projectile.DamagePool -= scaledDamage;

                block.DoDamage(scaledDamage, MyDamageType.Bullet, true, null, projectile.FiringCube.EntityId);
                if (system.AmmoAreaEffect)
                {
                    if (ExplosionReady) UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, projectile.Direction, projectile.FiringCube, grid, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield);
                    else UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, projectile.Direction, projectile.FiringCube, grid, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield, true);
                }
                else if (system.Values.Ammo.Mass > 0)
                {
                    var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                    ApplyProjectileForce(grid, hitEnt.HitPos.Value, projectile.Direction, (system.Values.Ammo.Mass * speed));
                }
            }
            hitEnt.Blocks.Clear();
        }

        private void DamageDestObj(HitEntity hitEnt, Projectile projectile)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as IMyDestroyableObject;
            var system = projectile.System;
            if (destObj == null || entity == null) return;
            projectile.ObjectsHit++;

            var objHp = destObj.Integrity;
            var integrityCheck = system.Values.DamageScales.MaxIntegrity > 0;
            if (integrityCheck && objHp > system.Values.DamageScales.MaxIntegrity) return;

            var character = hitEnt.Entity is IMyCharacter;
            float damageScale = 1;
            if (character && system.Values.DamageScales.Characters >= 0)
                damageScale *= system.Values.DamageScales.Characters;

            var scaledDamage = projectile.DamagePool * damageScale;

            if (scaledDamage < objHp) projectile.DamagePool = 0;
            else projectile.DamagePool -= scaledDamage;

            destObj.DoDamage(scaledDamage, MyDamageType.Bullet, true, null, projectile.FiringCube.EntityId);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, projectile.Direction, (system.Values.Ammo.Mass * speed));
            }
        }

        private void DamageVoxel(HitEntity hitEnt, Projectile projectile)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as MyVoxelBase;
            var system = projectile.System;
            if (destObj == null || entity == null || !system.Values.DamageScales.DamageVoxels) return;

            var baseDamage = system.Values.Ammo.DefaultDamage;
            var damage = baseDamage;
            projectile.ObjectsHit++; // add up voxel units

            //destObj.DoDamage(damage, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
        }

        private void DamageProximity(HitEntity hitEnt, Projectile projectile)
        {
            var system = projectile.System;
            projectile.DamagePool = 0;
            if (hitEnt.HitPos.HasValue)
            {
                if (ExplosionReady)
                    UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, projectile.Direction, projectile.FiringCube, hitEnt.Entity, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield);
                else
                    UtilsStatic.CreateMissileExplosion(hitEnt.HitPos.Value, projectile.Direction, projectile.FiringCube, hitEnt.Entity, system.Values.Ammo.AreaEffectRadius, system.Values.Ammo.AreaEffectYield, true);
            }
            else if (!hitEnt.Hit == false && hitEnt.HitPos.HasValue)
                UtilsStatic.CreateFakeExplosion(hitEnt.HitPos.Value, system.Values.Ammo.AreaEffectRadius);
        }

        public static void ApplyProjectileForce(MyEntity entity, Vector3D intersectionPosition, Vector3 normalizedDirection, float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic || entity.Physics.Mass / impulse > 500)
                return;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero);
        }
    }
}
