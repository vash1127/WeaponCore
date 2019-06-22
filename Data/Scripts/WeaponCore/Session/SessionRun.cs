using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using WeaponCore.Support;
using static Sandbox.Definitions.MyDefinitionManager;

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
                if (!InventoryEvent.IsEmpty) UpdateBlockInventories();
                UpdateWeaponPlatforms();
                MyAPIGateway.Parallel.Start(AiLoop);
                MyAPIGateway.Parallel.Start(Projectiles.Update);
                //Projectiles.Update();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void LoadData()
        {
            try
            {
                Instance = this;
                MyEntities.OnEntityCreate += OnEntityCreate;
                MyEntities.OnEntityAdd += OnEntityAdd;
                MyEntities.OnEntityDelete += OnEntityDelete;

                //MyEntities.OnEntityRemove += OnEntityRemove;
                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);
                AllDefinitions = Static.GetAllDefinitions();
            }
            catch (Exception ex) { Log.Line($"Exception in LoadData: {ex}"); }
        }

        private void OnEntityCreate(MyEntity myEntity)
        {
            try
            {
                var cube = myEntity as MyCubeBlock;
                var weaponBase = cube as IMyLargeMissileTurret;
                if (weaponBase == null) return;
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
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }

        private void OnEntityAdd(MyEntity myEntity)
        {
            try
            {
                if (!_compsToStart.IsEmpty)
                {
                    WeaponComponent weaponComp;
                    _compsToStart.TryDequeue(out weaponComp);
                    weaponComp.MyCube.Components.Add(weaponComp);
                    weaponComp.OnAddedToScene();
                    Log.Line($"added to comp");
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
                if (weaponBase == null) return;

                if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

                GridTargetingAIs[cube.CubeGrid].WeaponBase.Remove(cube);
                if (GridTargetingAIs[cube.CubeGrid].WeaponBase.Count == 0)
                    GridTargetingAIs.Remove(cube.CubeGrid);
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityDelete: {ex}"); }
        }

        protected override void UnloadData()
        {
            SApi.Unload();

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PACKET_ID, ReceivedPacket);
            MyAPIGateway.Utilities.UnregisterMessageHandler(7771, Handler);

            MyEntities.OnEntityCreate -= OnEntityCreate;
            MyEntities.OnEntityAdd -= OnEntityAdd;
            MyEntities.OnEntityDelete -= OnEntityDelete;

            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;

            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
}

