﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.AreaDamageDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.AreaDamageDef.EwarFieldsDef.PushPullDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.AreaDamageDef.AreaEffectType;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.DamageScaleDef;
using static CoreSystems.Projectiles.Projectiles;

namespace CoreSystems
{
    public partial class Session
    {
        private readonly Dictionary<MyCubeGrid, Dictionary<AreaEffectType, GridEffect>> _gridEffects = new Dictionary<MyCubeGrid, Dictionary<AreaEffectType, GridEffect>>(128);
        internal readonly MyConcurrentPool<Dictionary<AreaEffectType, GridEffect>> GridEffectsPool = new MyConcurrentPool<Dictionary<AreaEffectType, GridEffect>>(128, effect => effect.Clear());
        internal readonly MyConcurrentPool<GridEffect> GridEffectPool = new MyConcurrentPool<GridEffect>(128, effect => effect.Clean());
        internal readonly Dictionary<long, BlockState> EffectedCubes = new Dictionary<long, BlockState>();
        internal readonly Dictionary<long, EwarValues> CurrentClientEwaredCubes = new Dictionary<long, EwarValues>();
        internal readonly Dictionary<long, EwarValues> DirtyEwarData = new Dictionary<long, EwarValues>();
        private readonly CachingDictionary<long, BlockState> _activeEwarCubes = new CachingDictionary<long, BlockState>();
        private readonly Queue<long> _effectPurge = new Queue<long>();
        internal bool ClientEwarStale;

        private static void PushPull(HitEntity hitEnt, ProInfo info)
        {
            var depletable = info.AmmoDef.AreaEffect.EwarFields.Depletable;
            var healthPool = depletable && info.BaseHealthPool > 0 ? info.BaseHealthPool : float.MaxValue;
            if (healthPool <= 0) return;

            if (hitEnt.Entity.Physics == null || !hitEnt.Entity.Physics.Enabled || hitEnt.Entity.Physics.IsStatic || !hitEnt.HitPos.HasValue)
                return;

            var forceDef = info.AmmoDef.AreaEffect.EwarFields.Force;

            Vector3D forceFrom = Vector3D.Zero;
            Vector3D forceTo = Vector3D.Zero;
            Vector3D forcePosition = Vector3D.Zero;

            if (forceDef.ForceFrom == Force.ProjectileLastPosition) forceFrom = hitEnt.Intersection.From;
            else if (forceDef.ForceFrom == Force.ProjectileOrigin) forceFrom = info.Origin;
            else if (forceDef.ForceFrom == Force.HitPosition) forceFrom = hitEnt.HitPos.Value;
            else if (forceDef.ForceFrom == Force.TargetCenter) forceFrom = hitEnt.Entity.PositionComp.WorldAABB.Center;
            else if (forceDef.ForceFrom == Force.TargetCenterOfMass) forceFrom = hitEnt.Entity.Physics.CenterOfMassWorld;

            if (forceDef.ForceTo == Force.ProjectileLastPosition) forceTo = hitEnt.Intersection.From;
            else if (forceDef.ForceTo == Force.ProjectileOrigin) forceTo = info.Origin;
            else if (forceDef.ForceTo == Force.HitPosition) forceTo = hitEnt.HitPos.Value;
            else if (forceDef.ForceTo == Force.TargetCenter) forceTo = hitEnt.Entity.PositionComp.WorldAABB.Center;
            else if (forceDef.ForceTo == Force.TargetCenterOfMass) forceTo = hitEnt.Entity.Physics.CenterOfMassWorld;

            if (forceDef.Position == Force.ProjectileLastPosition) forcePosition = hitEnt.Intersection.From;
            else if (forceDef.Position == Force.ProjectileOrigin) forcePosition = info.Origin;
            else if (forceDef.Position == Force.HitPosition) forcePosition = hitEnt.HitPos.Value;
            else if (forceDef.Position == Force.TargetCenter) forcePosition = hitEnt.Entity.PositionComp.WorldAABB.Center;
            else if (forceDef.Position == Force.TargetCenterOfMass) forcePosition = hitEnt.Entity.Physics.CenterOfMassWorld;

            var hitDir = forceTo - forceFrom;

            Vector3D normHitDir;
            Vector3D.Normalize(ref hitDir, out normHitDir);

            normHitDir = info.AmmoDef.Const.AreaEffect == PushField ? normHitDir : -normHitDir;
            if (info.System.Session.IsServer)
                hitEnt.Entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normHitDir * (info.AmmoDef.Override.AreaEffectDamage * hitEnt.Entity.Physics.Mass), forcePosition, Vector3.Zero);

