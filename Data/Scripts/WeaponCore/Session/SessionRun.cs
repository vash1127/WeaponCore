using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
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
                BeforeStartInit();
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }


        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();
                DsUtil.Start("");
                _futureEvents.Tick(Tick);
                DsUtil.Complete("events", true);
                Ui.UpdateInput();
                DsUtil.Start("");
                if (!Hits.IsEmpty) ProcessHits();
                DsUtil.Complete("damage", true);
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
                    var lastControlledEnt = ControlledEntity;
                    ControlledEntity = (MyEntity)MyAPIGateway.Session.ControlledObject;
                    WeaponComponent notNeeded;
                    ControlChanged = lastControlledEnt != ControlledEntity && ControlledEntity.Components.TryGet(out notNeeded);
                    
                    CameraPos = Session.Camera.Position;
                    ProcessAnimationQueue();
                }

                DsUtil.Start("");
                AiLoop();
                UpdateWeaponPlatforms();
                DsUtil.Complete("update", true);

                DsUtil.Start("");
                Projectiles.Update();
                DsUtil.Complete("projectiles", true);

                DsUtil.Start("");
                if (_effectedCubes.Count > 0) ApplyEffect();
                if (Tick60)
                {
                    foreach (var ge in _gridEffects)
                    {
                        foreach (var v in ge.Value)
                        {
                            GetCubesForEffect(v.Value.Ai, ge.Key, v.Value.HitPos, v.Key, _tmpEffectCubes);
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
                DsUtil.Complete("effects", true);

                if (MyAPIGateway.Input.IsNewLeftMouseReleased())
                    Pointer.SelectTarget();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionSim: {ex}"); }
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
                if (Placer != null) UpdatePlacer();
                DsUtil.Start("");
                if (!DedicatedServer)//todo client side only
                    ProcessAnimations();
                DsUtil.Complete("animations", true);

                if (!CompsToStart.IsEmpty) StartComps();

                if (!CompsToRemove.IsEmpty) RemoveComps();

            }
            catch (Exception ex) { Log.Line($"Exception in SessionAfterSim: {ex}"); }
        }

        public override void Draw()
        {
            try
            {
                DsUtil.Start("");
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

                    if (_afterGlow.Count > 0)
                        AfterGlow();
                }
                DsUtil.Complete("draw", true);
                if (Tick300)
                {
                    var threshold = Projectiles.Wait.Length * 10;
                    HighLoad = Load > threshold;
                    Log.Line($"[Load:{Load:0.00}({threshold}) - Mp:{HighLoad}] [Projectiles:{DsUtil.GetValue("projectiles")}] [Update:{DsUtil.GetValue("update")}] [Damage:{DsUtil.GetValue("damage")}] [Draw:{DsUtil.GetValue("draw")}] [Dbs:{DsUtil.GetValue("db")}] [Effects:{DsUtil.GetValue("effects")}] [Events:{DsUtil.GetValue("events")}] [Anim:{DsUtil.GetValue("animations")}]");
                    Load = 0d;
                    DsUtil.Clean();
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

