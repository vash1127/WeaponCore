using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using WeaponThread;
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
                _futureEvents.Tick(Tick);
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
                    ControlledEntity = (MyEntity)MyAPIGateway.Session.ControlledObject;
                    
                    CameraPos = Session.Camera.Position;
                    ProcessAnimationQueue();
                }
                AiLoop();
                
                UpdateWeaponPlatforms();
                if (Tick600)
                {
                    var threshold = Projectiles.Wait.Length * 10;
                    HighLoad = Load > threshold;
                    Log.Line($"TurretLoad:{Load} - HighLoad:{threshold} - MultiCore:{HighLoad}");
                    Load = 0d;
                }
                Projectiles.Update();

                if (_effectedCubes.Count > 0) ApplyEffect();
                if (_gridEffects.Count > 0)
                {
                    foreach (var ge in _gridEffects)
                    {
                        foreach (var v in ge.Value)
                        {
                            Log.Line($"Effect:{v.Key} - hits:{v.Value.Hits} - {v.Value.Damage}");
                        }
                    }
                }

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
                var playerSphere = MyAPIGateway.Session.Player.Character.WorldVolume;
                playerSphere.Radius = 1;
                var test = new List<Projectile>();
                DynTrees.GetAllProjectilesInSphere(ref playerSphere, test);
                if (Placer != null)
                {
                    if (!Placer.Visible) Placer = null;
                    if (!MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.HitInfo.HasValue)
                    {
                        var hit = MyCubeBuilder.Static.HitInfo.Value as IHitInfo;
                        var grid = hit.HitEntity as MyCubeGrid;
                        GridAi gridAi;
                        if (grid != null && GridTargetingAIs.TryGetValue(grid, out gridAi))
                        {
                            if (MyCubeBuilder.Static.CurrentBlockDefinition != null)
                            {
                                var subtypeIdHash = MyCubeBuilder.Static.CurrentBlockDefinition.Id.SubtypeId;
                                GridAi.WeaponCount weaponCount;
                                if (gridAi.WeaponCounter.TryGetValue(subtypeIdHash, out weaponCount))
                                {
                                    if (weaponCount.Current >= weaponCount.Max && weaponCount.Max > 0)
                                    {
                                        MyCubeBuilder.Static.NotifyPlacementUnable();
                                        MyCubeBuilder.Static.Deactivate();
                                    }
                                }
                            }
                        }
                    }
                }
                if(!DedicatedServer)//todo client side only
                    ProcessAnimations();

                if (!PastedBlocksToInit.IsEmpty)
                {
                    MyCubeGrid grid;
                    while (PastedBlocksToInit.TryDequeue(out grid))
                    {
                        foreach (var block in grid.GetFatBlocks())
                        {
                            var weaponBase = block as IMyLargeTurretBase;
                            if (weaponBase == null) continue;

                            WeaponComponent comp;
                            if ((block.Components.TryGet(out comp) && comp.MyGrid.EntityId != block.CubeGrid.EntityId))
                            {
                                Log.Line("comp found");

                                CompsToRemove.Enqueue(comp);

                                OnEntityCreate(block);
                            }
                            else if (comp == null)
                            {
                                Log.Line("comp not found");

                                OnEntityCreate(block);
                            }
                        }
                        
                    }
                }

                if (!CompsToRemove.IsEmpty)
                {
                    WeaponComponent weaponComp;
                    while (CompsToRemove.TryDequeue(out weaponComp))
                        weaponComp.RemoveComp();
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
                    /*
                    if (LcdEntity1 != null)
                    {
                        var cameraMatrix = Camera.WorldMatrix;
                        cameraMatrix.Translation += (Camera.WorldMatrix.Forward * 0.1f);
                        //cameraMatrix.Translation += (Camera.WorldMatrix.Left * 0.075f);
                        //cameraMatrix.Translation += (Camera.WorldMatrix.Up * 0.05f);

                        LcdEntity1.WorldMatrix = cameraMatrix;
                        if (LcdPanel1 != null && LcdPanel1.InScene)
                        {
                            if (!_initLcdPanel1)
                            {
                                if (LcdPanel1.IsFunctional && LcdPanel1.IsWorking)
                                {
                                    LcdPanel1.FontSize = 2;
                                    _initLcdPanel1 = true;
                                    LcdPanel1.ResourceSink.SetMaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId, 0);
                                    LcdPanel1.WriteText("test1");
                                }
                            }
                        }
                    }
                    */
                    if (Ui.WheelActive && !MyAPIGateway.Session.Config.MinimalHud && !MyAPIGateway.Gui.IsCursorVisible)
                        Ui.DrawWheel();

                    Pointer.DrawSelector();

                    for (int i = 0; i < Projectiles.Wait.Length; i++)
                        lock (Projectiles.Wait[i])
                            DrawLists(Projectiles.DrawProjectiles[i]);

                    if (_shrinking.Count > 0)
                        Shrink();

                    if (_afterGlow.Count > 0)
                        AfterGlow();
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
                MyEntities.OnEntityAdd += OnEntityAdded;
                MyAPIGateway.Gui.GuiControlCreated += MenuOpened;
                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);
                AllDefinitions = Static.GetAllDefinitions();
                SoundDefinitions = Static.GetSoundDefinitions();

                var weapons = new Weapons();
                var WeaponDefinitions = weapons.ReturnDefs();
                for (int i = 0; i < WeaponDefinitions.Length; i++)
                {
                    WeaponDefinitions[i].ModPath = ModContext.ModPath;
                    _weaponDefinitions.Add(WeaponDefinitions[i]);
                }

                ModelIdToName.Add(ModelCount, ModContext.ModPath + "\\Models\\Environment\\JumpNullField.mwm");
                ModelCount++;

                FixPrefabs();
            }
            catch (Exception ex) { Log.Line($"Exception in LoadData: {ex}"); }
        }

        protected override void UnloadData()
        {
            PurgeAllEffects();
            SApi.Unload();

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PACKET_ID, ReceivedPacket);
            MyAPIGateway.Utilities.UnregisterMessageHandler(7771, Handler);

            MyEntities.OnEntityCreate -= OnEntityCreate;
            MyEntities.OnEntityAdd -= OnEntityAdded;
            MyAPIGateway.Gui.GuiControlCreated -= MenuOpened;
            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;
            ProjectileTree.Clear();

            //Session.Player.Character.ControllerInfo.ControlReleased -= PlayerControlReleased;
            //Session.Player.Character.ControllerInfo.ControlAcquired -= PlayerControlAcquired;
            AllDefinitions = null;
            SoundDefinitions = null;

            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
}

