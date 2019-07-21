using System.Collections.Generic;
using System.Threading;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace WeaponCore.Support
{
    public partial class GridTargetingAi
    {
        internal void UpdateTargetDb()
        {
            NewEntities.Clear();

            Targeting.AllowScanning = true;
            foreach (var ent in Targeting.TargetRoots)
            {
                MyDetectedEntityInfo entInfo;
                if (ent == null || ent == MyGrid || ent is MyVoxelBase || ent.Physics == null || ent is IMyFloatingObject 
                    || ent.MarkedForClose || ent.Physics.IsPhantom || !CreateEntInfo(ent, MyOwner, out entInfo)) continue;

                switch (entInfo.Relationship)
                {
                    case MyRelationsBetweenPlayerAndBlock.Owner:
                        continue;
                    case MyRelationsBetweenPlayerAndBlock.FactionShare:
                        continue;
                    case MyRelationsBetweenPlayerAndBlock.NoOwnership:
                        if (!TargetNoOwners) continue;
                        break;
                    case MyRelationsBetweenPlayerAndBlock.Neutral:
                        if (!TargetNeutrals) continue;
                        break;
                }
                if (ent is MyCubeGrid)
                {
                    var cubeList = CubePool.Get();
                    NewEntities.Add(new DetectInfo(ent, cubeList, entInfo));
                    ValidGrids.Add(ent, cubeList);
                }
                else NewEntities.Add(new DetectInfo(ent, null, entInfo));
            }

            GetTargetBlocks(Targeting, this);
            Targeting.AllowScanning = false;

            ValidGrids.Clear();
        }

        private static void GetTargetBlocks(MyGridTargeting targeting, GridTargetingAi ai)
        {
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = targeting.TargetBlocks;
            foreach (var targets in allTargets)
            {
                var rootGrid = targets.Key;
                List<MyEntity> cubes;
                if (ai.ValidGrids.TryGetValue(rootGrid, out cubes))
                {
                    for (int i = 0; i < targets.Value.Count; i++)
                        cubes.Add(targets.Value[i]);
                }
            }
        }
    }
}
