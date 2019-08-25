using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
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
                        if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp));
                            CompsToStart.Enqueue(weaponComp);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }
    }
}