            if (depletable)
                info.BaseHealthPool -= healthPool;
        }

        private void UpdateField(HitEntity hitEnt, ProInfo info)
        {
            if (info.AmmoDef.Const.AreaEffect == PullField || info.AmmoDef.Const.AreaEffect == PushField)
            {
                PushPull(hitEnt, info);
                return;
            }

            var grid = hitEnt.Entity as MyCubeGrid;
            if (grid?.Physics == null || grid.MarkedForClose) return;

            var attackerId = info.Target.CoreEntity.EntityId;
            GetAndSortBlocksInSphere(info.AmmoDef, hitEnt.Info.System, grid, hitEnt.PruneSphere, !hitEnt.DamageOverTime, hitEnt.Blocks);

            var depletable = info.AmmoDef.AreaEffect.EwarFields.Depletable;
            var healthPool = depletable && info.BaseHealthPool > 0 ? info.BaseHealthPool : float.MaxValue;
            ComputeEffects(grid, info.AmmoDef, info.AmmoDef.Override.AreaEffectDamage, ref healthPool, attackerId, info.System.WeaponIdHash, hitEnt.Blocks);

            if (depletable)
                info.BaseHealthPool -= healthPool;
        }

        private void UpdateEffect(HitEntity hitEnt, ProInfo info)
        {
            if (info.AmmoDef.Const.AreaEffect == PullField || info.AmmoDef.Const.AreaEffect == PushField)
            {
                PushPull(hitEnt, info);
                return;
            }

            var grid = hitEnt.Entity as MyCubeGrid;
            if (grid == null || grid.MarkedForClose) return;

            if (IsServer)
            {

                Dictionary<AreaEffectType, GridEffect> effects;
                var attackerId = info.Target.CoreEntity.EntityId;
                if (_gridEffects.TryGetValue(grid, out effects))
                {
                    GridEffect gridEffect;
                    if (effects.TryGetValue(info.AmmoDef.AreaEffect.AreaEffect, out gridEffect))
                    {
                        gridEffect.Damage += info.AmmoDef.Override.AreaEffectDamage;
                        gridEffect.Ai = info.Ai;
                        gridEffect.AttackerId = attackerId;
                        gridEffect.Hits++;
                        var hitPos = hitEnt.HitPos ?? info.Hit.SurfaceHit;
                        gridEffect.HitPos = (gridEffect.HitPos + hitPos) / 2;

                    }
                }
                else
                {

                    effects = GridEffectsPool.Get();
                    var gridEffect = GridEffectPool.Get();
                    gridEffect.System = info.System;
                    gridEffect.Damage = info.AmmoDef.Override.AreaEffectDamage;
                    gridEffect.Ai = info.Ai;
                    gridEffect.AmmoDef = info.AmmoDef;
                    gridEffect.AttackerId = attackerId;
                    gridEffect.Hits++;
                    var hitPos = hitEnt.HitPos ?? info.Hit.SurfaceHit;

                    gridEffect.HitPos = hitPos;
                    effects.Add(info.AmmoDef.AreaEffect.AreaEffect, gridEffect);
                    _gridEffects.Add(grid, effects);
                }
            }

            info.BaseHealthPool = 0;
            info.BaseDamagePool = 0;
        }


