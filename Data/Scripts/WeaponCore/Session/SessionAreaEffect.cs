using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using static WeaponCore.Support.AreaDamage.AreaEffectType;
using static WeaponCore.Projectiles.Projectiles;

namespace WeaponCore
{
    public partial class Session
    {
        private readonly Dictionary<long, BlockState> _effectedCubes = new Dictionary<long, BlockState>();
        private readonly Dictionary<MyCubeGrid, Dictionary<AreaDamage.AreaEffectType, GridEffect>> _gridEffects = new Dictionary<MyCubeGrid, Dictionary<AreaDamage.AreaEffectType, GridEffect>>();
        internal readonly MyConcurrentPool<Dictionary<AreaDamage.AreaEffectType, GridEffect>> GridEffectsPool = new MyConcurrentPool<Dictionary<AreaDamage.AreaEffectType, GridEffect>>();
        internal readonly MyConcurrentPool<GridEffect> GridEffectPool = new MyConcurrentPool<GridEffect>();

        private readonly Queue<long> _effectPurge = new Queue<long>();
        internal readonly HashSet<MyCubeGrid> RemoveEffectsFromGrid = new HashSet<MyCubeGrid>();
        private bool _effectActive;

        private void UpdateField(HitEntity hitEnt, ProInfo info)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            if (grid == null || grid.MarkedForClose) return;
            var depletable = info.System.Values.Ammo.AreaEffect.EwarFields.Depletable;
            var healthPool = depletable && info.BaseHealthPool > 0 ? info.BaseHealthPool : float.MaxValue;
            if (healthPool <= 0) return;

            var attackerId = info.System.Values.DamageScales.Shields.Type == ShieldDefinition.ShieldType.Bypass ? grid.EntityId : info.Target.FiringCube.EntityId;
            GetAndSortBlocksInSphere(info.System, hitEnt.Info.Ai, grid, hitEnt.PruneSphere, !hitEnt.DamageOverTime, hitEnt.Blocks);
            ComputeEffects(info.System, grid, info.AreaEffectDamage, healthPool, attackerId, hitEnt.Blocks);
            if (depletable) info.BaseHealthPool -= healthPool;
        }

        private void UpdateEffect(HitEntity hitEnt, ProInfo info)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            if (grid == null || grid.MarkedForClose ) return;
            Dictionary<AreaDamage.AreaEffectType, GridEffect> effects;
            var attackerId = info.System.Values.DamageScales.Shields.Type == ShieldDefinition.ShieldType.Bypass ? grid.EntityId : info.Target.FiringCube.EntityId;
            var found = false;
            if (_gridEffects.TryGetValue(grid, out effects))
            {
                GridEffect gridEffect;
                if (effects.TryGetValue(info.System.Values.Ammo.AreaEffect.AreaEffect, out gridEffect))
                {
                    found = true;
                    gridEffect.Damage += info.AreaEffectDamage;
                    gridEffect.Ai = info.Ai;
                    gridEffect.AttackerId = attackerId;
                    gridEffect.Hits++;
                    if (hitEnt.HitPos != null) gridEffect.HitPos = hitEnt.HitPos.Value / gridEffect.Hits;
                }
            }

