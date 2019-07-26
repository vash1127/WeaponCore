using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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

                //if (myEntity is MyCubeGrid)
                    //AddToSlimSpace(myEntity);
                if (weaponBase != null)
                {
                    if (!Inited) lock (_configLock) Init();
                    if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;
                    GridTargetingAi gridAi;
                    if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                    {
                        gridAi = new GridTargetingAi(cube.CubeGrid, this);
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
                var weaponBase = cube as IMyLargeMissileTurret;

                //if (myEntity is MyCubeGrid)
                    //RemoveFromSlimSpace(myEntity);
                if (weaponBase != null)
                {
                    if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

                    GridTargetingAIs[cube.CubeGrid].WeaponBase.Remove(cube);
                    if (GridTargetingAIs[cube.CubeGrid].WeaponBase.Count == 0)
                        GridTargetingAIs.Remove(cube.CubeGrid);
                }

            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityDelete: {ex}"); }
        }
    }
}
