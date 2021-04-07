using System;
using System.Collections.Generic;
using Sandbox.Engine.Utils;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Settings;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.AreaDamageDef;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.DamageScaleDef;
using static WeaponCore.Support.WeaponSystem.TurretType;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType;
using static WeaponCore.Settings.CoreSettings.ServerSettings;
using VRage.Utils;

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
        private bool _shieldNull;
        internal void ProcessHits()
        {
            _shieldNull = false;
            for (int x = 0; x < Hits.Count; x++)
            {
                var p = Hits[x];
                var info = p.Info;
                var maxObjects = info.AmmoDef.Const.MaxObjectsHit;
                var phantom = info.AmmoDef.BaseDamage <= 0;
                var pInvalid = (int) p.State > 3;
                var tInvalid = info.Target.IsProjectile && (int)info.Target.Projectile.State > 1;
                if (tInvalid) info.Target.Reset(Tick, Target.States.ProjectileClosed);
                var skip = pInvalid || tInvalid;
                var canDamage = IsServer && (p.Info.ClientSent || !p.Info.AmmoDef.Const.ClientPredictedAmmo);
                /*
                if (!p.Info.IsShrapnel)
                    Log.Line($"ProcessHits: canDamage:{canDamage} - notPredicted:{!p.Info.AmmoDef.Const.ClientPredictedAmmo} - clientSent:{p.Info.ClientSent} - beam:{p.Info.AmmoDef.Const.IsBeamWeapon} - ammo:{p.Info.AmmoDef.AmmoRound}");

                if (canDamage && !p.Info.ClientSent)
                    Log.Line($"invalid damage: shrapnel:{p.Info.IsShrapnel} - ClientSent:{p.Info.ClientSent} - ClientPredictedAmmo:{p.Info.AmmoDef.Const.ClientPredictedAmmo} - beam:{p.Info.AmmoDef.Const.IsBeamWeapon} - ammo:{p.Info.AmmoDef.AmmoRound}");
                else if (canDamage)
                    Log.Line($"valid damage: shrapnel:{p.Info.IsShrapnel} - ClientSent:{p.Info.ClientSent} - ClientPredictedAmmo:{p.Info.AmmoDef.Const.ClientPredictedAmmo} - beam:{p.Info.AmmoDef.Const.IsBeamWeapon} - ammo:{p.Info.AmmoDef.AmmoRound}");
                */
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
            if (!info.ShieldBypassed)
                info.ObjectsHit++;

            AmmoModifer ammoModifer;
            AmmoDamageMap.TryGetValue(info.AmmoDef, out ammoModifer);
            var directDmgGlobal = ammoModifer == null ? Settings.Enforcement.DirectDamageModifer : Settings.Enforcement.DirectDamageModifer * ammoModifer.DirectDamageModifer;
            var areaDmgGlobal = ammoModifer == null ? Settings.Enforcement.AreaDamageModifer : Settings.Enforcement.AreaDamageModifer * ammoModifer.AreaDamageModifer;
            var detDmgGlobal = ammoModifer == null ? Settings.Enforcement.AreaDamageModifer : Settings.Enforcement.AreaDamageModifer * ammoModifer.DetonationDamageModifer;

            var damageScale = 1 * directDmgGlobal;
            var distTraveled = info.AmmoDef.Const.IsBeamWeapon ? hitEnt.HitDist ?? info.DistanceTraveled : info.DistanceTraveled;
            var fallOff = info.AmmoDef.Const.FallOffScaling && distTraveled > info.AmmoDef.DamageScales.FallOff.Distance;
            if (info.AmmoDef.Const.VirtualBeams) damageScale *= info.WeaponCache.Hits;
            var damageType = info.AmmoDef.DamageScales.Shields.Type;
            var heal = damageType == ShieldDef.ShieldType.Heal;
            var energy = damageType == ShieldDef.ShieldType.Energy || info.ShieldBypassed || heal;
            var areaEffect = info.AmmoDef.AreaEffect;
            var detonateOnEnd = info.AmmoDef.AreaEffect.Detonation.DetonateOnEnd && info.Age >= info.AmmoDef.AreaEffect.Detonation.MinArmingTime && areaEffect.AreaEffect != AreaEffectType.Disabled && !info.ShieldBypassed;
            var areaDamage = areaEffect.AreaEffect != AreaEffectType.Disabled ? (info.AmmoDef.Const.AreaEffectDamage * (info.AmmoDef.Const.AreaEffectSize * 0.5f)) * areaDmgGlobal : 0;
            var scaledBaseDamage = info.BaseDamagePool * damageScale;

            var scaledDamage = (scaledBaseDamage + areaDamage) * info.AmmoDef.Const.ShieldModifier;
            
            if (fallOff) {
                var fallOffMultipler = MathHelperD.Clamp(1.0 - ((distTraveled  - info.AmmoDef.DamageScales.FallOff.Distance) / (info.AmmoDef.Const.MaxTrajectory - info.AmmoDef.DamageScales.FallOff.Distance)), info.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
                scaledDamage *= fallOffMultipler;
            }

            scaledDamage = (scaledDamage * info.ShieldResistMod) * info.ShieldBypassMod;
            var unscaledDetDmg = areaEffect.AreaEffect == AreaEffectType.Radiant ? info.AmmoDef.Const.DetonationDamage : info.AmmoDef.Const.DetonationDamage * (info.AmmoDef.Const.DetonationRadius * 0.5f);
            var detonateDamage = detonateOnEnd ? (unscaledDetDmg * info.AmmoDef.Const.ShieldModifier * detDmgGlobal) * info.ShieldResistMod : 0;
            if (heal) {
                var heat = SApi.GetShieldHeat(shield);

                switch (heat)
                {
                    case 0:
                        scaledDamage *= -1;
                        detonateDamage *= -1;
                        break;
                    case 100:
                        scaledDamage = -0.01f;
                        detonateDamage = -0.01f;
                        break;
                    default:
                    {
                        var dec = heat / 100f;
                        var healFactor = 1 - dec;
                        scaledDamage *= healFactor;
                        scaledDamage *= -1;
                        detonateDamage *= healFactor;
                        detonateDamage *= -1;
                        break;
                    }
                }
            }
            var applyToShield = info.AmmoDef.AmmoGraphics.ShieldHitDraw && (!info.AmmoDef.AmmoGraphics.Particles.Hit.ApplyToShield || !info.AmmoDef.Const.HitParticle);
            var hit = SApi.PointAttackShieldCon(shield, hitEnt.HitPos.Value, info.Target.FiringCube.EntityId, (float)scaledDamage, (float)detonateDamage, energy, applyToShield);
            if (hit.HasValue) {

                if (heal) {
                    info.BaseDamagePool = 0;
                    return;
                }

                var objHp = hit.Value;


                if (info.EwarActive)
                    info.BaseHealthPool -= 1;
                else if (objHp > 0) {

                    if (!info.ShieldBypassed)
                        info.BaseDamagePool = 0;
                    else
                        info.BaseDamagePool -= (info.BaseDamagePool * info.ShieldResistMod) * info.ShieldBypassMod;
                }
                else info.BaseDamagePool = (objHp * -1);

                if (info.AmmoDef.Mass <= 0) return;

                var speed = info.AmmoDef.Trajectory.DesiredSpeed > 0 ? info.AmmoDef.Trajectory.DesiredSpeed : 1;
                if (Session.IsServer) ApplyProjectileForce((MyEntity)shield.CubeGrid, hitEnt.HitPos.Value, hitEnt.Intersection.Direction, info.AmmoDef.Mass * speed);
            }
            else if (!_shieldNull) {
                Log.Line($"DamageShield PointAttack returned null");
                _shieldNull = true;
            }
        }

        private void DamageGrid(HitEntity hitEnt, ProInfo t, bool canDamage)
        {
            var grid = hitEnt.Entity as MyCubeGrid;

            if (grid == null || grid.MarkedForClose || !hitEnt.HitPos.HasValue || hitEnt.Blocks == null) {
                hitEnt.Blocks?.Clear();
                return;
            }
            if (t.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Heal|| (!t.AmmoDef.Const.SelfDamage || !MyAPIGateway.Session.SessionSettings.EnableTurretsFriendlyFire) && t.Target.FiringCube.CubeGrid.IsSameConstructAs(grid) || !grid.DestructibleBlocks || grid.Immune || grid.GridGeneralDamageModifier <= 0)
            {
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
            var detonateOnEnd = t.AmmoDef.AreaEffect.Detonation.DetonateOnEnd && t.Age >= t.AmmoDef.AreaEffect.Detonation.MinArmingTime;
            var detonateDmg = t.AmmoDef.Const.DetonationDamage;

            var attackerId = t.Target.FiringCube.EntityId;
            var attacker = t.Target.FiringCube;
            
            var areaEffectDmg = areaEffect != AreaEffectType.Disabled ? t.AmmoDef.Const.AreaEffectDamage : 0;
            var hitMass = t.AmmoDef.Mass;
            var sync = MpActive && (DedicatedServer || IsServer);
            var hasAreaDmg = areaEffectDmg > 0;
            var radiantCascade = radiant && !detonateOnEnd;
            var primeDamage = !radiantCascade || !hasAreaDmg;
            var radiantBomb = radiant && detonateOnEnd;
            var damageType = t.ShieldBypassed ? ShieldBypassDamageType : explosive || radiant ? MyDamageType.Explosion : MyDamageType.Bullet;
            var minAoeOffset = largeGrid ? 1.25 : 0.5f;
            var gridMatrix = grid.PositionComp.WorldMatrixRef;
            AmmoModifer ammoModifer;
            AmmoDamageMap.TryGetValue(t.AmmoDef, out ammoModifer);
            var directDmgGlobal = ammoModifer == null ? Settings.Enforcement.DirectDamageModifer : Settings.Enforcement.DirectDamageModifer * ammoModifer.DirectDamageModifer;
            var areaDmgGlobal = ammoModifer == null ? Settings.Enforcement.AreaDamageModifer : Settings.Enforcement.AreaDamageModifer * ammoModifer.AreaDamageModifer;
            var detDmgGlobal = ammoModifer == null ? Settings.Enforcement.AreaDamageModifer : Settings.Enforcement.AreaDamageModifer * ammoModifer.DetonationDamageModifer;

            float gridDamageModifier = grid.GridGeneralDamageModifier;

            var distTraveled = t.AmmoDef.Const.IsBeamWeapon ? hitEnt.HitDist ?? t.DistanceTraveled: t.DistanceTraveled;

            var fallOff = t.AmmoDef.Const.FallOffScaling && distTraveled > t.AmmoDef.DamageScales.FallOff.Distance;
            var fallOffMultipler = 1f;
            if (fallOff)
            {
                fallOffMultipler = (float)MathHelperD.Clamp(1.0 - ((distTraveled - t.AmmoDef.DamageScales.FallOff.Distance) / (t.AmmoDef.Const.MaxTrajectory - t.AmmoDef.DamageScales.FallOff.Distance)), t.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
            }

            var damagePool = t.BaseDamagePool;
            int hits = 1;
            if (t.AmmoDef.Const.VirtualBeams)
            {
                hits = t.WeaponCache.Hits;
                areaEffectDmg *= hits;
            }
            
            var objectsHit = t.ObjectsHit;
            var countBlocksAsObjects = t.AmmoDef.ObjectsHit.CountBlocks;

            List<Vector3I> radiatedBlocks = null;
            if (radiant) GetBlockSphereDb(grid, areaRadius, out radiatedBlocks);

            var done = false;
            var nova = false;
            var outOfPew = false;
            IMySlimBlock rootBlock = null;
            var destroyed = 0;
            for (int i = 0; i < hitEnt.Blocks.Count; i++)
            {
                if (done || outOfPew && !nova) break;

                rootBlock = hitEnt.Blocks[i];

                if (!nova)
                {
                    if (_destroyedSlims.Contains(rootBlock) || _destroyedSlimsClient.Contains(rootBlock)) continue;
                    if (rootBlock.IsDestroyed)
                    {
                        destroyed++;
                        _destroyedSlims.Add(rootBlock);
                        if (IsClient)
                        {
                            _destroyedSlimsClient.Add(rootBlock);
                            _slimHealthClient.Remove(rootBlock);
                        }
                        continue;
                    }
                }

                var fatBlock = rootBlock.FatBlock as MyCubeBlock;
                var door = fatBlock as MyDoorBase;
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
                    var cubeBlockDef = (MyCubeBlockDefinition)block.BlockDefinition;
                    float cachedIntegrity;
                    var blockHp = !IsClient ? block.Integrity - block.AccumulatedDamage : (_slimHealthClient.TryGetValue(block, out cachedIntegrity) ? cachedIntegrity : block.Integrity);
                    var blockDmgModifier = cubeBlockDef.GeneralDamageMultiplier;
                    float damageScale = hits;
                    float directDamageScale = directDmgGlobal;
                    float areaDamageScale = areaDmgGlobal;
                    float detDamageScale = detDmgGlobal;

                    if (t.AmmoDef.Const.DamageScaling || !MyUtils.IsEqual(blockDmgModifier, 1f) || !MyUtils.IsEqual(gridDamageModifier, 1f))
                    {

                        if (blockDmgModifier < 0.000000001f || gridDamageModifier < 0.000000001f)
                            blockHp = float.MaxValue;
                        else
                            blockHp = (blockHp / blockDmgModifier / gridDamageModifier);

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
                            var isArmor = AllArmorBaseDefinitions.Contains(blockDef) || CustomArmorSubtypes.Contains(blockDef.Id.SubtypeId);
                            if (isArmor && d.Armor.Armor >= 0) damageScale *= d.Armor.Armor;
                            else if (!isArmor && d.Armor.NonArmor >= 0) damageScale *= d.Armor.NonArmor;

                            if (isArmor && (d.Armor.Light >= 0 || d.Armor.Heavy >= 0))
                            {
                                var isHeavy = HeavyArmorBaseDefinitions.Contains(blockDef) || CustomHeavyArmorSubtypes.Contains(blockDef.Id.SubtypeId);
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
                        if (GlobalDamageModifed)
                        {
                            if (blockDef == null) blockDef = block.BlockDefinition;
                            BlockDamage modifier;
                            var found = BlockDamageMap.TryGetValue(blockDef, out modifier);

                            if (found) {
                                directDamageScale *= modifier.DirectModifer;
                                areaDamageScale *= modifier.AreaModifer;
                            }
                        }

                        if (fallOff)
                            damageScale *= fallOffMultipler;
                    }

                    var blockIsRoot = block == rootBlock;
                    var primaryDamage = primeDamage || blockIsRoot;

                    if (damagePool <= 0 && primaryDamage || objectsHit >= maxObjects) {
                        outOfPew = true;
                        damagePool = 0;
                        break;
                    }

                    var scaledDamage = damagePool * damageScale * directDamageScale;
                    //Log.Line($"gridDamage:{damagePool}");

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
                            destroyed++;
                            _destroyedSlims.Add(block);
                            if (IsClient)
                            {
                                _destroyedSlimsClient.Add(block);
                                if (_slimHealthClient.ContainsKey(block))
                                    _slimHealthClient.Remove(block);
                            }
                            damagePool -= (blockHp / (damageScale * directDamageScale));
                        }
                    }
                    else
                    {
                        scaledDamage = (areaEffectDmg  * damageScale) * areaDamageScale;
                        if (scaledDamage >= blockHp)
                        {
                            destroyed++;
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
                        var realDmg = scaledDamage * gridDamageModifier * blockDmgModifier;
                        if (hasBlock && _slimHealthClient[block] - realDmg > 0)
                            _slimHealthClient[block] -= realDmg;
                        else if (hasBlock)
                            _slimHealthClient.Remove(block);
                        else if (block.Integrity - realDmg > 0)
                            _slimHealthClient[block] = blockHp - realDmg;
                    }

                    var theEnd = damagePool <= 0 || objectsHit >= maxObjects;

                    if (explosive && (!detonateOnEnd && blockIsRoot || detonateOnEnd && theEnd))
                    {
                        var travelOffset = hitEnt.Intersection.Length > minAoeOffset ? hitEnt.Intersection.Length : minAoeOffset;
                        var aoeOffset = Math.Min(areaRadius * 0.5f, travelOffset);
                        var expOffsetClamp = MathHelperD.Clamp(aoeOffset, minAoeOffset, 2f);
                        var blastCenter = hitEnt.HitPos.Value + (-hitEnt.Intersection.Direction * expOffsetClamp);
                        if ((areaEffectDmg * areaDamageScale) > 0) SUtils.CreateMissileExplosion(this, (areaEffectDmg  * damageScale) * areaDamageScale, areaRadius, blastCenter, hitEnt.Intersection.Direction, attacker, grid, t.AmmoDef, true);
                        if (detonateOnEnd && theEnd)
                         SUtils.CreateMissileExplosion(this, (detonateDmg * damageScale) * detDamageScale, detonateRadius, blastCenter, hitEnt.Intersection.Direction, attacker, grid, t.AmmoDef, true);
                    }
                    else if (!nova)
                    {
                        if (hitMass > 0 && blockIsRoot)
                        {
                            var speed = t.AmmoDef.Trajectory.DesiredSpeed > 0 ? t.AmmoDef.Trajectory.DesiredSpeed : 1;
                            if (Session.IsServer) ApplyProjectileForce(grid, grid.GridIntegerToWorld(rootBlock.Position), hitEnt.Intersection.Direction, (hitMass * speed));
                        }

                        if (radiantBomb && theEnd)
                        {
                            nova = true;
                            i--;
                            t.BaseDamagePool = 0;
                            t.ObjectsHit = objectsHit;
                            if (t.AmmoDef.Const.DetonationDamage > 0) damagePool = (detonateDmg * detDamageScale);
                            else if (t.AmmoDef.Const.AreaEffectDamage > 0) damagePool = (areaEffectDmg * areaDamageScale);
                            else damagePool = scaledDamage;
                            break;
                        }
                    }
                }
            }

            if (rootBlock != null && destroyed > 0)
            {
                var fat = rootBlock.FatBlock;
                MyOrientedBoundingBoxD obb;
                if (fat != null)
                    obb = new MyOrientedBoundingBoxD(fat.Model.BoundingBox, fat.PositionComp.WorldMatrixRef);
                else {
                    Vector3 halfExt;
                    rootBlock.ComputeScaledHalfExtents(out halfExt);
                    var blockBox = new BoundingBoxD(-halfExt, halfExt);
                    gridMatrix.Translation = grid.GridIntegerToWorld(grid.GridIntegerToWorld(rootBlock.Position));
                    obb = new MyOrientedBoundingBoxD(blockBox, gridMatrix);
                }

                var dist = obb.Intersects(ref hitEnt.Intersection);
                if (dist.HasValue)
                {
                    t.Hit.LastHit = hitEnt.Intersection.From + (hitEnt.Intersection.Direction * dist.Value);
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

            AmmoModifer ammoModifer;
            AmmoDamageMap.TryGetValue(info.AmmoDef, out ammoModifer);
            var directDmgGlobal = ammoModifer == null ? Settings.Enforcement.DirectDamageModifer : Settings.Enforcement.DirectDamageModifer * ammoModifer.DirectDamageModifer;
            var areaDmgGlobal = ammoModifer == null ? Settings.Enforcement.AreaDamageModifer : Settings.Enforcement.AreaDamageModifer * ammoModifer.AreaDamageModifer;

            var shieldHeal = info.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Heal;
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

            var areaEffect = info.AmmoDef.AreaEffect;
            var areaDamage = areaEffect.AreaEffect != AreaEffectType.Disabled ? (info.AmmoDef.Const.AreaEffectDamage * (info.AmmoDef.Const.AreaEffectSize * 0.5f)) * areaDmgGlobal : 0;
            var scaledDamage = (float)((((info.BaseDamagePool * damageScale) * directDmgGlobal) + areaDamage) * info.ShieldResistMod);

            var distTraveled = info.AmmoDef.Const.IsBeamWeapon ? hitEnt.HitDist ?? info.DistanceTraveled : info.DistanceTraveled;

            var fallOff = info.AmmoDef.Const.FallOffScaling && distTraveled > info.AmmoDef.DamageScales.FallOff.Distance;
            if (fallOff)
            {
                var fallOffMultipler = (float)MathHelperD.Clamp(1.0 - ((distTraveled - info.AmmoDef.DamageScales.FallOff.Distance) / (info.AmmoDef.Const.MaxTrajectory - info.AmmoDef.DamageScales.FallOff.Distance)), info.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
                scaledDamage *= fallOffMultipler;
            }

            if (scaledDamage < objHp) info.BaseDamagePool = 0;
            else info.BaseDamagePool -= objHp;

            if(canDamage)
                destObj.DoDamage(scaledDamage, !info.ShieldBypassed ? MyDamageType.Bullet : MyDamageType.Drill, sync, null, attackerId);
            if (info.AmmoDef.Mass > 0)
            {
                var speed = info.AmmoDef.Trajectory.DesiredSpeed > 0 ? info.AmmoDef.Trajectory.DesiredSpeed : 1;
                if (Session.IsServer) ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, hitEnt.Intersection.Direction, (info.AmmoDef.Mass * speed));
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

            var damageScale = (float)attacker.AmmoDef.Const.HealthHitModifier;
            if (attacker.AmmoDef.Const.VirtualBeams) damageScale *= attacker.WeaponCache.Hits;
            var scaledDamage = 1 * damageScale;

            var distTraveled = attacker.AmmoDef.Const.IsBeamWeapon ? hitEnt.HitDist ?? attacker.DistanceTraveled : attacker.DistanceTraveled;

            var fallOff = attacker.AmmoDef.Const.FallOffScaling && distTraveled > attacker.AmmoDef.DamageScales.FallOff.Distance;
            if (fallOff) {
                var fallOffMultipler = (float)MathHelperD.Clamp(1.0 - ((distTraveled - attacker.AmmoDef.DamageScales.FallOff.Distance) / (attacker.AmmoDef.Const.MaxTrajectory - attacker.AmmoDef.DamageScales.FallOff.Distance)), attacker.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
                scaledDamage *= fallOffMultipler;
            }

            if (scaledDamage >= objHp) {

                var safeObjHp = objHp <= 0 ? 0.0000001f : objHp;
                var remaining = (scaledDamage / safeObjHp) / damageScale;
                attacker.BaseDamagePool -= remaining;
                pTarget.Info.BaseHealthPool = 0;
                pTarget.State = Projectile.ProjectileState.Destroy;
                if (attacker.AmmoDef.Const.DetonationDamage > 0 && attacker.AmmoDef.AreaEffect.Detonation.DetonateOnEnd && attacker.Age >= attacker.AmmoDef.AreaEffect.Detonation.MinArmingTime)
                    DetonateProjectile(hitEnt, attacker);
            }
            else {
                attacker.BaseDamagePool = 0;
                pTarget.Info.BaseHealthPool -= scaledDamage;
                DetonateProjectile(hitEnt, attacker);
            }
        }

        private static void DetonateProjectile(HitEntity hitEnt, ProInfo attacker)
        {
            if (attacker.AmmoDef.Const.DetonationDamage > 0 && attacker.AmmoDef.AreaEffect.Detonation.DetonateOnEnd && attacker.Age >= attacker.AmmoDef.AreaEffect.Detonation.MinArmingTime)
            {
                var areaSphere = new BoundingSphereD(hitEnt.Projectile.Position, attacker.AmmoDef.Const.DetonationRadius);
                foreach (var sTarget in attacker.Ai.LiveProjectile) {

                    if (areaSphere.Contains(sTarget.Position) != ContainmentType.Disjoint) {

                        var objHp = sTarget.Info.BaseHealthPool;
                        var integrityCheck = attacker.AmmoDef.DamageScales.MaxIntegrity > 0;
                        if (integrityCheck && objHp > attacker.AmmoDef.DamageScales.MaxIntegrity) continue;

                        var damageScale = (float)attacker.AmmoDef.Const.HealthHitModifier;
                        if (attacker.AmmoDef.Const.VirtualBeams) damageScale *= attacker.WeaponCache.Hits;
                        var scaledDamage = 1 * damageScale;

                        if (scaledDamage >= objHp) {
                            sTarget.Info.BaseHealthPool = 0;
                            sTarget.State = Projectile.ProjectileState.Destroy;
                        }
                        else sTarget.Info.BaseHealthPool -= attacker.AmmoDef.Health;
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

            AmmoModifer ammoModifer;
            AmmoDamageMap.TryGetValue(info.AmmoDef, out ammoModifer);
            var directDmgGlobal = ammoModifer == null ? Settings.Enforcement.DirectDamageModifer : Settings.Enforcement.DirectDamageModifer * ammoModifer.DirectDamageModifer;
            var detDmgGlobal = ammoModifer == null ? Settings.Enforcement.AreaDamageModifer : Settings.Enforcement.AreaDamageModifer * ammoModifer.DetonationDamageModifer;

            using (destObj.Pin())
            {
                var detonateOnEnd = info.AmmoDef.Const.AmmoAreaEffect && info.AmmoDef.AreaEffect.Detonation.DetonateOnEnd && info.Age >= info.AmmoDef.AreaEffect.Detonation.MinArmingTime && info.AmmoDef.AreaEffect.AreaEffect != AreaEffectType.Radiant;

                info.ObjectsHit++;
                float damageScale = 1 * directDmgGlobal;
                if (info.AmmoDef.Const.VirtualBeams) damageScale *= info.WeaponCache.Hits;

                var scaledDamage = info.BaseDamagePool * damageScale;

                var distTraveled = info.AmmoDef.Const.IsBeamWeapon ? hitEnt.HitDist ?? info.DistanceTraveled : info.DistanceTraveled;
                var fallOff = info.AmmoDef.Const.FallOffScaling && distTraveled > info.AmmoDef.DamageScales.FallOff.Distance;
                
                if (fallOff) {
                    var fallOffMultipler = (float)MathHelperD.Clamp(1.0 - ((distTraveled - info.AmmoDef.DamageScales.FallOff.Distance) / (info.AmmoDef.Const.MaxTrajectory - info.AmmoDef.DamageScales.FallOff.Distance)), info.AmmoDef.DamageScales.FallOff.MinMultipler, 1);
                    scaledDamage *= fallOffMultipler;
                }

                var oRadius = info.AmmoDef.Const.AreaEffectSize;
                var minTestRadius = distTraveled - info.PrevDistanceTraveled;
                var tRadius = oRadius < minTestRadius && !info.AmmoDef.Const.IsBeamWeapon ? minTestRadius : oRadius;
                var objHp = (int)MathHelper.Clamp(MathFuncs.VolumeCube(MathFuncs.LargestCubeInSphere(tRadius)), 5000, double.MaxValue);


                if (tRadius > 5) objHp *= 5;

                if (scaledDamage < objHp) {
                    var reduceBy = objHp / scaledDamage;
                    oRadius /= reduceBy;
                    if (oRadius < 1) oRadius = 1;

                    info.BaseDamagePool = 0;
                }
                else {
                    info.BaseDamagePool -= objHp;
                    if (oRadius < minTestRadius) oRadius = minTestRadius;
                }
                destObj.PerformCutOutSphereFast(hitEnt.HitPos.Value, (float)(oRadius * info.AmmoDef.Const.VoxelHitModifier), false);

                if (detonateOnEnd && info.BaseDamagePool <= 0)
                {
                    var dRadius = info.AmmoDef.Const.DetonationRadius;
                    var dDamage = info.AmmoDef.Const.DetonationDamage * detDmgGlobal;

                    if (dRadius < 1.5) dRadius = 1.5f;

                    if (canDamage)
                        SUtils.CreateMissileExplosion(this, dDamage, dRadius, hitEnt.HitPos.Value, hitEnt.Intersection.Direction, info.Target.FiringCube, destObj, info.AmmoDef, true);
                }
            }
        }

        public static void ApplyProjectileForce(MyEntity entity, Vector3D intersectionPosition, Vector3 normalizedDirection, float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic || entity.Physics.Mass / impulse > 500)
                return;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero);
        }

        internal static void ApplyDeformationCubeGrid(LineD intersect, Vector3D hitPosition, MyCubeGrid grid, float damage, float hitMass)
        {
            /*
            var matrix = grid.PositionComp.WorldMatrixNormalizedInv;
            var vector3D1 = Vector3D.Transform(hitPosition, matrix);
            var vector3D2 = Vector3D.TransformNormal(intersect.Direction, matrix);
            float deformationOffset = 0.00664f * hitMass;
            float num1 = 0.011904f * damage;
            float num2 = 0.008928f * damage;
            float softAreaPlanar = MathHelper.Clamp(num1, grid.GridSize * 0.75f, grid.GridSize * 1.3f);
            float softAreaVertical = MathHelper.Clamp(num2, grid.GridSize * 0.9f, grid.GridSize * 1.3f);
            //grid.Physics.ApplyDeformation(deformationOffset, softAreaPlanar, softAreaVertical, vector3D1, vector3D2, MyDamageType.Bullet, 0.0f, 0.0f, 0L);
            */
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
                    var obb = new MyOrientedBoundingBoxD(door.PositionComp.LocalAABB, door.PositionComp.WorldMatrixRef);

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
