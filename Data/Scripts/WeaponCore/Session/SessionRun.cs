using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using WeaponCore.Support;
using static Sandbox.Definitions.MyDefinitionManager;

namespace WeaponCore
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation, int.MaxValue - 1)]
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


        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();
                Ui.UpdateInput();
                if (!Hits.IsEmpty) ProcessHits();
                if (!InventoryEvent.IsEmpty) UpdateBlockInventories();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void Simulate()
        {
            try
            {
                if (!DedicatedServer)
                {
                    ControlledEntity = MyAPIGateway.Session.CameraController.Entity;
                    CameraPos = Session.Camera.Position;
                }
                AiLoop();
                UpdateWeaponPlatforms();
                Projectiles.Update();

                if (MyAPIGateway.Input.IsNewLeftMouseReleased())
                    Pointer.SelectTarget();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void UpdatingStopped()
        {
            try
            {
                Paused();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdatingStopped: {ex}"); }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (!MyCubeBuilder.Static.DynamicMode)
                {
                    if (MyCubeBuilder.Static.HitInfo.HasValue)
                    {
                        var hit = MyCubeBuilder.Static.HitInfo.Value as IHitInfo;
                        var grid = hit.HitEntity as MyCubeGrid;
                        GridAi gridAi;
                        if (grid != null && GridTargetingAIs.TryGetValue(grid, out gridAi))
                        {
                            if (MyCubeBuilder.Static.CurrentBlockDefinition != null)
                            {
                                var subtypeIdHash = MyCubeBuilder.Static.ToolbarBlockDefinition.Id.SubtypeId;
                                GridAi.WeaponCount weaponCount;
                                if (gridAi.WeaponCounter.TryGetValue(subtypeIdHash, out weaponCount))
                                {
                                    Log.Line($"{weaponCount.Current} - {weaponCount.Max}");
                                    if (weaponCount.Current > weaponCount.Max) MyAPIGateway.CubeBuilder.DeactivateBlockCreation();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void Draw()
        {
            try
            {
                if (!DedicatedServer)
                {
                    if (Ui.WheelActive && !MyAPIGateway.Session.Config.MinimalHud && !MyAPIGateway.Gui.IsCursorVisible)
                        Ui.DrawWheel();

                    Pointer.DrawSelector();

                    for (int i = 0; i < Projectiles.Wait.Length; i++)
                        lock (Projectiles.Wait[i])
                            DrawLists(Projectiles.DrawProjectiles[i]);
                    if (_shrinking.Count > 0)
                        Shrink();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void LoadData()
        {
            try
            {
                Instance = this;
                MyEntities.OnEntityCreate += OnEntityCreate;
                //MyEntities.OnEntityAdd += OnEntityAdd;
                //MyEntities.OnEntityRemove += OnEntityRemove;
                //MyEntities.OnEntityDelete += OnEntityDelete;
                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);
                AllDefinitions = Static.GetAllDefinitions();
                SoundDefinitions = Static.GetSoundDefinitions();
            }
            catch (Exception ex) { Log.Line($"Exception in LoadData: {ex}"); }
        }

        protected override void UnloadData()
        {
            SApi.Unload();

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PACKET_ID, ReceivedPacket);
            MyAPIGateway.Utilities.UnregisterMessageHandler(7771, Handler);

            MyEntities.OnEntityCreate -= OnEntityCreate;
            //MyEntities.OnEntityAdd -= OnEntityAdd;
            //MyEntities.OnEntityRemove -= OnEntityRemove;
            //MyEntities.OnEntityDelete -= OnEntityDelete;

            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;

            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
}

