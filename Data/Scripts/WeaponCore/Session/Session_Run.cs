using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation, int.MinValue)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            try
            {
                BeforeStartInit();
                MyConfig.Init();
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }

        public override void Draw()
        {
            try
            {
                if (!DedicatedServer)
                {
                    for (int i = 0; i < _projectiles.Wait.Length; i++)
                        lock (_projectiles.Wait[i]) DrawLists(_projectiles.DrawProjectiles[i]);
                    if (_shrinking.Count > 0)
                        Shrink();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();
                if (!_projectiles.Hits.IsEmpty) ProcessHits();
                UpdateWeaponPlatforms();
                MyAPIGateway.Parallel.Start(_projectiles.Update);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void LoadData()
        {
            Instance = this;
            Log.Init("debugdevelop.log");
            //Log.Line($"Logging Started");
            MasterLoadData();
            MyEntities.OnEntityCreate += OnEntityCreate;
            MyEntities.OnEntityDelete += OnEntityDelete;

            //MyEntities.OnEntityCreate += MyEntities_OnEntityCreate;
        }

        private void OnEntityCreate(MyEntity myEntity)
        {
            var cube = myEntity as MyCubeBlock;
            if (cube != null)
            {
                var weaponBase = cube as IMyLargeMissileTurret;
                if (weaponBase != null && WeaponStructures.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                {
                    GridTargetingAIs.Add(cube.CubeGrid, new GridTargetingAi(cube.CubeGrid));
                    var weaponComp = new WeaponComponent(cube, weaponBase);
                    cube.Components.Add(weaponComp);
                    GridTargetingAIs[cube.CubeGrid].WeaponBase.Add(cube, weaponComp);
                    Log.Line($"Iscube: {cube.DebugName} - {cube.SubBlockName} - {cube.BlockDefinition.Id.SubtypeId.String}");
                }
            }
        }

        private void OnEntityDelete(MyEntity myEntity)
        {
            var cube = myEntity as MyCubeBlock;
            if (cube != null)
            {
                var weaponBase = cube as IMyLargeMissileTurret;
                if (weaponBase != null && WeaponStructures.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                {
                    GridTargetingAIs[cube.CubeGrid].WeaponBase.Remove(cube);
                    Log.Line($"removing Weapon");
                    if (GridTargetingAIs[cube.CubeGrid].WeaponBase.Count == 0)
                    {
                        Log.Line($"last weapon, removing grid");
                        GridTargetingAIs.Remove(cube.CubeGrid);
                    }
                }
            }
        }
        protected override void UnloadData()
        {
            SApi.Unload();

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PACKET_ID, ReceivedPacket);

            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;

            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
}

