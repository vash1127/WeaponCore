using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using static WeaponCore.Support.HitEntity;
using static WeaponCore.Support.AreaDamage.AreaEffectType;
namespace WeaponCore
{
    public partial class Session
    {
        internal readonly Queue<Effect> EffectStore = new Queue<Effect>();
        private readonly List<MyCubeBlock> _effectedCubeHits = new List<MyCubeBlock>();
        private readonly List<MyCubeGrid> _effectedGridHits = new List<MyCubeGrid>();
        private readonly List<MyEntity> _pruneGrids = new List<MyEntity>();
        private readonly Dictionary<MyCubeGrid, EffectHit> _effectedGridShapes = new Dictionary<MyCubeGrid, EffectHit>();
        private readonly Dictionary<long, BlockState> _effectedCubes = new Dictionary<long, BlockState>();
        private readonly Queue<long> _effectPurge = new Queue<long>();
        internal readonly HashSet<MyCubeGrid> RemoveEffectsFromGrid = new HashSet<MyCubeGrid>();


        private bool _effectActive;
        internal readonly EffectWork EffectWork = new EffectWork();
        internal volatile bool EffectDispatched;
        internal volatile bool EffectLoaded;


        private void PrepBlastEffect()
        {
            var stackCount = 0;
            var warHeadSize = 0;
            var warHeadYield = 0d;
            var epiCenter = Vector3D.Zero;

            Effect effectChild;
            while (EffectStore.TryDequeue(out effectChild))
            {
                if (effectChild.CustomData.Contains("@EMP"))
                {
                    stackCount++;
                    warHeadSize = effectChild.Size;
                    warHeadYield = effectChild.Yield;
                    epiCenter += effectChild.Position;
                }
            }

            if (stackCount == 0)
            {
                EffectWork.EventComplete();
                return;
            }
            epiCenter /= stackCount;
            var rangeCap = MathHelper.Clamp(stackCount * warHeadYield, warHeadYield, SyncDist);

            _effectedGridHits.Clear();
            _pruneGrids.Clear();

            var sphere = new BoundingSphereD(epiCenter, rangeCap);
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, _pruneGrids);

            foreach (var ent in _pruneGrids)
            {
                var grid = ent as MyCubeGrid;
                if (grid != null)
                {
                    var gridCenter = grid.PositionComp.WorldVolume.Center;
                    var testDir = Vector3D.Normalize(gridCenter - epiCenter);
                    var impactPos = gridCenter + (testDir * -grid.PositionComp.WorldVolume.Radius);

                    IHitInfo hitInfo;
                    MyAPIGateway.Physics.CastRay(epiCenter, impactPos, out hitInfo, CollisionLayers.DefaultCollisionLayer);
                    if (hitInfo?.HitEntity == null) _effectedGridHits.Add(grid);
                }
            }

            EffectWork.StoreEmpBlast(epiCenter, warHeadSize, warHeadYield, stackCount, rangeCap);
        }

        private void ComputeBlast()
        {
            var epiCenter = EffectWork.EpiCenter;
            var rangeCap = EffectWork.RangeCap;
            var dirYield = EffectWork.DirYield;
            const double BlockInflate = 1.25;

            GetBlastFilteredItems(epiCenter, rangeCap, dirYield);

            foreach (var cube in _effectedCubeHits)
            {
                EffectHit effectHit;
                var foundSphere = _effectedGridShapes.TryGetValue(cube.CubeGrid, out effectHit);
                if (foundSphere && effectHit.Sphere.Contains(cube.PositionComp.WorldAABB.Center) != ContainmentType.Disjoint)
                {
                    var clearance = cube.CubeGrid.GridSize * BlockInflate;
                    var testDir = Vector3D.Normalize(epiCenter - cube.PositionComp.WorldAABB.Center);
                    var testPos = cube.PositionComp.WorldAABB.Center + (testDir * clearance);
                    var hit = cube.CubeGrid.RayCastBlocks(epiCenter, testPos);

                    if (hit == null)
                    {
                        BlockState blockState;
                        uint endTick;

                        var cubeId = cube.EntityId;
                        var oldState = _effectedCubes.TryGetValue(cubeId, out blockState);

                        if (oldState) endTick = blockState.Endtick + (Tick + (effectHit.Duration + 1));
                        else endTick = Tick + (effectHit.Duration + 1);
                        var startTick = (((Tick + 1) / 20) * 20) + 20;

                        //_effectedCubes[cube.EntityId] = new BlockState(cube, startTick, endTick);
                    }
                    else if (cube.SlimBlock == cube.CubeGrid.GetCubeBlock(hit.Value))
                    {
                        BlockState blockState;
                        uint endTick;

                        var cubeId = cube.EntityId;
                        var oldState = _effectedCubes.TryGetValue(cubeId, out blockState);

                        if (oldState) endTick = blockState.Endtick + (Tick + (effectHit.Duration + 1));
                        else endTick = Tick + (effectHit.Duration + 1);
                        var startTick = (((Tick + 1) / 20) * 20) + 20;

                        //_effectedCubes[cube.EntityId] = new BlockState(cube, startTick, endTick);
                    }
                }
            }
            EffectWork.ComputeComplete();
        }