        private void ComputeEffects(MyCubeGrid grid, AmmoDef ammoDef, float damagePool, ref float healthPool, long attackerId, int sysmteId, List<IMySlimBlock> blocks)
        {
            var largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
            var eWarInfo = ammoDef.AreaEffect.EwarFields;
            var duration = (uint)eWarInfo.Duration;
            var stack = eWarInfo.StackDuration;
            var maxStack = eWarInfo.MaxStacks;
            var nextTick = Tick + 1;
            var maxTick = stack ? (uint)(nextTick + (duration * maxStack)) : nextTick + duration;
            var fieldType = ammoDef.AreaEffect.AreaEffect;
            var sync = MpActive && (DedicatedServer || IsServer);
            foreach (var block in blocks)
            {
                var cubeBlock = block.FatBlock as MyCubeBlock;
                if (damagePool <= 0 || healthPool <= 0) break;

                IMyFunctionalBlock funcBlock = null;
                if (fieldType != DotField)
                {

                    if (cubeBlock == null || cubeBlock.MarkedForClose)
                        continue;

                    funcBlock = cubeBlock as IMyFunctionalBlock;
                    var isConveyor = cubeBlock is MyConveyor;
                    var ewared = EffectedCubes.ContainsKey(cubeBlock.EntityId);

                    if (funcBlock == null || isConveyor || !cubeBlock.IsWorking && !ewared || ewared && !stack) continue;
                }

                var blockHp = block.Integrity;
                float damageScale = 1;
                var tmpDamagePool = damagePool;
                if (ammoDef.Const.DamageScaling)
                {
                    var d = ammoDef.DamageScales;
                    if (d.MaxIntegrity > 0 && blockHp > d.MaxIntegrity) continue;

                    if (d.Grids.Large >= 0 && largeGrid) damageScale *= d.Grids.Large;
                    else if (d.Grids.Small >= 0 && !largeGrid) damageScale *= d.Grids.Small;

                    MyDefinitionBase blockDef = null;
                    if (ammoDef.Const.ArmorScaling)
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
                    if (ammoDef.Const.CustomDamageScales)
                    {
                        if (blockDef == null) blockDef = block.BlockDefinition;
                        float modifier;
                        var found = ammoDef.Const.CustomBlockDefinitionBasesToScales.TryGetValue(blockDef, out modifier);

                        if (found) damageScale *= modifier;
                        else if (ammoDef.DamageScales.Custom.IgnoreAllOthers) continue;
                    }
                }

                var scaledDamage = tmpDamagePool * damageScale;

                var blockDisabled = false;
                if (scaledDamage <= blockHp)
                    tmpDamagePool = 0;
                else
                {
                    blockDisabled = true;
                    tmpDamagePool -= blockHp;
                }

                if (fieldType == DotField && IsServer)
                {
                    block.DoDamage(scaledDamage, MyDamageType.Explosion, sync, null, attackerId);
                    continue;
                }

                if (funcBlock != null)
                {
                    BlockState blockState;
                    var cubeId = cubeBlock.EntityId;
                    if (EffectedCubes.TryGetValue(cubeId, out blockState))
                    {
                        if (blockState.Health > 0) damagePool = tmpDamagePool;
                        if (!blockDisabled && blockState.Health - scaledDamage > 0)
                        {
                            blockState.Health -= scaledDamage;
                            blockState.Endtick = Tick + (duration + 1);
                        }
                        else if (blockState.Endtick + (duration + 1) < maxTick)
                        {
                            blockState.Health = 0;
                            healthPool -= 1;
                            blockState.Endtick += (duration + 1);
                        }
                        else
                        {
                            blockState.Health = 0;
                            healthPool -= 1;
                            blockState.Endtick = maxTick;
                        }
                    }
                    else
                    {
                        damagePool = tmpDamagePool;
                        blockState.FunctBlock = funcBlock;
                        var originState = blockState.FunctBlock.Enabled;
                        blockState.FirstTick = Tick + 1;
                        blockState.FirstState = originState;
                        blockState.NextTick = nextTick;
                        blockState.Endtick = Tick + (duration + 1);
                        blockState.Session = this;
                        blockState.AmmoDef = ammoDef;
                        blockState.SystemId = sysmteId;
                        if (!blockDisabled) blockState.Health = blockHp - scaledDamage;
                        else
                        {
                            blockState.Health = 0;
                        }
                    }
                    EffectedCubes[cubeId] = blockState;
                }
            }

            if (!IsServer)
                EffectedCubes.Clear();
        }

        internal void GridEffects()
        {
            foreach (var ge in _gridEffects)
            {
                foreach (var v in ge.Value)
                {
                    GetCubesForEffect(v.Value.Ai, ge.Key, v.Value.HitPos, v.Key, _tmpEffectCubes);
                    var healthPool = v.Value.AmmoDef.Override.Health;
                    ComputeEffects(ge.Key, v.Value.AmmoDef, v.Value.Damage * v.Value.Hits, ref healthPool, v.Value.AttackerId, v.Value.System.WeaponIdHash, _tmpEffectCubes);
                    _tmpEffectCubes.Clear();
                    GridEffectPool.Return(v.Value);
                }
                GridEffectsPool.Return(ge.Value);
            }
            _gridEffects.Clear();
        }

