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
                    Log.Line("Create");
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
                        if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp));
                            CompsToStart.Enqueue(weaponComp);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }
        /*

        private void OnEntityAdd(MyEntity myEntity)
        {
            try
            {
                var cube = myEntity as MyCubeBlock;
                var weaponBase = cube as IMyLargeMissileTurret;

                if (weaponBase != null)
                {
                    if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;
                    Log.Line("Add");
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
                            _compsToStart.Enqueue(weaponComp);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }
        private void OnEntityRemove(MyEntity myEntity)
        {
            try
            {
                var cube = myEntity as MyCubeBlock;
                var weaponBase = cube as IMyLargeMissileTurret;
                if (weaponBase != null && WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                {
                    Log.Line("Remove");
                    using (myEntity.Pin())
                    {
                        GridAi gridAi;
                        if (GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                            _compsToRemove.Enqueue(gridAi.WeaponBase[cube]);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityDelete: {ex}"); }
        }

        private void OnEntityDelete(MyEntity myEntity)
        {
            try
            {
                var cube = myEntity as MyCubeBlock;
                var weaponBase = cube as IMyLargeMissileTurret;
                if (weaponBase != null && WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                {
                    Log.Line("Delete");
                    using (myEntity.Pin())
                    {
                        GridAi gridAi;
                        if (GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                            _compsToRemove.Enqueue(gridAi.WeaponBase[cube]);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityDelete: {ex}"); }
        }
        */
    }
}
