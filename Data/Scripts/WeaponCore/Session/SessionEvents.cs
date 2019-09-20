using System;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        private void OnEntityCreate(MyEntity myEntity)
        {
            try
            {
                var weaponBase = myEntity as IMyLargeMissileTurret;
                var placer = myEntity as IMyBlockPlacerBase;
                if (placer != null && Placer == null) Placer = placer;
                if (weaponBase != null)
                {
                    if (!Inited) lock (_configLock) Init();
                    var cube = (MyCubeBlock)myEntity;
                    if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

                    using (myEntity.Pin())
                    {
                        if (myEntity.MarkedForClose) return;
                        GridAi gridAi;
                        if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                        {
                            gridAi = new GridAi(cube.CubeGrid);
                            GridTargetingAIs.TryAdd(cube.CubeGrid, gridAi);
                        }
                        var weaponComp = new WeaponComponent(gridAi, cube, weaponBase);
                        if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp))
                        {
                            gridAi.WeaponCounter.TryAdd(cube.BlockDefinition.Id.SubtypeId, new GridAi.WeaponCount());
                            CompsToStart.Enqueue(weaponComp);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }

        private void OnPrefabSpawn(long entityId, string prefabName)
        {
            var grid = MyEntities.GetEntityById(entityId) as MyCubeGrid;
            if (grid == null) return;

            foreach (var block in grid.GetFatBlocks())
            {
                if (WeaponPlatforms.ContainsKey(block.BlockDefinition.Id.SubtypeId))
                {
                    PastedBlocksToInit.Enqueue(block);
                }
            }
        }


        private void OnEntityAdded(MyEntity obj)
        {
            var grid = obj as MyCubeGrid;

            if (grid == null) return;
            
            foreach (var block in grid.GetFatBlocks())
            {
                if (WeaponPlatforms.ContainsKey(block.BlockDefinition.Id.SubtypeId))
                {
                    PastedBlocksToInit.Enqueue(block);
                }
            }

        }
    }
}
