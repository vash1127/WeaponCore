using System;
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
                    if (projectile.BaseDamagePool <= 0 || projectile.ObjectsHit >= maxObjects)
                    {
                        projectile.State = Projectile.ProjectileState.Depleted;
                        Projectiles.HitEntityPool[projectile.PoolId].Return(hitEnt);
                        continue;
                    }
                    switch (hitEnt.EventType)
                    {
                        case HitEntity.Type.Shield:
                            DamageShield(hitEnt, projectile);
                            continue;
                        case HitEntity.Type.Grid:
                            DamageGrid(hitEnt, projectile);
                            continue;
                        case HitEntity.Type.Destroyable:
                            DamageDestObj(hitEnt, projectile);
                            continue;
                        case HitEntity.Type.Voxel:
                            DamageVoxel(hitEnt, projectile);
                            continue;
                        case HitEntity.Type.Proximity:
                            ExplosionProximity(hitEnt, projectile);
                            continue;
                    }
                    Projectiles.HitEntityPool[projectile.PoolId].Return(hitEnt);
                }

                if (projectile.BaseDamagePool <= 0)
                {
                    //Log.Line($"Depleted2: pool:{projectile.BaseDamagePool} - objHit:{projectile.ObjectsHit}");
                    projectile.State = Projectile.ProjectileState.Depleted;
                }
                projectile.HitList.Clear();
            }
        }

        private void DamageShield(HitEntity hitEnt, Projectile projectile)
        {
            var shield = hitEnt.Entity as IMyTerminalBlock;
            var system = projectile.System;
            if (shield == null || !hitEnt.HitPos.HasValue) return;
            projectile.ObjectsHit++;
            SApi.PointAttackShield(shield, hitEnt.HitPos.Value, projectile.FiringCube.EntityId, projectile.BaseDamagePool, false, true);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce((MyEntity)shield.CubeGrid, hitEnt.HitPos.Value, projectile.Direction, system.Values.Ammo.Mass * speed);
            }
            projectile.BaseDamagePool = 0;
        }

        private readonly HashSet<IMySlimBlock> _destroyedSlims = new HashSet<IMySlimBlock>();
        private void DamageGrid(HitEntity hitEnt, Projectile projectile)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            var system = projectile.System;

            if (grid == null || grid.MarkedForClose || !hitEnt.HitPos.HasValue || hitEnt.Blocks == null)
            {
                //Log.Line($"grid something is null: gridNull:{grid == null} - gridMarked:{grid?.MarkedForClose} - noHitValue:{!hitEnt.HitPos.HasValue} - blocksNull:{hitEnt.Blocks == null}");
                hitEnt.Blocks?.Clear();
                return;
            }
            //Log.Line($"new hit: blockCnt:{grid.BlocksCount} - pool:{projectile.BaseDamagePool} - objHit:{projectile.ObjectsHit}");
            _destroyedSlims.Clear();
            //var cubes = SlimSpace[grid];
            var largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
            var areaRadius = largeGrid ? system.AreaRadiusLarge : system.AreaRadiusSmall;
            var sphere = new BoundingSphereD(hitEnt.HitPos.Value, areaRadius);
            var maxObjects = projectile.System.MaxObjectsHit;
            var areaEffect = system.Values.Ammo.AreaEffect.AreaEffect;
            var explosive = areaEffect == AreaDamage.AreaEffectType.Explosive;
            var radiant = areaEffect == AreaDamage.AreaEffectType.Radiant;
            var detonateOnEnd = system.Values.Ammo.AreaEffect.Detonation.DetonateOnEnd;
            var areaEffectDmg = system.Values.Ammo.AreaEffect.AreaEffectDamage;

            var hasAreaDmg = areaEffectDmg > 0;
            var radiantCascade = radiant && !detonateOnEnd;
            var primeDamage = !radiantCascade || !hasAreaDmg;
            var radiantBomb = radiant && detonateOnEnd;
            var damageType = explosive || radiant ? MyDamageType.Explosion : MyDamageType.Bullet;
            var damagePool = projectile.BaseDamagePool;
            var objectsHit = projectile.ObjectsHit;
            var countBlocksAsObjects = system.Values.Ammo.ObjectsHit.CountBlocks;
            List<Vector3I> radiatedBlocks = null;
            if (radiant)
            {
                if (largeGrid) LargeBlockSphereDb.TryGetValue(areaRadius, out radiatedBlocks);
                else SmallBlockSphereDb.TryGetValue(areaRadius, out radiatedBlocks);
            }
            var done = false;
            var nova = false;
            var outOfPew = false;
            for (int i = 0; i < hitEnt.Blocks.Count; i++)
            {
                if (done || outOfPew) break;

                var rootBlock = hitEnt.Blocks[i];

                if (!nova)
                {
                    if (_destroyedSlims.Contains(rootBlock)) continue;
                    if (rootBlock.IsDestroyed)
                    {
                        _destroyedSlims.Add(rootBlock);
                        continue;
                    }
                }
                var radiate = radiantCascade || nova;

                var dmgCount = 1;
                if (radiate)
                {
                    //GetBlocksInsideSphereBrute(grid, rootBlock.Position, ref sphere, true);
                    //ShiftAndPruneBlockSphere(grid, rootBlock.Position, radiatedBlocks, _slimsSortedList);
                    sphere.Center = grid.GridIntegerToWorld(rootBlock.Position);
                    GetIntVectorsInSphere2(grid, rootBlock.Position, sphere.Radius);
                    done = nova;
                    dmgCount = _slimsSortedList.Count;
                }

                for (int j = 0; j < dmgCount; j++)
                {
                    var block = radiate ? _slimsSortedList[j].Slim : rootBlock;

                    var blockHp = block.Integrity;
                    float damageScale = 1;

                    if (system.DamageScaling)
                    {
                        var d = system.Values.DamageScales;
                        if (d.MaxIntegrity > 0 && blockHp > d.MaxIntegrity) continue;

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

                    var blockIsRoot = block == rootBlock;
                    var primaryDamage = primeDamage || blockIsRoot;

                    if (damagePool <= 0 && primaryDamage || objectsHit >= maxObjects) break;

                    var scaledDamage = damagePool * damageScale;

                    if (primaryDamage)
                    {
                        if (countBlocksAsObjects) objectsHit++;

                        if (scaledDamage <= blockHp)
                        {
                            outOfPew = true;
                            damagePool = 0;
                        }
                        else
                        {
                            _destroyedSlims.Add(block);
                            damagePool -= blockHp;
                        }
                    }
                    else
                    {
                        scaledDamage = areaEffectDmg * damageScale;
                        if (scaledDamage >= blockHp) _destroyedSlims.Add(block);
                    }

                    block.DoDamage(scaledDamage, damageType, true, null, projectile.FiringCube.EntityId);
                    var theEnd = damagePool <= 0 || objectsHit >= maxObjects;

                    if (explosive && !nova && ((!detonateOnEnd && blockIsRoot) || detonateOnEnd && theEnd))
                    {
                        var aInfo = system.Values.Ammo.AreaEffect;
                        var dInfo = aInfo.Detonation;
                        var damage = detonateOnEnd && theEnd ? dInfo.DetonationDamage : aInfo.AreaEffectDamage;
                        var radius = detonateOnEnd && theEnd ? dInfo.DetonationRadius : aInfo.AreaEffectRadius;
                        if (ExplosionReady) UtilsStatic.CreateMissileExplosion(damage, radius, hitEnt.HitPos.Value, projectile.Direction, projectile.FiringCube, grid, system);
                        else UtilsStatic.CreateMissileExplosion(damage, radius, hitEnt.HitPos.Value, projectile.Direction, projectile.FiringCube, grid, system, true);
                    }
                    else if (!nova)
                    {
                        if (system.Values.Ammo.Mass > 0 && blockIsRoot)
                        {
                            var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                            ApplyProjectileForce(grid, hitEnt.HitPos.Value, projectile.Direction, (system.Values.Ammo.Mass * speed));
                        }

                        if (radiantBomb && theEnd)
                        {
                            nova = true;
                            i--;
                            projectile.BaseDamagePool = 0;
                            projectile.ObjectsHit = maxObjects;
                            objectsHit = int.MinValue;
                            var aInfo = system.Values.Ammo.AreaEffect;
                            var dInfo = aInfo.Detonation;

                            sphere.Radius = dInfo.DetonationRadius;

                            if (dInfo.DetonationDamage > 0) damagePool = dInfo.DetonationDamage;
                            else if (aInfo.AreaEffectDamage > 0) damagePool = aInfo.AreaEffectDamage;
                            else damagePool = scaledDamage;
                            //Log.Line($"[raidant end] scaled:{scaledDamage} - area:{system.Values.Ammo.AreaEffect.AreaEffectDamage} - pool:{damagePool}({projectile.BaseDamagePool}) - objHit:{projectile.ObjectsHit} - gridBlocks:{grid.CubeBlocks.Count}({((MyCubeGrid)rootBlock.CubeGrid).BlocksCount}) - i:{i} j:{j}");
                            break;
                        }
                    }
                }
            }
            if (!countBlocksAsObjects) projectile.ObjectsHit += 1;
            if (!nova)
            {
                projectile.BaseDamagePool = damagePool;
                projectile.ObjectsHit = objectsHit;
                //Log.Line($"not end game: pool:{damagePool} - objHit:{objectsHit}" );
            }
            hitEnt.Blocks.Clear();
        }

        private void DamageDestObj(HitEntity hitEnt, Projectile projectile)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as IMyDestroyableObject;
            var system = projectile.System;
            if (destObj == null || entity == null) return;
            //projectile.ObjectsHit++;

            var objHp = destObj.Integrity;
            var integrityCheck = system.Values.DamageScales.MaxIntegrity > 0;
            if (integrityCheck && objHp > system.Values.DamageScales.MaxIntegrity) return;

            var character = hitEnt.Entity is IMyCharacter;
            float damageScale = 1;
            if (character && system.Values.DamageScales.Characters >= 0)
                damageScale *= system.Values.DamageScales.Characters;

            var scaledDamage = projectile.BaseDamagePool * damageScale;

            if (scaledDamage < objHp) projectile.BaseDamagePool = 0;
            else projectile.BaseDamagePool -= objHp;

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

            var baseDamage = system.Values.Ammo.BaseDamage;
            var damage = baseDamage;
            projectile.ObjectsHit++; // add up voxel units

            //destObj.DoDamage(damage, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
        }

        private void ExplosionProximity(HitEntity hitEnt, Projectile projectile)
        {
            var system = projectile.System;
            projectile.BaseDamagePool = 0;
            var radius = system.Values.Ammo.AreaEffect.AreaEffectRadius;
            var damage = system.Values.Ammo.AreaEffect.AreaEffectDamage;

            if (hitEnt.HitPos.HasValue)
            {
                if (ExplosionReady)
                    UtilsStatic.CreateMissileExplosion(damage, radius, hitEnt.HitPos.Value, projectile.Direction, projectile.FiringCube, hitEnt.Entity, system);
                else
                    UtilsStatic.CreateMissileExplosion(damage, radius, hitEnt.HitPos.Value, projectile.Direction, projectile.FiringCube, hitEnt.Entity, system, true);
            }
            else if (!hitEnt.Hit == false && hitEnt.HitPos.HasValue)
                UtilsStatic.CreateFakeExplosion(radius, hitEnt.HitPos.Value, system);
        }

        public static void ApplyProjectileForce(MyEntity entity, Vector3D intersectionPosition, Vector3 normalizedDirection, float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic || entity.Physics.Mass / impulse > 500)
                return;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero);
        }
    }
}
