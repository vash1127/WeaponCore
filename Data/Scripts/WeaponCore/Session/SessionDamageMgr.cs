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
using static WeaponCore.Support.WeaponDefinition.AmmoDef.AreaDamageDef;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.DamageScaleDef;
using static WeaponCore.Support.WeaponSystem.TurretType;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType;
namespace WeaponCore
{
    public struct RadiatedBlock
    {
        public Vector3I Center;
        public IMySlimBlock Slim;
        public Vector3I Position;
    }

    public partial class Session
    {
        internal void ProcessHits()
        {
            for (int x = 0; x < Hits.Count; x++)
            {
                var p = Hits[x];
                var info = p.Info;
                var maxObjects = info.AmmoDef.Const.MaxObjectsHit;
                var phantom = info.AmmoDef.BaseDamage <= 0;
                var pInvalid = (int) p.State > 3;
                var tInvalid = info.Target.IsProjectile && (int)info.Target.Projectile.State > 1;
                if (tInvalid) info.Target.Reset(info.Ai.Session.Tick, Target.States.ProjectileClosed);
                var skip = pInvalid || tInvalid;
                var fixedFire = p.Info.System.TurretMovement == Fixed && p.Guidance == None && !p.Info.AmmoDef.Const.IsBeamWeapon;
                var canDamage = !MpActive || !IsClient && (!fixedFire || (p.Info.IsFiringPlayer || p.Info.ClientSent));

                if (IsClient && p.Info.IsFiringPlayer && fixedFire)
                {
                    SendFixedGunHitEvent(p.Info.Target.FiringCube, p.Hit.Entity, info.HitList[0].HitPos ??Vector3.Zero, info.Direction, p.Info.OriginUp, p.Info.MuzzleId, info.System.WeaponIdHash);
                    p.Info.IsFiringPlayer = false; //to prevent hits on another grid from triggering again
                }

                for (int i = 0; i < info.HitList.Count; i++)
                {
                    var hitEnt = info.HitList[i];
                    var hitMax = info.ObjectsHit >= maxObjects;
                    var outOfPew = info.BaseDamagePool <= 0 && !(phantom && hitEnt.EventType == HitEntity.Type.Effect);

                    if (skip || hitMax || outOfPew)
                    {
                        if (hitMax || outOfPew || pInvalid)
                        {
                            p.State = Projectile.ProjectileState.Depleted;
                        }
                        Projectiles.HitEntityPool.Return(hitEnt);
                        continue;
                    }
                    switch (hitEnt.EventType)
                    {
                        case HitEntity.Type.Shield:
                            DamageShield(hitEnt, info);
                            continue;
                        case HitEntity.Type.Grid:
                            DamageGrid(hitEnt, info, canDamage);
                            continue;
                        case HitEntity.Type.Destroyable:
                            DamageDestObj(hitEnt, info, canDamage);
                            continue;
                        case HitEntity.Type.Voxel:
                            DamageVoxel(hitEnt, info, canDamage);
                            continue;
                        case HitEntity.Type.Projectile:
                            DamageProjectile(hitEnt, info);
                            continue;
                        case HitEntity.Type.Field:
                            UpdateField(hitEnt, info);
                            continue;
                        case HitEntity.Type.Effect:
                            UpdateEffect(hitEnt, info);
                            continue;
                    }
                    Projectiles.HitEntityPool.Return(hitEnt);
                }

                if (info.BaseDamagePool <= 0)
                    p.State = Projectile.ProjectileState.Depleted;

                info.HitList.Clear();
            }
            Hits.Clear();
        }