            if (!found)
            {
                if (effects == null) effects = GridEffectsPool.Get();
                GridEffect gridEffect;
                if (effects.TryGetValue(info.System.Values.Ammo.AreaEffect.AreaEffect, out gridEffect))
                {
                    gridEffect.Damage += info.AreaEffectDamage;
                    gridEffect.Ai = info.Ai;
                    gridEffect.AttackerId = attackerId;
                    gridEffect.Hits++;
                    if (hitEnt.HitPos != null) gridEffect.HitPos += hitEnt.HitPos.Value / gridEffect.Hits;
                }
                else
                {
                    gridEffect = GridEffectPool.Get();
                    gridEffect.System = info.System;
                    gridEffect.Damage = info.AreaEffectDamage;
                    gridEffect.Ai = info.Ai;
                    gridEffect.AttackerId = attackerId;
                    gridEffect.Hits++;
                    if (hitEnt.HitPos != null) gridEffect.HitPos = hitEnt.HitPos.Value;
                    effects.Add(info.System.Values.Ammo.AreaEffect.AreaEffect, gridEffect);
                }
                _gridEffects.Add(grid, effects);
            }
            info.BaseHealthPool = 0;
            info.BaseDamagePool = 0;
        }


        private void ComputeEffects(WeaponSystem system, MyCubeGrid grid, float damagePool, float healthPool, long attackerId, List<IMySlimBlock> blocks)
        {
            var largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
            var eWarInfo = system.Values.Ammo.AreaEffect.EwarFields;
            var duration = (uint)eWarInfo.Duration;
            var stack = eWarInfo.StackDuration;
            var maxStack = eWarInfo.MaxStacks;
            var nextTick = Tick + 1;
            var maxTick = stack ? (uint)(nextTick + (duration * maxStack)) : (uint)(nextTick + duration);
            var fieldType = system.Values.Ammo.AreaEffect.AreaEffect;
            var sync = MpActive && (DedicatedServer || IsServer);
            foreach (var block in blocks)
            {
                var cube = block.FatBlock as MyCubeBlock;
                if (damagePool <= 0 || healthPool <= 0) break;

                if (fieldType != DotField)
                    if (cube == null || cube.MarkedForClose || !cube.IsWorking && !_effectedCubes.ContainsKey(cube.EntityId)) continue;

                var blockHp = block.Integrity;
                float damageScale = 1;
                var tmpDamagePool = damagePool;

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

                var scaledDamage = tmpDamagePool * damageScale;

                var blockDisabled = false;
                if (scaledDamage <= blockHp)
                    tmpDamagePool = 0;
                else
                {
                    blockDisabled = true;
                    tmpDamagePool -= blockHp;
                }

                if (fieldType == DotField && (IsServer || DedicatedServer))
                {
                    block.DoDamage(scaledDamage, MyDamageType.Explosion, sync, null, attackerId);
                    continue;
                }

                if (cube != null)
                {
                    BlockState blockState;
                    var cubeId = cube.EntityId;
                    if (stack && _effectedCubes.TryGetValue(cubeId, out blockState))
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
                        blockState.FunctBlock = ((IMyFunctionalBlock)cube);
                        var originState = blockState.FunctBlock.Enabled;
                        blockState.CubeBlock = cube;
                        blockState.FirstTick = Tick + 1;
                        blockState.FirstState = originState;
                        blockState.NextTick = nextTick;
                        blockState.Endtick = Tick + (duration + 1);
                        if (!blockDisabled) blockState.Health = blockHp - scaledDamage;
                        else
                        {
                            blockState.Health = 0;
                        }
                    }
                    _effectedCubes[cube.EntityId] = blockState;
                }
            }
        }

        private void ApplyEffect()
        {
            var tick = Tick;
            foreach (var item in _effectedCubes)
            {
                var cubeid = item.Key;
                var blockInfo = item.Value;
                var functBlock = blockInfo.FunctBlock;
                var health = blockInfo.Health;
                var cube = blockInfo.CubeBlock;
                if (cube == null || cube.MarkedForClose)
                {
                    _effectPurge.Enqueue(cubeid);
                    continue;
                }

                if (health <= 0)
                {
                    if (cube.IsWorking)
                    {
                        functBlock.Enabled = false;
                        functBlock.EnabledChanged += ForceDisable;
                        cube.SetDamageEffect(true);
                    }
                }

                if (tick < blockInfo.Endtick)
                {
                    if (Tick60)
                    {
                        var grid = (MyCubeGrid) functBlock.CubeGrid;
                        if (RemoveEffectsFromGrid.Contains(grid))
                        {
                            functBlock.EnabledChanged -= ForceDisable;
                            functBlock.Enabled = blockInfo.FirstState;
                            cube.SetDamageEffect(false);
                            _effectPurge.Enqueue(cubeid);
                            RemoveEffectsFromGrid.Remove(grid);
                        }
                    }
                }
                else
                {
                    functBlock.EnabledChanged -= ForceDisable;
                    functBlock.Enabled = blockInfo.FirstState;
                    cube.SetDamageEffect(false);
                    _effectPurge.Enqueue(cubeid);
                }
            }

            while (_effectPurge.Count != 0)
            {
                _effectedCubes.Remove(_effectPurge.Dequeue());
            }

            if (_effectedCubes.Count == 0) _effectActive = false;
        }


        private static void ForceDisable(IMyTerminalBlock myTerminalBlock)
        {
            ((IMyFunctionalBlock)myTerminalBlock).Enabled = false;
        }


        private readonly List<IMySlimBlock> _tmpEffectCubes = new List<IMySlimBlock>();
        internal static void GetCubesForEffect(GridAi ai, MyCubeGrid grid, Vector3D hitPos, AreaDamage.AreaEffectType effectType, List<IMySlimBlock> cubes)
        {
            var fats = QueryBlockCaches(ai, grid, effectType);
            if (fats == null) return;

            for (int i = 0; i < fats.Count; i++) cubes.Add(fats[i].SlimBlock);

            cubes.Sort((a, b) =>
            {
                var aPos = grid.GridIntegerToWorld(a.Position);
                var bPos = grid.GridIntegerToWorld(b.Position);
                return -Vector3D.DistanceSquared(aPos, hitPos).CompareTo(Vector3D.DistanceSquared(bPos, hitPos));
            });
        }

        private static ConcurrentCachingList<MyCubeBlock> QueryBlockCaches(GridAi ai, MyCubeGrid targetGrid, AreaDamage.AreaEffectType effectType)
        {
            ConcurrentDictionary<TargetingDefinition.BlockTypes, ConcurrentCachingList<MyCubeBlock>> blockTypeMap;
            if (!ai.Session.GridToBlockTypeMap.TryGetValue(targetGrid, out blockTypeMap)) return null;

            ConcurrentCachingList<MyCubeBlock> cubes;
            switch (effectType)
            {
                case JumpNullField:
                    if (blockTypeMap.TryGetValue(TargetingDefinition.BlockTypes.Jumping, out cubes))
                        return cubes;
                    break;
                case EnergySinkField:
                    if (blockTypeMap.TryGetValue(TargetingDefinition.BlockTypes.Power, out cubes))
                        return cubes;
                    break;
                case AnchorField:
                    if (blockTypeMap.TryGetValue(TargetingDefinition.BlockTypes.Thrust, out cubes))
                        return cubes;
                    break;
                case NavField:
                    if (blockTypeMap.TryGetValue(TargetingDefinition.BlockTypes.Steering, out cubes))
                        return cubes;
                    break;
                case OffenseField:
                    if (blockTypeMap.TryGetValue(TargetingDefinition.BlockTypes.Offense, out cubes))
                        return cubes;
                    break;
                case EmpField:
                case DotField:
                    FatMap fatMap;
                    if (ai.Session.GridToFatMap.TryGetValue(targetGrid, out fatMap))
                        return fatMap.MyCubeBocks;
                    break;
            }

            return null;
        }
    }

    public struct BlockState
    {
        public MyCubeBlock CubeBlock;
        public IMyFunctionalBlock FunctBlock;
        public bool FirstState;
        public uint FirstTick;
        public uint NextTick;
        public uint Endtick;
        public float Health;
    }

    public class GridEffect
    {
        public Vector3D HitPos;
        public WeaponSystem System;
        public GridAi Ai;
        public long AttackerId;
        public float Damage;
        public int Hits;

        public void Clean()
        {
            System = null;
            HitPos = Vector3D.Zero;
            Ai = null;
            AttackerId = 0;
            Damage = 0;
            Hits = 0;
        }
    }
}
