using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using VRage.Collections;
using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support
{
    public partial class GridTargetingAi
    {
        internal volatile bool Ready;
        internal static volatile bool SubGridUpdate;
        internal readonly MyCubeGrid MyGrid;
        internal readonly MyConcurrentPool<List<MyEntity>> CubePool = new MyConcurrentPool<List<MyEntity>>(50);
        internal readonly ConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new ConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly Dictionary<MyEntity, MyDetectedEntityInfo> ValidGrids = new Dictionary<MyEntity, MyDetectedEntityInfo>();
        internal readonly List<TargetInfo> SortedTargets = new List<TargetInfo>();
        //internal readonly List<MyEntity> TargetBlocks = new List<MyEntity>();
        internal readonly HashSet<MyCubeGrid> SubGrids = new HashSet<MyCubeGrid>();
        internal MyGridTargeting Targeting { get; set; }
        internal bool TargetNeutrals;
        internal bool TargetNoOwners;
        internal bool SubUpdate;
        internal Random Rnd;
        internal Session MySession;
        internal DSUtils DsWatch = new DSUtils();
        internal uint SubTick;
        internal uint TargetsUpdatedTick;

        internal BoundingBoxD GroupAABB;
        private readonly TargetCompare _targetCompare = new TargetCompare();
        private long _myOwner;

        internal struct TargetInfo
        {
            internal readonly MyDetectedEntityInfo EntInfo;
            internal readonly MyEntity Target;
            internal readonly bool IsGrid;
            internal readonly int PartCount;
            internal readonly MyCubeGrid MyGrid;
            internal readonly GridTargetingAi Ai;
            internal List<MyEntity> Cubes;


            internal TargetInfo(MyDetectedEntityInfo entInfo, MyEntity target, bool isGrid, int partCount, MyCubeGrid myGrid, GridTargetingAi ai)
            {
                EntInfo = entInfo;
                Target = target;
                IsGrid = isGrid;
                PartCount = partCount;
                MyGrid = myGrid;
                Ai = ai;
                if (isGrid)
                {
                    Cubes = ai.CubePool.Get();
                }
                else
                {
                    Cubes = null;
                }
            }

            internal bool Clean()
            {
                if (IsGrid)
                {
                    Cubes.Clear();
                    Ai.CubePool.Return(Cubes);
                }
                return true;
            }
        }
    }
}