        private void DamageShield(HitEntity hitEnt, ProInfo info)
        {
            var shield = hitEnt.Entity as IMyTerminalBlock;
            if (shield == null || !hitEnt.HitPos.HasValue) return;
            info.ObjectsHit++;

            var damageScale = 1;
            var fallOff = info.AmmoDef.Const.FallOffScaling && info.DistanceTraveled > info.AmmoDef.DamageScales.FallOff.Distance;
            if (info.AmmoDef.Const.VirtualBeams) damageScale *= info.WeaponCache.Hits;
            var damageType = info.AmmoDef.DamageScales.Shields.Type;
            var energy = damageType == ShieldDef.ShieldType.Energy;
            var heal = damageType == ShieldDef.ShieldType.Heal;
            var shieldByPass = info.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Bypass;

            var areaEffect = info.AmmoDef.AreaEffect;
            var detonateOnEnd = info.AmmoDef.AreaEffect.Detonation.DetonateOnEnd && areaEffect.AreaEffect != AreaEffectType.Disabled && !shieldByPass;
            var areaDamage = areaEffect.AreaEffect != AreaEffectType.Disabled ? areaEffect.AreaEffectDamage * (areaEffect.AreaEffectRadius * 0.5f) : 0;
            var scaledDamage = (((info.BaseDamagePool * damageScale) + areaDamage) * info.AmmoDef.Const.ShieldModifier) * info.AmmoDef.Const.ShieldBypassMod;
            if (fallOff)
            {
                var fallOffMultipler = MathHelperD.Clamp(1.0 - ((info.DistanceTraveled - info.AmmoDef.DamageScales.FallOff.Distance) / (info.AmmoDef.Const.MaxTrajectory - info.AmmoDef.DamageScales.FallOff.Distance)), info.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
                scaledDamage *= fallOffMultipler;
            }
            
            var detonateDamage = detonateOnEnd ? (areaEffect.Detonation.DetonationDamage * (areaEffect.Detonation.DetonationRadius * 0.5f)) * info.AmmoDef.Const.ShieldModifier : 0;

            var combinedDamage = (float) (scaledDamage + detonateDamage);
           
            if (heal) combinedDamage *= -1;

            var hit = SApi.PointAttackShieldExt(shield, hitEnt.HitPos.Value, info.Target.FiringCube.EntityId, combinedDamage, energy, info.AmmoDef.AmmoGraphics.ShieldHitDraw);
            if (hit.HasValue)
            {
                if (heal)
                {
                    info.BaseDamagePool = 0;
                    return;
                }
                var objHp = hit.Value;
                if (info.EwarActive)
                    info.BaseHealthPool -= 1;
                else if (objHp > 0)
                {
                    if (!shieldByPass)
                        info.BaseDamagePool = 0;
                    else 
                        info.BaseDamagePool *= info.AmmoDef.Const.ShieldBypassMod;
                }
                else info.BaseDamagePool = (objHp * -1);
                if (info.AmmoDef.Mass <= 0) return;

                var speed = info.AmmoDef.Trajectory.DesiredSpeed > 0 ? info.AmmoDef.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce((MyEntity)shield.CubeGrid, hitEnt.HitPos.Value, hitEnt.Intersection.Direction, info.AmmoDef.Mass * speed);
            }
        }

        private void DamageGrid(HitEntity hitEnt, ProInfo t, bool canDamage)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            if (grid == null || grid.MarkedForClose || !hitEnt.HitPos.HasValue || hitEnt.Blocks == null) {
                hitEnt.Blocks?.Clear();
                return;
            }
            if (t.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Heal || (!t.AmmoDef.Const.SelfDamage || !MyAPIGateway.Session.SessionSettings.EnableTurretsFriendlyFire) && t.Ai.MyGrid.IsSameConstructAs(grid)) {
                t.BaseDamagePool = 0;
                return;
            }

            _destroyedSlims.Clear();
            _destroyedSlimsClient.Clear();
            var largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
            var areaRadius = largeGrid ? t.AmmoDef.Const.AreaRadiusLarge : t.AmmoDef.Const.AreaRadiusSmall;
            var detonateRadius = largeGrid ? t.AmmoDef.Const.DetonateRadiusLarge : t.AmmoDef.Const.DetonateRadiusSmall;
            var maxObjects = t.AmmoDef.Const.MaxObjectsHit;
            var areaEffect = t.AmmoDef.AreaEffect.AreaEffect;
            var explosive = areaEffect == AreaEffectType.Explosive;
            var radiant = areaEffect == AreaEffectType.Radiant;
            var detonateOnEnd = t.AmmoDef.AreaEffect.Detonation.DetonateOnEnd;
            var detonateDmg = t.DetonationDamage;
            var shieldBypass = t.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Bypass;
            var attackerId = shieldBypass ? grid.EntityId : t.Target.FiringCube.EntityId;
            var attacker = shieldBypass ? (MyEntity)grid : t.Target.FiringCube;
            var areaEffectDmg = areaEffect != AreaEffectType.Disabled ? t.AreaEffectDamage : 0;
            var hitMass = t.AmmoDef.Mass;
            var sync = MpActive && (DedicatedServer || IsServer);
            var hasAreaDmg = areaEffectDmg > 0;
            var radiantCascade = radiant && !detonateOnEnd;
            var primeDamage = !radiantCascade || !hasAreaDmg;
            var radiantBomb = radiant && detonateOnEnd;
            var damageType = explosive || radiant ? MyDamageType.Explosion : MyDamageType.Bullet;

            var fallOff = t.AmmoDef.Const.FallOffScaling && t.DistanceTraveled > t.AmmoDef.DamageScales.FallOff.Distance;
            var fallOffMultipler = 1f;
            if (fallOff)
                fallOffMultipler = (float) MathHelperD.Clamp(1.0 - ((t.DistanceTraveled - t.AmmoDef.DamageScales.FallOff.Distance) / (t.AmmoDef.Const.MaxTrajectory - t.AmmoDef.DamageScales.FallOff.Distance)), t.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
            
            var damagePool = t.BaseDamagePool;
            if (t.AmmoDef.Const.VirtualBeams)
            {
                var hits = t.WeaponCache.Hits;
                damagePool *= hits;
                areaEffectDmg *= hits;
            }
            var objectsHit = t.ObjectsHit;
            var countBlocksAsObjects = t.AmmoDef.ObjectsHit.CountBlocks;

            List<Vector3I> radiatedBlocks = null;
            if (radiant) GetBlockSphereDb(grid, areaRadius, out radiatedBlocks);

            var done = false;
            var nova = false;
            var outOfPew = false;
            for (int i = 0; i < hitEnt.Blocks.Count; i++)
            {
                if (done || outOfPew && !nova) break;

                var rootBlock = hitEnt.Blocks[i];

                if (!nova)
                {
                    if (_destroyedSlims.Contains(rootBlock) || _destroyedSlimsClient.Contains(rootBlock)) continue;
                    if (rootBlock.IsDestroyed)
                    {
                        _destroyedSlims.Add(rootBlock);
                        if (IsClient)
                        {
                            _destroyedSlimsClient.Add(rootBlock);
                            if (_slimHealthClient.ContainsKey(rootBlock))
                                _slimHealthClient.Remove(rootBlock);
                        }
                        continue;
                    }
                }
                var door = rootBlock.FatBlock as MyDoorBase;
                if (door != null && door.Open && !HitDoor(hitEnt, door))
                    continue;

                var radiate = radiantCascade || nova;
                var dmgCount = 1;
                if (radiate)
                {
                    if (nova) GetBlockSphereDb(grid, detonateRadius, out radiatedBlocks);
                    if (radiatedBlocks != null) ShiftAndPruneBlockSphere(grid, rootBlock.Position, radiatedBlocks, SlimsSortedList);

                    done = nova;
                    dmgCount = SlimsSortedList.Count;
                }

                for (int j = 0; j < dmgCount; j++)
                {
                    var block = radiate ? SlimsSortedList[j].Slim : rootBlock;
                    var blockHp = !IsClient ? block.Integrity : _slimHealthClient.ContainsKey(block) ? _slimHealthClient[block] : block.Integrity;
                    float damageScale = 1;

                    if (t.AmmoDef.Const.DamageScaling)
                    {
                        var d = t.AmmoDef.DamageScales;
                        if (d.MaxIntegrity > 0 && blockHp > d.MaxIntegrity)
                        {
                            outOfPew = true;
                            damagePool = 0;
                            continue;
                        }

                        if (d.Grids.Large >= 0 && largeGrid) damageScale *= d.Grids.Large;
                        else if (d.Grids.Small >= 0 && !largeGrid) damageScale *= d.Grids.Small;

                        MyDefinitionBase blockDef = null;
                        if (t.AmmoDef.Const.ArmorScaling)
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
                        if (t.AmmoDef.Const.CustomDamageScales)
                        {
                            if (blockDef == null) blockDef = block.BlockDefinition;
                            float modifier;
                            var found = t.AmmoDef.Const.CustomBlockDefinitionBasesToScales.TryGetValue(blockDef, out modifier);

                            if (found) damageScale *= modifier;
                            else if (t.AmmoDef.DamageScales.Custom.IgnoreAllOthers) continue;
                        }
                        if (fallOff)
                            damageScale *= fallOffMultipler;
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
                            if (IsClient)
                            {
                                _destroyedSlimsClient.Add(block);
                                if (_slimHealthClient.ContainsKey(block))
                                    _slimHealthClient.Remove(block);
                            }
                            damagePool -= blockHp;
                        }
                    }
                    else
                    {
                        scaledDamage = areaEffectDmg * damageScale;
                        if (scaledDamage >= blockHp)
                        {
                            _destroyedSlims.Add(block);
                            if (IsClient)
                            {
                                _destroyedSlimsClient.Add(block);
                                if (_slimHealthClient.ContainsKey(block))
                                    _slimHealthClient.Remove(block);
                            }
                        }
                    }

                    if (canDamage)
                        block.DoDamage(scaledDamage, damageType, sync, null, attackerId);
                    else
                    {
                        var hasBlock = _slimHealthClient.ContainsKey(block);
                        if (hasBlock && _slimHealthClient[block] - scaledDamage > 0)
                            _slimHealthClient[block] -= scaledDamage;
                        else if (hasBlock)
                            _slimHealthClient.Remove(block);
                        else if (block.Integrity - scaledDamage > 0)
                            _slimHealthClient[block] = blockHp - scaledDamage;
                    }

                    var theEnd = damagePool <= 0 || objectsHit >= maxObjects;

                    if (explosive && (!detonateOnEnd && blockIsRoot || detonateOnEnd && theEnd))
                    {
                        var rootPos = grid.GridIntegerToWorld(rootBlock.Position);
                        if (areaEffectDmg > 0) SUtils.CreateMissileExplosion(this, areaEffectDmg * damageScale, areaRadius, rootPos, hitEnt.Intersection.Direction, attacker, grid, t.AmmoDef, true);
                        if (detonateOnEnd && theEnd) SUtils.CreateMissileExplosion(this, detonateDmg  * damageScale, detonateRadius, rootPos, hitEnt.Intersection.Direction, attacker, grid, t.AmmoDef, true);
                    }
                    else if (!nova)
                    {
                        if (hitMass > 0 && blockIsRoot)
                        {
                            var speed = t.AmmoDef.Trajectory.DesiredSpeed > 0 ? t.AmmoDef.Trajectory.DesiredSpeed : 1;
                            ApplyProjectileForce(grid, grid.GridIntegerToWorld(rootBlock.Position), hitEnt.Intersection.Direction, (hitMass * speed));
                        }

                        if (radiantBomb && theEnd)
                        {
                            nova = true;
                            i--;
                            t.BaseDamagePool = 0;
                            t.ObjectsHit = maxObjects;
                            objectsHit = int.MinValue;
                            var aInfo = t.AmmoDef.AreaEffect;
                            var dInfo = aInfo.Detonation;

                            if (dInfo.DetonationDamage > 0) damagePool = detonateDmg;
                            else if (aInfo.AreaEffectDamage > 0) damagePool = areaEffectDmg;
                            else damagePool = scaledDamage;
                            break;
                        }
                    }
                }
            }
            if (!countBlocksAsObjects) t.ObjectsHit += 1;
            if (!nova)
            {
                t.BaseDamagePool = damagePool;
                t.ObjectsHit = objectsHit;
            }
            if (radiantCascade || nova) SlimsSortedList.Clear();
            hitEnt.Blocks.Clear();
        }