        private void GetBlastFilteredItems(Vector3D epiCenter, double rangeCap, double dirYield)
        {
            _effectedCubeHits.Clear();
            _effectedGridShapes.Clear();
            var myCubeList = new List<MyEntity>();
            foreach (var grid in _effectedGridHits)
            {
                var invSqrDist = UtilsStatic.InverseSqrDist(epiCenter, grid.PositionComp.WorldAABB.Center, rangeCap);
                var damage = (uint)(dirYield * invSqrDist) * 5;
                var gridAabb = grid.PositionComp.WorldAABB;
                var sphere = NewObbClosestTriCorners(grid, epiCenter);

                grid.Hierarchy.QueryAABB(ref gridAabb, myCubeList);
                _effectedGridShapes.Add(grid, new EffectHit(sphere, damage));
            }

            for (int i = 0; i < myCubeList.Count; i++)
            {
                var myEntity = myCubeList[i];
                var myCube = myEntity as MyCubeBlock;

                if (myCube == null || myCube.MarkedForClose) continue;
                if ((myCube is IMyThrust || myCube is IMyUserControllableGun || myCube is IMyUpgradeModule) && myCube.IsFunctional && myCube.IsWorking)
                {
                    _effectedCubeHits.Add(myCube);
                }
            }
        }

        private void ComputeField(HitEntity hitEnt, Trajectile t)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            var system = t.System;
            var eWarInfo = system.Values.Ammo.AreaEffect.EwarFields;
            var depletable = eWarInfo.Depletable;
            var healthPool = !depletable ? t.BaseHealthPool : float.MaxValue;
            var pruneSphere = hitEnt.PruneSphere;
            if (grid == null || grid.MarkedForClose || healthPool <= 0) return;
            var fieldType = system.Values.Ammo.AreaEffect.AreaEffect;
            var duration = (uint)eWarInfo.Duration;
            var stack = eWarInfo.StackDuration;
            var nextTick = Tick + 1;

