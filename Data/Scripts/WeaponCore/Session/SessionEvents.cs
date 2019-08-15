using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        private void OnEntityCreate(MyEntity myEntity)
        {
            try
            {
                var cube = myEntity as MyCubeBlock;
                var weaponBase = cube as IMyLargeMissileTurret;

                if (weaponBase != null)
                {
                    if (!Inited) lock (_configLock) Init();
                    if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;
                    GridAi gridAi;
                    if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                    {
                        gridAi = new GridAi(cube.CubeGrid, this);
                        GridTargetingAIs.TryAdd(cube.CubeGrid, gridAi);
                    }
                    var weaponComp = new WeaponComponent(gridAi, cube, weaponBase);
                    GridTargetingAIs[cube.CubeGrid].WeaponBase.TryAdd(cube, weaponComp);
                    _compsToStart.Enqueue(weaponComp);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }

        private void OnEntityDelete(MyEntity myEntity)
        {
            try
            {
                var cube = myEntity as MyCubeBlock;
                if (WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) _compsToRemove.Enqueue(GridTargetingAIs[cube.CubeGrid].WeaponBase[cube]);
            }
            catch (Exception ex) { } //Likely a preview delete
            
        }
    }
}