        private void DamageDestObj(HitEntity hitEnt, ProInfo info, bool canDamage)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as IMyDestroyableObject;

            if (destObj == null || entity == null) return;
            var shieldHeal = info.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Heal;
            var shieldByPass = info.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Bypass;
            var sync = MpActive && IsServer;

            var attackerId = info.Target.FiringCube.EntityId;

            var objHp = destObj.Integrity;
            var integrityCheck = info.AmmoDef.DamageScales.MaxIntegrity > 0;
            if (integrityCheck && objHp > info.AmmoDef.DamageScales.MaxIntegrity || shieldHeal)
            {
                info.BaseDamagePool = 0;
                return;
            }

            var character = hitEnt.Entity as IMyCharacter;
            float damageScale = 1;
            if (info.AmmoDef.Const.VirtualBeams) damageScale *= info.WeaponCache.Hits;
            if (character != null && info.AmmoDef.DamageScales.Characters >= 0)
                damageScale *= info.AmmoDef.DamageScales.Characters;

            var scaledDamage = info.BaseDamagePool * damageScale;

            var fallOff = info.AmmoDef.Const.FallOffScaling && info.DistanceTraveled > info.AmmoDef.DamageScales.FallOff.Distance;
            if (fallOff)
            {
                var fallOffMultipler = (float)MathHelperD.Clamp(1.0 - ((info.DistanceTraveled - info.AmmoDef.DamageScales.FallOff.Distance) / (info.AmmoDef.Const.MaxTrajectory - info.AmmoDef.DamageScales.FallOff.Distance)), info.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
                scaledDamage *= fallOffMultipler;
            }

            if (scaledDamage < objHp) info.BaseDamagePool = 0;
            else info.BaseDamagePool -= objHp;

            if(canDamage)
                destObj.DoDamage(scaledDamage, !shieldByPass ? MyDamageType.Bullet : MyDamageType.Drill, sync, null, attackerId);
            if (info.AmmoDef.Mass > 0)
            {
                var speed = info.AmmoDef.Trajectory.DesiredSpeed > 0 ? info.AmmoDef.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, hitEnt.Intersection.Direction, (info.AmmoDef.Mass * speed));
            }
        }