        internal void ApplyGridEffect()
        {
            var tick = Tick;
            foreach (var item in EffectedCubes)
            {
                var cubeid = item.Key;
                var blockInfo = item.Value;
                var functBlock = blockInfo.FunctBlock;
                var health = blockInfo.Health;
                if (functBlock == null || functBlock.MarkedForClose)
                {
                    _effectPurge.Enqueue(cubeid);
                    continue;
                }

                if (health <= 0)
                {

                    if (functBlock.IsWorking)
                    {

                        functBlock.Enabled = false;
                        functBlock.EnabledChanged += ForceDisable;

                        if (MpActive && IsServer)
                        {
                            var ewarData = EwarDataPool.Get();
                            ewarData.FiringBlockId = blockInfo.FiringBlockId;
                            ewarData.EwaredBlockId = cubeid;
                            ewarData.EndTick = blockInfo.Endtick - Tick;
                            ewarData.AmmoId = blockInfo.AmmoDef.Const.AmmoIdxPos;
                            ewarData.SystemId = blockInfo.SystemId;
                            DirtyEwarData.Add(cubeid, ewarData);
                            EwarNetDataDirty = true;
                        }

                        if (IsHost)
                        {
                            functBlock.AppendingCustomInfo += blockInfo.AppendCustomInfo;
                            functBlock.RefreshCustomInfo();

                            if (!blockInfo.AmmoDef.AreaEffect.EwarFields.DisableParticleEffect)
                                functBlock.SetDamageEffect(true);
                        }
                    }
                }

                if (IsHost && Tick60 && HandlesInput && LastTerminal == functBlock)
                    functBlock.RefreshCustomInfo();

                if (tick >= blockInfo.Endtick)
                {

                    functBlock.EnabledChanged -= ForceDisable;

                    if (IsHost)
                    {

                        functBlock.AppendingCustomInfo -= blockInfo.AppendCustomInfo;
                        functBlock.RefreshCustomInfo();

                        if (!blockInfo.AmmoDef.AreaEffect.EwarFields.DisableParticleEffect)
                            functBlock.SetDamageEffect(false);
                    }

                    functBlock.Enabled = blockInfo.FirstState;

                    _effectPurge.Enqueue(cubeid);
                }

            }

            while (_effectPurge.Count != 0)
            {
                var queue = _effectPurge.Dequeue();

                if (MpActive && IsServer)
                {

                    EwarValues ewarValue;
                    if (DirtyEwarData.TryGetValue(queue, out ewarValue))
                        EwarDataPool.Return(ewarValue);

                    EwarNetDataDirty = true;
                }

                EffectedCubes.Remove(queue);
            }
        }

        internal void SyncClientEwarBlocks()
        {
            foreach (var ewarPair in CurrentClientEwaredCubes)
            {
                BlockState state;
                MyEntity ent;
                var entId = ewarPair.Key;
                if (MyEntities.TryGetEntityById(entId, out ent))
                {

                    var cube = (MyCubeBlock)ent;
                    var func = (IMyFunctionalBlock)cube;
                    func.RefreshCustomInfo();

                    if (!_activeEwarCubes.ContainsKey(entId))
                    {

                        state = new BlockState { FunctBlock = func, FirstState = func.Enabled, Endtick = Tick + ewarPair.Value.EndTick, Session = this };
                        _activeEwarCubes[entId] = state;
                        ActivateClientEwarState(ref state);
                    }
                }
                else if (_activeEwarCubes.TryGetValue(entId, out state))
                {

                    DeactivateClientEwarState(ref state);
                    _activeEwarCubes.Remove(entId);
                }

                ClientEwarStale = false;
            }

            _activeEwarCubes.ApplyChanges();
            foreach (var activeEwar in _activeEwarCubes)
            {

                if (!CurrentClientEwaredCubes.ContainsKey(activeEwar.Key))
                {
                    var state = activeEwar.Value;
                    DeactivateClientEwarState(ref state);
                    _activeEwarCubes.Remove(activeEwar.Key);
                }
            }
            _activeEwarCubes.ApplyRemovals();
        }

        private static void ActivateClientEwarState(ref BlockState state)
        {
            var functBlock = state.FunctBlock;
            functBlock.Enabled = false;
            functBlock.EnabledChanged += ForceDisable;
            functBlock.AppendingCustomInfo += state.AppendCustomInfo;
            functBlock.RefreshCustomInfo();
            functBlock.SetDamageEffect(true);
        }