            var damagePool = t.AreaEffectDamage;
            var objectsHit = t.ObjectsHit;
            var countBlocksAsObjects = system.Values.Ammo.ObjectsHit.CountBlocks;
            var largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
            var maxObjects = t.System.MaxObjectsHit;
            var shieldByPass = system.Values.DamageScales.Shields.Type == ShieldDefinition.ShieldType.Bypass;
            var attackerId = shieldByPass ? grid.EntityId : t.Target.FiringCube.EntityId;
            WeaponCore.Projectiles.Projectiles.GetAndSortBlocksInSphere(hitEnt, grid, hitEnt.PruneSphere.Center, !hitEnt.DamageOverTime);
            foreach (var block in hitEnt.Blocks)
            {
                var cube = block.FatBlock as MyCubeBlock;
                if (damagePool <= 0 || objectsHit >= maxObjects || healthPool <= 0) break;

                if (fieldType != DotField)
                {
                    if (cube == null || cube.MarkedForClose || !cube.IsWorking && !_effectedCubes.ContainsKey(cube.EntityId)) continue;
                }
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

                if (countBlocksAsObjects) objectsHit++;

                var blockDisabled = false;
                if (scaledDamage <= blockHp)
                    tmpDamagePool = 0;
                else
                {
                    blockDisabled = true;
                    tmpDamagePool -= blockHp;
                }

                if (fieldType == DotField)
                {
                    block.DoDamage(scaledDamage, MyDamageType.Explosion, true, null, attackerId);
                    continue;
                }

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
                    else
                    {
                        blockState.Health = 0;
                        healthPool -= 1;
                        blockState.Endtick += (duration + 1);
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

            if (depletable) t.BaseHealthPool -= healthPool;
        }

        private void EffectCallBack()
        {
            EffectDispatched = false;
            if (_effectedCubes.Count > 0) _effectActive = true;
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

        internal void PurgeAllEffects()
        {
            foreach (var item in _effectedCubes)
            {
                var cubeid = item.Key;
                var blockInfo = item.Value;
                var functBlock = blockInfo.FunctBlock;
                var cube = blockInfo.CubeBlock;

                if (cube == null || cube.MarkedForClose)
                {
                    _effectPurge.Enqueue(cubeid);
                    continue;
                }

                functBlock.EnabledChanged -= ForceDisable;
                functBlock.Enabled = blockInfo.FirstState;
                cube.SetDamageEffect(false);
                _effectPurge.Enqueue(cubeid);
            }

            while (_effectPurge.Count != 0)
            {
                _effectedCubes.Remove(_effectPurge.Dequeue());
            }

            _effectActive = false;
            RemoveEffectsFromGrid.Clear();
        }

        private static void ForceDisable(IMyTerminalBlock myTerminalBlock)
        {
            ((IMyFunctionalBlock)myTerminalBlock).Enabled = false;
        }

        public static BoundingSphereD NewObbClosestTriCorners(MyEntity ent, Vector3D pos)
        {
            var entCorners = new Vector3D[8];

            var quaternion = Quaternion.CreateFromRotationMatrix(ent.PositionComp.GetOrientation());
            var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
            var gridCenter = ent.PositionComp.WorldAABB.Center;
            var obb = new MyOrientedBoundingBoxD(gridCenter, halfExtents, quaternion);

            var minValue = double.MaxValue;
            var minValue0 = double.MaxValue;
            var minValue1 = double.MaxValue;
            var minValue2 = double.MaxValue;
            var minValue3 = double.MaxValue;

            var minNum = -2;
            var minNum0 = -2;
            var minNum1 = -2;
            var minNum2 = -2;
            var minNum3 = -2;

            obb.GetCorners(entCorners, 0);
            for (int i = 0; i < entCorners.Length; i++)
            {
                var gridCorner = entCorners[i];
                var range = gridCorner - pos;
                var test = (range.X * range.X) + (range.Y * range.Y) + (range.Z * range.Z);
                if (test < minValue3)
                {
                    if (test < minValue)
                    {
                        minValue3 = minValue2;
                        minNum3 = minNum2;
                        minValue2 = minValue1;
                        minNum2 = minNum1;
                        minValue1 = minValue0;
                        minNum1 = minNum0;
                        minValue0 = minValue;
                        minNum0 = minNum;
                        minValue = test;
                        minNum = i;
                    }
                    else if (test < minValue0)
                    {
                        minValue3 = minValue2;
                        minNum3 = minNum2;
                        minValue2 = minValue1;
                        minNum2 = minNum1;
                        minValue1 = minValue0;
                        minNum1 = minNum0;
                        minValue0 = test;
                        minNum0 = i;
                    }
                    else if (test < minValue1)
                    {
                        minValue3 = minValue2;
                        minNum3 = minNum2;
                        minValue2 = minValue1;
                        minNum2 = minNum1;
                        minValue1 = test;
                        minNum1 = i;
                    }
                    else if (test < minValue2)
                    {
                        minValue3 = minValue2;
                        minNum3 = minNum2;
                        minValue2 = test;
                        minNum2 = i;
                    }
                    else
                    {
                        minValue3 = test;
                        minNum3 = i;
                    }
                }
            }
            var corner = entCorners[minNum];
            var corner0 = entCorners[minNum0];
            var corner1 = entCorners[minNum1];
            var corner2 = entCorners[minNum2];
            var corner3 = gridCenter;
            Vector3D[] closestCorners = { corner, corner0, corner3 };

            var sphere = BoundingSphereD.CreateFromPoints(closestCorners);
            //var subObb = MyOrientedBoundingBoxD.CreateFromBoundingBox(box);
            return sphere;
        }
    }

    internal class EffectWork
    {
        internal MyCubeGrid Grid;
        internal Vector3D EpiCenter;
        internal int WarHeadSize;
        internal double WarHeadYield;
        internal double DirYield;
        internal int StackCount;
        internal double RangeCap;
        internal double RangeCapSqr;
        internal bool Stored;
        internal bool Computed;
        internal bool Drawed;
        internal bool EventRunning;

        internal void StoreEmpBlast(Vector3D epicCenter, int warHeadSize, double warHeadYield, int stackCount, double rangeCap)
        {
            EpiCenter = epicCenter;
            WarHeadSize = warHeadSize;
            WarHeadYield = warHeadYield;
            StackCount = stackCount;
            RangeCap = rangeCap;
            RangeCapSqr = rangeCap * rangeCap;
            DirYield = (warHeadYield * stackCount) * 0.5;
            Stored = true;
            EventRunning = true;
        }

        internal void ComputeComplete()
        {
            Computed = true;
        }

        internal void EventComplete()
        {
            Computed = false;
            Drawed = false;
            Stored = false;
            EventRunning = false;
        }
    }

    public struct Effect
    {
        public readonly int Size;
        public readonly double Yield;
        public readonly Vector3D Position;
        public readonly string CustomData;

        public Effect(int size, Vector3D position, string customData)
        {
            Size = size;
            Yield = Size * 50;
            Position = position;
            CustomData = customData;
        }
    }

    public struct EffectHit
    {
        public readonly uint Duration;
        public BoundingSphereD Sphere;

        public EffectHit(BoundingSphereD sphere, uint duration)
        {
            Sphere = sphere;
            Duration = duration;
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
}