        private static void DamageProjectile(HitEntity hitEnt, ProInfo attacker)
        {
            var pTarget = hitEnt.Projectile;
            if (pTarget == null) return;

            attacker.ObjectsHit++;
            var objHp = pTarget.Info.BaseHealthPool;
            var integrityCheck = attacker.AmmoDef.DamageScales.MaxIntegrity > 0;
            if (integrityCheck && objHp > attacker.AmmoDef.DamageScales.MaxIntegrity) return;

            float damageScale = 1;
            if (attacker.AmmoDef.Const.VirtualBeams) damageScale *= attacker.WeaponCache.Hits;

            var scaledDamage = attacker.BaseDamagePool * damageScale;

            var fallOff = attacker.AmmoDef.Const.FallOffScaling && attacker.DistanceTraveled > attacker.AmmoDef.DamageScales.FallOff.Distance;
            if (fallOff)
            {
                var fallOffMultipler = (float)MathHelperD.Clamp(1.0 - ((attacker.DistanceTraveled - attacker.AmmoDef.DamageScales.FallOff.Distance) / (attacker.AmmoDef.Const.MaxTrajectory - attacker.AmmoDef.DamageScales.FallOff.Distance)), attacker.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
                scaledDamage *= fallOffMultipler;
            }

            if (scaledDamage >= objHp)
            {
                attacker.BaseDamagePool -= objHp;
                pTarget.Info.BaseHealthPool = 0;
                pTarget.State = Projectile.ProjectileState.Destroy;
            }
            else 
            {
                attacker.BaseDamagePool = 0;
                pTarget.Info.BaseHealthPool -= scaledDamage;

                if (attacker.DetonationDamage > 0 && attacker.AmmoDef.AreaEffect.Detonation.DetonateOnEnd)
                {
                    var areaSphere = new BoundingSphereD(pTarget.Position, attacker.AmmoDef.AreaEffect.Detonation.DetonationRadius);
                    foreach (var sTarget in attacker.Ai.LiveProjectile)
                    {
                        if (areaSphere.Contains(sTarget.Position) != ContainmentType.Disjoint)
                        {
                            if (attacker.DetonationDamage >= sTarget.Info.BaseHealthPool)
                            {
                                sTarget.Info.BaseHealthPool = 0;
                                sTarget.State = Projectile.ProjectileState.Destroy;
                            }
                            else sTarget.Info.BaseHealthPool -= attacker.DetonationDamage;
                        }
                    }
                }
            }
        }

        private void DamageVoxel(HitEntity hitEnt, ProInfo info, bool canDamage)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as MyVoxelBase;
            if (destObj == null || entity == null || !hitEnt.HitPos.HasValue) return;
            var shieldHeal = info.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Heal;
            if (!info.AmmoDef.Const.VoxelDamage || shieldHeal)
            {
                info.BaseDamagePool = 0;
                return;
            }

            using (destObj.Pin())
            {
                var detonateOnEnd = info.AmmoDef.Const.AmmoAreaEffect && info.AmmoDef.AreaEffect.Detonation.DetonateOnEnd && info.AmmoDef.AreaEffect.AreaEffect != AreaEffectType.Radiant;

                info.ObjectsHit++;
                float damageScale = 1;
                if (info.AmmoDef.Const.VirtualBeams) damageScale *= info.WeaponCache.Hits;

                var scaledDamage = info.BaseDamagePool * damageScale;
                var fallOff = info.AmmoDef.Const.FallOffScaling && info.DistanceTraveled > info.AmmoDef.DamageScales.FallOff.Distance;
                if (fallOff)
                {
                    var fallOffMultipler = (float)MathHelperD.Clamp(1.0 - ((info.DistanceTraveled - info.AmmoDef.DamageScales.FallOff.Distance) / (info.AmmoDef.Const.MaxTrajectory - info.AmmoDef.DamageScales.FallOff.Distance)), info.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
                    scaledDamage *= fallOffMultipler;
                }

                var oRadius = info.AmmoDef.AreaEffect.AreaEffectRadius;
                var minTestRadius = info.DistanceTraveled - info.PrevDistanceTraveled;
                var tRadius = oRadius < minTestRadius ? minTestRadius : oRadius;
                var objHp = (int)MathHelper.Clamp(MathFuncs.VolumeCube(MathFuncs.LargestCubeInSphere(tRadius)), 5000, double.MaxValue);


                if (tRadius > 5) objHp *= 5;
                if (scaledDamage < objHp)
                {
                    var reduceBy = objHp / scaledDamage;
                    oRadius /= reduceBy;
                    if (oRadius < 1) oRadius = 1;

                    info.BaseDamagePool = 0;
                }
                else
                {
                    info.BaseDamagePool -= objHp;
                    if (oRadius < minTestRadius) oRadius = minTestRadius;
                }

                destObj.PerformCutOutSphereFast(hitEnt.HitPos.Value, (float)oRadius, true);
                //Log.Line($"TestHealth: {objHp} - tRadius:{tRadius} - oRadius:{oRadius} - travel:{minTestRadius} - base:{info.BaseDamagePool} - det:{detonateOnEnd}");

                if (detonateOnEnd && info.BaseDamagePool <= 0)
                {
                    var det = info.AmmoDef.AreaEffect.Detonation;
                    var dRadius = det.DetonationRadius;
                    var dDamage = det.DetonationDamage;

                    //var dObjHp = (int)MathHelper.Clamp(MathFuncs.VolumeCube(MathFuncs.LargestCubeInSphere(dRadius)), 5000, double.MaxValue);
                    //if (dRadius > 5) dObjHp *= 5;
                    //dObjHp *= 5;
                    //var reduceBy = dObjHp / dDamage;
                    //dRadius /= reduceBy;

                    if (dRadius < 1.5) dRadius = 1.5f;

                    //Log.Line($"radius: {det.DetonationRadius} - dRadius:{dRadius} - reduceBy:{reduceBy} - dObjHp:{dObjHp}");
                    if (canDamage)
                    {
                        //destObj.PerformCutOutSphereFast(hitEnt.HitPos.Value, dRadius, true);
                        SUtils.CreateMissileExplosion(this, dDamage, dRadius, hitEnt.HitPos.Value, hitEnt.Intersection.Direction, info.Target.FiringCube, destObj, info.AmmoDef, true);
                    }
                }
            }
        }

        private readonly List<MyEntity> _detList = new List<MyEntity>();
        private void Detonate(HitEntity hitEnt, ProInfo info, float radius)
        {
            var detSphere = new BoundingSphereD(hitEnt.Intersection.To, radius);
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref detSphere, _detList);
            for (int i = 0; i < _detList.Count; i++)
            {
                
            }
        }

