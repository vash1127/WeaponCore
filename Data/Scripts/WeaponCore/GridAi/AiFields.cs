using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static WeaponCore.Support.SubSystemDefinition;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal volatile bool Ready;
        internal static volatile bool SubGridUpdate;
        internal readonly MyCubeGrid MyGrid;
        internal readonly MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>>(50);
        internal readonly MyConcurrentPool<List<MyCubeBlock>> CubePool = new MyConcurrentPool<List<MyCubeBlock>>(50);

        internal readonly ConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new ConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly Dictionary<MyEntity, Dictionary<BlockTypes, List<MyCubeBlock>>> ValidGrids = new Dictionary<MyEntity, Dictionary<BlockTypes, List<MyCubeBlock>>>();
        internal readonly HashSet<BlockTypes> BlockTypeIsSorted = new HashSet<BlockTypes>();

        internal readonly List<DetectInfo> NewEntities = new List<DetectInfo>();
        internal readonly HashSet<MyCubeGrid> SubGrids = new HashSet<MyCubeGrid>();

        internal List<TargetInfo> SortedTargets = new List<TargetInfo>();

        internal MyGridTargeting Targeting { get; set; }
        internal bool TargetNeutrals;
        internal bool TargetNoOwners;
        internal bool SubUpdate;
        internal Session MySession;
        internal DSUtils DsWatch = new DSUtils();
        internal uint SubTick;
        internal uint TargetsUpdatedTick;
        internal long MyOwner;
        internal int DbUpdating;
        internal bool DbReady;

        internal BoundingBoxD GroupAABB;
        internal readonly TargetCompare TargetCompare1 = new TargetCompare();
    }
}
