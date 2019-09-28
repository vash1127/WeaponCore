using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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
                if (!Inited) lock (_configLock) Init();
                foreach (var ent in BlocksToInit) {
                    OnEntityCreate(ent);
                }
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
                if (Tick60)
                {
                    foreach (var ge in _gridEffects)
                    {
                        foreach (var v in ge.Value)
                        {
                            GetCubesForEffect(ge.Key, v.Value.HitPos, v.Key, _tmpEffectCubes);
                            ComputeEffects(v.Value.System, ge.Key, v.Value.Damage * v.Value.Hits, float.MaxValue, v.Value.AttackerId, _tmpEffectCubes);
                            _tmpEffectCubes.Clear();
                            v.Value.Clean();
                            GridEffectPool.Return(v.Value);
                        }
                        ge.Value.Clear();
                        GridEffectsPool.Return(ge.Value);
                    }
                    _gridEffects.Clear();
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

                if (!CompsToStart.IsEmpty)
                {
                    WeaponComponent weaponComp;
                    CompsToStart.TryDequeue(out weaponComp);

                    if (weaponComp.MyGrid.EntityId != weaponComp.MyCube.CubeGrid.EntityId)
                    {
                        Log.Line("comp found");

                        CompsToRemove.Enqueue(weaponComp);

                        OnEntityCreate(weaponComp.MyCube);
                    }
                    else{

                        weaponComp.MyCube.Components.Add(weaponComp);
                        weaponComp.OnAddedToScene();
                        weaponComp.Ai.FirstRun = true;
                        Log.Line($"added to comp");
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
                //MyEntities.OnEntityAdd += OnEntityAdded;
                MyAPIGateway.Gui.GuiControlCreated += MenuOpened;
                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);
                AllDefinitions = Static.GetAllDefinitions();
                SoundDefinitions = Static.GetSoundDefinitions();

                var weapons = new Weapons();
                var weaponDefinitions = weapons.ReturnDefs();
                for (int i = 0; i < weaponDefinitions.Length; i++)
                {
                    weaponDefinitions[i].ModPath = ModContext.ModPath;
                    _weaponDefinitions.Add(weaponDefinitions[i]);
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
            //MyEntities.OnEntityAdd -= OnEntityAdded;
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