        public static void ApplyProjectileForce(MyEntity entity, Vector3D intersectionPosition, Vector3 normalizedDirection, float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic || entity.Physics.Mass / impulse > 500)
                return;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero);
        }

        private bool HitDoor(HitEntity hitEnt, MyDoorBase door)
        {
            var ray = new RayD(ref hitEnt.Intersection.From, ref hitEnt.Intersection.Direction);
            var rayHit = ray.Intersects(door.PositionComp.WorldVolume);
            if (rayHit != null)
            {
                var hitPos = hitEnt.Intersection.From + (hitEnt.Intersection.Direction * (rayHit.Value + 0.25f));
                IHitInfo hitInfo;
                if (MyAPIGateway.Physics.CastRay(hitPos, hitEnt.Intersection.To, out hitInfo, 15))
                {
                    var rotMatrix = Quaternion.CreateFromRotationMatrix(door.WorldMatrix);
                    var obb = new MyOrientedBoundingBoxD(door.PositionComp.WorldAABB.Center, door.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                    var sphere = new BoundingSphereD(hitInfo.Position + (hitEnt.Intersection.Direction * 0.15f), 0.01f);
                    if (obb.Intersects(ref sphere))
                        return true;
                }
            }
            return false;
        }
        public void GetBlockSphereDb(MyCubeGrid grid, double areaRadius, out List<Vector3I> radiatedBlocks)
        {
            areaRadius = Math.Ceiling(areaRadius);

            if (grid.GridSizeEnum == MyCubeSize.Large)
            {
                if (areaRadius < 3) areaRadius = 3;
                LargeBlockSphereDb.TryGetValue(areaRadius, out radiatedBlocks);
            }
            else SmallBlockSphereDb.TryGetValue(areaRadius, out radiatedBlocks);
        }

        private void GenerateBlockSphere(MyCubeSize gridSizeEnum, double radiusInMeters)
        {
            var gridSizeInv = 2.0; // Assume small grid (1 / 0.5)
            if (gridSizeEnum == MyCubeSize.Large)
                gridSizeInv = 0.4; // Large grid (1 / 2.5)

            var radiusInBlocks = radiusInMeters * gridSizeInv;
            var radiusSq = radiusInBlocks * radiusInBlocks;
            var radiusCeil = (int)Math.Ceiling(radiusInBlocks);
            int i, j, k;
            var max = Vector3I.One * radiusCeil;
            var min = Vector3I.One * -radiusCeil;

            var blockSphereLst = _blockSpherePool.Get();
            for (i = min.X; i <= max.X; ++i)
                for (j = min.Y; j <= max.Y; ++j)
                    for (k = min.Z; k <= max.Z; ++k)
                        if (i * i + j * j + k * k < radiusSq)
                            blockSphereLst.Add(new Vector3I(i, j, k));

            blockSphereLst.Sort((a, b) => Vector3I.Dot(a, a).CompareTo(Vector3I.Dot(b, b)));
            if (gridSizeEnum == MyCubeSize.Large)
                LargeBlockSphereDb.Add(radiusInMeters, blockSphereLst);
            else
                SmallBlockSphereDb.Add(radiusInMeters, blockSphereLst);
        }

        private void ShiftAndPruneBlockSphere(MyCubeGrid grid, Vector3I center, List<Vector3I> sphereOfCubes, List<RadiatedBlock> slims)
        {
            slims.Clear(); // Ugly but super inlined V3I check
            var gMinX = grid.Min.X;
            var gMinY = grid.Min.Y;
            var gMinZ = grid.Min.Z;
            var gMaxX = grid.Max.X;
            var gMaxY = grid.Max.Y;
            var gMaxZ = grid.Max.Z;

            for (int i = 0; i < sphereOfCubes.Count; i++)
            {
                var v3ICheck = center + sphereOfCubes[i];
                var contained = gMinX <= v3ICheck.X && v3ICheck.X <= gMaxX && (gMinY <= v3ICheck.Y && v3ICheck.Y <= gMaxY) && (gMinZ <= v3ICheck.Z && v3ICheck.Z <= gMaxZ);
                if (!contained) continue;

                MyCube cube;
                if (grid.TryGetCube(v3ICheck, out cube))
                {
                    IMySlimBlock slim = cube.CubeBlock;
                    if (slim.Position == v3ICheck)
                        slims.Add(new RadiatedBlock { Center = center, Slim = slim, Position = v3ICheck });
                }
            }
        }

        static void GetIntVectorsInSphere(MyCubeGrid grid, Vector3I center, double radius, List<RadiatedBlock> points)
        {
            points.Clear();
            radius *= grid.GridSizeR;
            double radiusSq = radius * radius;
            int radiusCeil = (int)Math.Ceiling(radius);
            int i, j, k;
            for (i = -radiusCeil; i <= radiusCeil; ++i)
            {
                for (j = -radiusCeil; j <= radiusCeil; ++j)
                {
                    for (k = -radiusCeil; k <= radiusCeil; ++k)
                    {
                        if (i * i + j * j + k * k < radiusSq)
                        {
                            var vector3I = center + new Vector3I(i, j, k);
                            IMySlimBlock slim = grid.GetCubeBlock(vector3I);

                            if (slim != null)
                            {
                                var radiatedBlock = new RadiatedBlock
                                {
                                    Center = center, Slim = slim, Position = vector3I
                                };
                                points.Add(radiatedBlock);
                            }
                        }
                    }
                }
            }
        }

        private void GetIntVectorsInSphere2(MyCubeGrid grid, Vector3I center, double radius)
        {
            SlimsSortedList.Clear();
            radius *= grid.GridSizeR;
            var gridMin = grid.Min;
            var gridMax = grid.Max;
            double radiusSq = radius * radius;
            int radiusCeil = (int)Math.Ceiling(radius);
            int i, j, k;
            Vector3I max = Vector3I.Min(Vector3I.One * radiusCeil, gridMax - center);
            Vector3I min = Vector3I.Max(Vector3I.One * -radiusCeil, gridMin - center);

            for (i = min.X; i <= max.X; ++i)
            {
                for (j = min.Y; j <= max.Y; ++j)
                {
                    for (k = min.Z; k <= max.Z; ++k)
                    {
                        if (i * i + j * j + k * k < radiusSq)
                        {
                            var vector3I = center + new Vector3I(i, j, k);
                            IMySlimBlock slim = grid.GetCubeBlock(vector3I);

                            if (slim != null && slim.Position == vector3I)
                            {
                                var radiatedBlock = new RadiatedBlock
                                {
                                    Center = center, Slim = slim, Position = vector3I
                                };
                                SlimsSortedList.Add(radiatedBlock);
                            }
                        }
                    }
                }
            }
            SlimsSortedList.Sort((a, b) => Vector3I.Dot(a.Position, a.Position).CompareTo(Vector3I.Dot(b.Position, b.Position)));
        }

        public void GetBlocksInsideSphere(MyCubeGrid grid, Dictionary<Vector3I, IMySlimBlock> cubes, ref BoundingSphereD sphere, bool sorted, Vector3I center, bool checkTriangles = false)
        {
            if (grid.PositionComp == null) return;

            if (sorted) SlimsSortedList.Clear();
            else _slimsSet.Clear();

            var matrixNormalizedInv = grid.PositionComp.WorldMatrixNormalizedInv;
            Vector3D result;
            Vector3D.Transform(ref sphere.Center, ref matrixNormalizedInv, out result);
            var localSphere = new BoundingSphere(result, (float)sphere.Radius);
            var fromSphere2 = BoundingBox.CreateFromSphere(localSphere);
            var min = (Vector3D)fromSphere2.Min;
            var max = (Vector3D)fromSphere2.Max;
            var vector3I1 = new Vector3I((int)Math.Round(min.X * grid.GridSizeR), (int)Math.Round(min.Y * grid.GridSizeR), (int)Math.Round(min.Z * grid.GridSizeR));
            var vector3I2 = new Vector3I((int)Math.Round(max.X * grid.GridSizeR), (int)Math.Round(max.Y * grid.GridSizeR), (int)Math.Round(max.Z * grid.GridSizeR));
            var start = Vector3I.Min(vector3I1, vector3I2);
            var end = Vector3I.Max(vector3I1, vector3I2);
            if ((end - start).Volume() < cubes.Count)
            {
                var vector3IRangeIterator = new Vector3I_RangeIterator(ref start, ref end);
                var next = vector3IRangeIterator.Current;
                while (vector3IRangeIterator.IsValid())
                {
                    IMySlimBlock cube;
                    if (cubes.TryGetValue(next, out cube))
                    {
                        if (new BoundingBox(cube.Min * grid.GridSize - grid.GridSizeHalf, cube.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                        {
                            var radiatedBlock = new RadiatedBlock
                            {
                                Center = center,
                                Slim = cube,
                                Position = cube.Position,
                            };
                            if (sorted) SlimsSortedList.Add(radiatedBlock);
                            else _slimsSet.Add(cube);
                        }
                    }
                    vector3IRangeIterator.GetNext(out next);
                }
            }
            else
            {
                foreach (var cube in cubes.Values)
                {
                    if (new BoundingBox(cube.Min * grid.GridSize - grid.GridSizeHalf, cube.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                    {
                        var radiatedBlock = new RadiatedBlock
                        {
                            Center = center,
                            Slim = cube,
                            Position = cube.Position,
                        };
                        if (sorted) SlimsSortedList.Add(radiatedBlock);
                        else _slimsSet.Add(cube);
                    }
                }
            }
            if (sorted)
                SlimsSortedList.Sort((x, y) => Vector3I.DistanceManhattan(x.Position, x.Slim.Position).CompareTo(Vector3I.DistanceManhattan(y.Position, y.Slim.Position)));
        }

        public static void GetBlocksInsideSphereFast(MyCubeGrid grid, ref BoundingSphereD sphere, bool checkDestroyed, List<IMySlimBlock> blocks)
        {
            var radius = sphere.Radius;
            radius *= grid.GridSizeR;
            var center = grid.WorldToGridInteger(sphere.Center);
            var gridMin = grid.Min;
            var gridMax = grid.Max;
            double radiusSq = radius * radius;
            int radiusCeil = (int)Math.Ceiling(radius);
            int i, j, k;
            Vector3I max2 = Vector3I.Min(Vector3I.One * radiusCeil, gridMax - center);
            Vector3I min2 = Vector3I.Max(Vector3I.One * -radiusCeil, gridMin - center);
            for (i = min2.X; i <= max2.X; ++i)
            {
                for (j = min2.Y; j <= max2.Y; ++j)
                {
                    for (k = min2.Z; k <= max2.Z; ++k)
                    {
                        if (i * i + j * j + k * k < radiusSq)
                        {
                            MyCube cube;
                            var vector3I = center + new Vector3I(i, j, k);

                            if (grid.TryGetCube(vector3I, out cube))
                            {
                                var slim = (IMySlimBlock)cube.CubeBlock;
                                if (slim.Position == vector3I)
                                {
                                    if (checkDestroyed && slim.IsDestroyed)
                                        continue;

                                    blocks.Add(slim);

                                }
                            }
                        }
                    }
                }
            }
        }

        public void GetBlocksInsideSphereBrute(MyCubeGrid grid, Vector3I center, ref BoundingSphereD sphere, bool sorted)
        {
            if (grid.PositionComp == null) return;

            if (sorted) SlimsSortedList.Clear();
            else _slimsSet.Clear();

            var matrixNormalizedInv = grid.PositionComp.WorldMatrixNormalizedInv;
            Vector3D result;
            Vector3D.Transform(ref sphere.Center, ref matrixNormalizedInv, out result);
            var localSphere = new BoundingSphere(result, (float)sphere.Radius);
            foreach (IMySlimBlock cube in grid.CubeBlocks)
            {
                if (new BoundingBox(cube.Min * grid.GridSize - grid.GridSizeHalf, cube.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                {
                    var radiatedBlock = new RadiatedBlock
                    {
                        Center = center,
                        Slim = cube,
                        Position = cube.Position,
                    };
                    if (sorted) SlimsSortedList.Add(radiatedBlock);
                    else _slimsSet.Add(cube);
                }
            }
            if (sorted)
                SlimsSortedList.Sort((x, y) => Vector3I.DistanceManhattan(x.Position, x.Slim.Position).CompareTo(Vector3I.DistanceManhattan(y.Position, y.Slim.Position)));
        }



        public static void GetExistingCubes(MyCubeGrid grid, Vector3I min, Vector3I max, Dictionary<Vector3I, IMySlimBlock> resultSet)
        {
            resultSet.Clear();
            Vector3I result1 = Vector3I.Floor((min - Vector3I.One) / 2f);
            Vector3I result2 = Vector3I.Ceiling((max - Vector3I.One) / 2f);
            var gridMin = grid.Min;
            var gridMax = grid.Max;
            Vector3I.Max(ref result1, ref gridMin, out result1);
            Vector3I.Min(ref result2, ref gridMax, out result2);
            Vector3I key;
            for (key.X = result1.X; key.X <= result2.X; ++key.X)
            {
                for (key.Y = result1.Y; key.Y <= result2.Y; ++key.Y)
                {
                    for (key.Z = result1.Z; key.Z <= result2.Z; ++key.Z)
                    {
                        MyCube myCube;
                        if (grid.TryGetCube(key, out myCube))
                        {
                            resultSet[key] = myCube.CubeBlock;
                        }
                    }
                }
            }
        }

        public static void GetExistingCubes(MyCubeGrid grid, Vector3I min, Vector3I max, List<IMySlimBlock> resultSet)
        {
            resultSet.Clear();
            Vector3I result1 = Vector3I.Floor((min - Vector3I.One) / 2f);
            Vector3I result2 = Vector3I.Ceiling((max - Vector3I.One) / 2f);
            var gridMin = grid.Min;
            var gridMax = grid.Max;
            Vector3I.Max(ref result1, ref gridMin, out result1);
            Vector3I.Min(ref result2, ref gridMax, out result2);
            Vector3I key;
            for (key.X = result1.X; key.X <= result2.X; ++key.X)
            {
                for (key.Y = result1.Y; key.Y <= result2.Y; ++key.Y)
                {
                    for (key.Z = result1.Z; key.Z <= result2.Z; ++key.Z)
                    {
                        MyCube myCube;
                        if (grid.TryGetCube(key, out myCube))
                        {
                            resultSet.Add(myCube.CubeBlock);
                        }
                    }
                }
            }
        }

        public static void GetExistingCubes(MyCubeGrid grid, Vector3I min, Vector3I max, BoundingSphere localSphere, bool checkDestroyed, List<IMySlimBlock> resultSet)
        {
            resultSet.Clear();
            Vector3I result1 = Vector3I.Floor((min - Vector3I.One) / 2f);
            Vector3I result2 = Vector3I.Ceiling((max - Vector3I.One) / 2f);
            var gridMin = grid.Min;
            var gridMax = grid.Max;
            Vector3I.Max(ref result1, ref gridMin, out result1);
            Vector3I.Min(ref result2, ref gridMax, out result2);
            Vector3I key;
            for (key.X = result1.X; key.X <= result2.X; ++key.X)
            {
                for (key.Y = result1.Y; key.Y <= result2.Y; ++key.Y)
                {
                    for (key.Z = result1.Z; key.Z <= result2.Z; ++key.Z)
                    {
                        MyCube myCube;
                        if (grid.TryGetCube(key, out myCube))
                        {
                            var block = (IMySlimBlock)myCube.CubeBlock;
                            if (checkDestroyed && block.IsDestroyed || !new BoundingBox(block.Min * grid.GridSize - grid.GridSizeHalf, block.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                                continue;

                            resultSet.Add(block);
                        }
                    }
                }
            }
        }
    }
}
