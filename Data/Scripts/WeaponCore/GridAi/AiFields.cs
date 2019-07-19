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
        internal readonly Dictionary<MyEntity, MyDetectedEntityInfo> ValidTargets = new Dictionary<MyEntity, MyDetectedEntityInfo>();
        internal readonly List<TargetInfo> SortedTargets = new List<TargetInfo>();
        //internal readonly List<MyEntity> TargetBlocks = new List<MyEntity>();
        internal readonly HashSet<MyCubeGrid> SubGrids = new HashSet<MyCubeGrid>();
        internal MyGridTargeting Targeting { get; set; }
        internal bool TargetNeutrals;
        internal bool TargetNoOwners;
        internal bool SubUpdate;
        internal Random Rnd;
        internal Session MySession;
        internal uint SubTick;
        internal BoundingBoxD GroupAABB;
        private readonly TargetCompare _targetCompare = new TargetCompare();
        private uint _targetsUpdatedTick;
        private long _myOwner;

        internal struct TargetInfo
        {
            internal readonly MyDetectedEntityInfo EntInfo;
            internal readonly MyEntity Target;
            internal readonly bool IsGrid;
            internal readonly int PartCount;
            internal readonly MyCubeGrid MyGrid;
            internal readonly GridTargetingAi Ai;
            internal readonly List<MyEntity> Cubes;
            internal readonly bool ValidCubes;


            internal TargetInfo(MyDetectedEntityInfo entInfo, MyEntity target, bool isGrid, int partCount, MyCubeGrid myGrid, GridTargetingAi ai, int numOfBlocks)
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
                    ValidCubes = GetTargetBlocks(target, numOfBlocks, ai.Targeting, Cubes);
                }
                else
                {
                    Cubes = null;
                    ValidCubes = false;
                }
            }

            internal bool Clean()
            {
                Cubes.Clear();
                Ai.CubePool.Return(Cubes);
                return true;
            }
        }
    }
}
