using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage.Collections;
using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support
{
    public partial class GridTargetingAi
    {
        internal volatile bool Ready;
        internal volatile bool Stale;
        internal static volatile bool SubGridUpdate;
        internal readonly MyCubeGrid MyGrid;
        internal readonly MyConcurrentPool<List<MyEntity>> CubePool = new MyConcurrentPool<List<MyEntity>>(50);
        internal readonly ConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new ConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly Dictionary<MyEntity, List<MyEntity>> ValidGrids = new Dictionary<MyEntity, List<MyEntity>>();
        internal readonly List<DetectInfo> NewEntities = new List<DetectInfo>();
        internal readonly HashSet<MyCubeGrid> SubGrids = new HashSet<MyCubeGrid>();

        internal List<TargetInfo> SortedTargets = new List<TargetInfo>();

        internal MyGridTargeting Targeting { get; set; }
        internal bool TargetNeutrals;
        internal bool TargetNoOwners;
        internal bool SubUpdate;
        internal Random Rnd;
        internal Session MySession;
        internal DSUtils DsWatch = new DSUtils();
        internal uint SubTick;
        internal uint TargetsUpdatedTick;
        internal long MyOwner;
        internal long DbUpdating;

        internal BoundingBoxD GroupAABB;
        internal readonly TargetCompare TargetCompare1 = new TargetCompare();
    }
}
