using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.AreaDamageDef;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.AreaDamageDef.AreaEffectType;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.DamageScaleDef;
using static WeaponCore.Projectiles.Projectiles;

namespace WeaponCore
{
    public partial class Session
    {
        private readonly Dictionary<MyCubeGrid, Dictionary<AreaEffectType, GridEffect>> _gridEffects = new Dictionary<MyCubeGrid, Dictionary<AreaEffectType, GridEffect>>();
        internal readonly MyConcurrentPool<Dictionary<AreaEffectType, GridEffect>> GridEffectsPool = new MyConcurrentPool<Dictionary<AreaEffectType, GridEffect>>();
        internal readonly MyConcurrentPool<GridEffect> GridEffectPool = new MyConcurrentPool<GridEffect>();
        internal readonly Dictionary<long, BlockState> EffectedCubes = new Dictionary<long, BlockState>();

        private readonly Queue<long> _effectPurge = new Queue<long>();
        internal readonly HashSet<MyCubeGrid> RemoveEffectsFromGrid = new HashSet<MyCubeGrid>();
        private bool _effectActive;

        private void UpdateField(HitEntity hitEnt, ProInfo info)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            if (grid == null || grid.MarkedForClose) return;
            var depletable = info.AmmoDef.AreaEffect.EwarFields.Depletable;
            var healthPool = depletable && info.BaseHealthPool > 0 ? info.BaseHealthPool : float.MaxValue;
            if (healthPool <= 0) return;
            if (info.AmmoDef.Const.AreaEffect == PullField || info.AmmoDef.Const.AreaEffect == PushField)
            {
                if (grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                    return;
                var dir = info.AmmoDef.Const.AreaEffect == PushField ? hitEnt.Intersection.Direction : -hitEnt.Intersection.Direction;
                grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, dir * (info.AmmoDef.Const.AreaEffectDamage * hitEnt.Entity.Physics.Mass), grid.Physics?.CenterOfMassWorld, Vector3.Zero);
            }
            else
            {
                var attackerId = info.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Bypass ? grid.EntityId : info.Target.FiringCube.EntityId;
                GetAndSortBlocksInSphere(info.AmmoDef, hitEnt.Info.System, grid, hitEnt.PruneSphere, !hitEnt.DamageOverTime, hitEnt.Blocks);
                ComputeEffects(grid, info.AmmoDef, info.AmmoDef.Const.AreaEffectDamage, healthPool, attackerId, hitEnt.Blocks);
            }

            if (depletable) info.BaseHealthPool -= healthPool;
        }

        private void UpdateEffect(HitEntity hitEnt, ProInfo info)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            if (grid == null || grid.MarkedForClose ) return;
            Dictionary<AreaEffectType, GridEffect> effects;
            var attackerId = info.AmmoDef.DamageScales.Shields.Type == ShieldDef.ShieldType.Bypass ? grid.EntityId : info.Target.FiringCube.EntityId;
            var found = false;
            if (_gridEffects.TryGetValue(grid, out effects))
            {
                GridEffect gridEffect;
                if (effects.TryGetValue(info.AmmoDef.AreaEffect.AreaEffect, out gridEffect))
                {
                    found = true;
                    gridEffect.Damage += info.AmmoDef.Const.AreaEffectDamage;
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
                if (effects.TryGetValue(info.AmmoDef.AreaEffect.AreaEffect, out gridEffect))
                {
                    gridEffect.Damage += info.AmmoDef.Const.AreaEffectDamage;
                    gridEffect.Ai = info.Ai;
                    gridEffect.AmmoDef = info.AmmoDef;
                    gridEffect.AttackerId = attackerId;
                    gridEffect.Hits++;
                    if (hitEnt.HitPos != null) gridEffect.HitPos += hitEnt.HitPos.Value / gridEffect.Hits;
                }
                else
                {
                    gridEffect = GridEffectPool.Get();
                    gridEffect.System = info.System;
                    gridEffect.Damage = info.AmmoDef.Const.AreaEffectDamage;
                    gridEffect.Ai = info.Ai;
                    gridEffect.AmmoDef = info.AmmoDef;
                    gridEffect.AttackerId = attackerId;
                    gridEffect.Hits++;
                    if (hitEnt.HitPos != null) gridEffect.HitPos = hitEnt.HitPos.Value;
                    effects.Add(info.AmmoDef.AreaEffect.AreaEffect, gridEffect);
                }
                _gridEffects.Add(grid, effects);
            }
            info.BaseHealthPool = 0;
            info.BaseDamagePool = 0;
        }


        private void ComputeEffects(MyCubeGrid grid, AmmoDef ammoDef, float damagePool, float healthPool, long attackerId, List<IMySlimBlock> blocks)
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
                var cube = block.FatBlock as MyCubeBlock;
                if (damagePool <= 0 || healthPool <= 0) break;
                if (fieldType != DotField)
                    if (cube == null || cube.MarkedForClose || !cube.IsWorking && !EffectedCubes.ContainsKey(cube.EntityId) || !(cube is IMyFunctionalBlock)) continue;

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

                if (cube != null)
                {
                    BlockState blockState;
                    var cubeId = cube.EntityId;
                    if (stack && EffectedCubes.TryGetValue(cubeId, out blockState))
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
                    EffectedCubes[cube.EntityId] = blockState;
                }
            }
        }

        internal void GridEffects()
        {
            foreach (var ge in _gridEffects)
            {
                foreach (var v in ge.Value)
                {
                    GetCubesForEffect(v.Value.Ai, ge.Key, v.Value.HitPos, v.Key, _tmpEffectCubes);
                    ComputeEffects(ge.Key, v.Value.AmmoDef, v.Value.Damage * v.Value.Hits, float.MaxValue, v.Value.AttackerId, _tmpEffectCubes);
                    _tmpEffectCubes.Clear();
                    v.Value.Clean();
                    GridEffectPool.Return(v.Value);
                }
                ge.Value.Clear();
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
                EffectedCubes.Remove(_effectPurge.Dequeue());
            }

            if (EffectedCubes.Count == 0) _effectActive = false;
        }


        private static void ForceDisable(IMyTerminalBlock myTerminalBlock)
        {
            ((IMyFunctionalBlock)myTerminalBlock).Enabled = false;
        }


        private readonly List<IMySlimBlock> _tmpEffectCubes = new List<IMySlimBlock>();
        internal static void GetCubesForEffect(GridAi ai, MyCubeGrid grid, Vector3D hitPos, AreaEffectType effectType, List<IMySlimBlock> cubes)
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

        private static ConcurrentCachingList<MyCubeBlock> QueryBlockCaches(GridAi ai, MyCubeGrid targetGrid, AreaEffectType effectType)
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
                    FatMap fatMap;
                    if (ai.Session.GridToFatMap.TryGetValue(targetGrid, out fatMap))
                        return fatMap.MyCubeBocks;
                    break;
            }

            return null;
        }
    }

    internal struct BlockState
    {
        public MyCubeBlock CubeBlock;
        public IMyFunctionalBlock FunctBlock;
        public bool FirstState;
        public uint FirstTick;
        public uint NextTick;
        public uint Endtick;
        public float Health;
    }

    internal class GridEffect
    {
        internal Vector3D HitPos;
        internal WeaponSystem System;
        internal GridAi Ai;
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