        private static void DeactivateClientEwarState(ref BlockState state)
        {
            state.FunctBlock.EnabledChanged -= ForceDisable;
            state.FunctBlock.Enabled = state.FirstState;
            state.Endtick = 0;
            state.FunctBlock.RefreshCustomInfo();
            state.FunctBlock.AppendingCustomInfo -= state.AppendCustomInfo;
            state.FunctBlock.RefreshCustomInfo();

            state.FunctBlock.SetDamageEffect(false);
        }

        private static void ForceDisable(IMyTerminalBlock myTerminalBlock)
        {
            var cube = (MyCubeBlock)myTerminalBlock;
            if (cube == null || myTerminalBlock?.SlimBlock == null || myTerminalBlock.SlimBlock.IsDestroyed || cube.MarkedForClose || cube.Closed || cube.CubeGrid.MarkedForClose || !cube.IsFunctional || !cube.InScene) // keen is failing to check for null when they null out functional block types
                return;

            ((IMyFunctionalBlock)myTerminalBlock).Enabled = false;
        }


        private readonly List<IMySlimBlock> _tmpEffectCubes = new List<IMySlimBlock>();
        internal static void GetCubesForEffect(Ai ai, MyCubeGrid grid, Vector3D hitPos, AreaEffectType effectType, List<IMySlimBlock> cubes)
        {
            var fats = QueryBlockCaches(ai, grid, effectType);
            if (fats == null) return;

            for (int i = 0; i < fats.Count; i++) cubes.Add(fats[i].SlimBlock);

            cubes.Sort((a, b) =>
            {
                var aPos = grid.GridIntegerToWorld(a.Position);
                var bPos = grid.GridIntegerToWorld(b.Position);
                return Vector3D.DistanceSquared(aPos, hitPos).CompareTo(Vector3D.DistanceSquared(bPos, hitPos));
            });
        }

        private static ConcurrentCachingList<MyCubeBlock> QueryBlockCaches(Ai ai, MyCubeGrid targetGrid, AreaEffectType effectType)
        {
            ConcurrentDictionary<TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> blockTypeMap;
            if (!ai.Session.GridToBlockTypeMap.TryGetValue(targetGrid, out blockTypeMap)) return null;

            ConcurrentCachingList<MyCubeBlock> cubes;
            switch (effectType)
            {
                case JumpNullField:
                    if (blockTypeMap.TryGetValue(TargetingDef.BlockTypes.Jumping, out cubes))
                        return cubes;
                    break;
                case EnergySinkField:
                    if (blockTypeMap.TryGetValue(TargetingDef.BlockTypes.Power, out cubes))
                        return cubes;
                    break;
                case AnchorField:
                    if (blockTypeMap.TryGetValue(TargetingDef.BlockTypes.Thrust, out cubes))
                        return cubes;
                    break;
                case NavField:
                    if (blockTypeMap.TryGetValue(TargetingDef.BlockTypes.Steering, out cubes))
                        return cubes;
                    break;
                case OffenseField:
                    if (blockTypeMap.TryGetValue(TargetingDef.BlockTypes.Offense, out cubes))
                        return cubes;
                    break;
                case EmpField:
                case DotField:
                    GridMap gridMap;
                    if (ai.Session.GridToInfoMap.TryGetValue(targetGrid, out gridMap))
                        return gridMap.MyCubeBocks;
                    break;
            }

            return null;
        }
    }

    internal struct BlockState
    {
        public Session Session;
        public AmmoDef AmmoDef;
        public IMyFunctionalBlock FunctBlock;
        public bool FirstState;
        public uint FirstTick;
        public uint NextTick;
        public uint Endtick;
        public float Health;
        public long FiringBlockId;
        public int SystemId;

        internal void AppendCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var seconds = (Endtick - Session.Tick) / 60;
            if (Endtick > Session.Tick && seconds > 0)
                stringBuilder.Append($"\n*****************************************\nDisabled due to Electronic Warfare!\nRebooting in {seconds} seconds");
            else stringBuilder.Clear();
        }
    }

    internal class GridEffect
    {
        internal Vector3D HitPos;
        internal WeaponSystem System;
        internal Ai Ai;
        internal AmmoDef AmmoDef;
        internal long AttackerId;
        internal float Damage;
        internal int Hits;

        internal void Clean()
        {
            System = null;
            HitPos = Vector3D.Zero;
            Ai = null;
            AmmoDef = null;
            AttackerId = 0;
            Damage = 0;
            Hits = 0;
        }
    }
}
