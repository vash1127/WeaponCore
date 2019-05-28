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
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }

        public override void Draw()
        {
            try
            {
                if (!DedicatedServer)
                {
                    for (int i = 0; i < Projectiles.Wait.Length; i++)
                        lock (Projectiles.Wait[i]) DrawLists(Projectiles.DrawProjectiles[i]);
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
                if (!Projectiles.Hits.IsEmpty) ProcessHits();
                UpdateWeaponPlatforms();
                MyAPIGateway.Parallel.Start(Projectiles.Update);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void LoadData()
        {
            try
            {
                Instance = this;
                MyEntities.OnEntityCreate += OnEntityCreate;
                MyEntities.OnEntityDelete += OnEntityDelete;
                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);
            }
            catch (Exception ex) { Log.Line($"Exception in LoadData: {ex}"); }
        }

        private void OnEntityCreate(MyEntity myEntity)
        {
            var cube = myEntity as MyCubeBlock;
            var weaponBase = cube as IMyLargeMissileTurret;
            if (weaponBase == null) return;

            if (!Inited) lock (_configLock) MyConfig.Init();
            if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

            GridTargetingAIs.Add(cube.CubeGrid, new GridTargetingAi(cube.CubeGrid));
            var weaponComp = new WeaponComponent(cube, weaponBase);
            cube.Components.Add(weaponComp);
            GridTargetingAIs[cube.CubeGrid].WeaponBase.Add(cube, weaponComp);
        }

        private void OnEntityDelete(MyEntity myEntity)
        {
            var cube = myEntity as MyCubeBlock;
            var weaponBase = cube as IMyLargeMissileTurret;
            if (weaponBase == null) return;

            if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

            GridTargetingAIs[cube.CubeGrid].WeaponBase.Remove(cube);
            if (GridTargetingAIs[cube.CubeGrid].WeaponBase.Count == 0)
                GridTargetingAIs.Remove(cube.CubeGrid);
        }

        protected override void UnloadData()
        {
            SApi.Unload();

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PACKET_ID, ReceivedPacket);
            MyAPIGateway.Utilities.UnregisterMessageHandler(7771, Handler);

            MyEntities.OnEntityCreate -= OnEntityCreate;
            MyEntities.OnEntityDelete -= OnEntityDelete;

            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;

            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
}

