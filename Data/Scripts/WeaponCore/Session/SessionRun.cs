using System;
using System.Collections;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
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

        public override void UpdatingStopped()
        {
            try
            {
                Log.Line($"Paused:{Tick}");
                Paused();
                _paused = true;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdatingStopped: {ex}"); }
        }


        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();
                if (!WeaponAmmoPullQueue.IsEmpty && ITask.IsComplete)
                {
                    if (ITask.valid && ITask.Exceptions != null)
                        TaskHasErrors(ref ITask, "ITask");

                    ITask = MyAPIGateway.Parallel.StartBackground(AmmoPull, MoveAmmo);
                }

                if (!CompsToStart.IsEmpty)
                    StartComps();

                if (Tick120 && CompsDelayed.Count > 0)
                    DelayedComps();

                if (CompReAdds.Count > 0)
                    ChangeReAdds();
                
                if (Tick180)
                {
                    HighLoad = false;
                    var projectileTime = DsUtil.GetValue("projectiles");
                    var updateTime = DsUtil.GetValue("shoot");
                    var damageTime = DsUtil.GetValue("damage");
                    var drawTime = DsUtil.GetValue("draw");
                    var db = DsUtil.GetValue("db");
                    var ai = DsUtil.GetValue("ai");
                    var charge = DsUtil.GetValue("charge");
                    var acquire = DsUtil.GetValue("acquire");
                    Log.LineShortDate($"<Acq>{acquire.Median:0.0000}/{acquire.Min:0.0000}/{acquire.Max:0.0000} <DM>{damageTime.Median:0.0000}/{damageTime.Min:0.0000}/{damageTime.Max:0.0000} <DR>{drawTime.Median:0.0000}/{drawTime.Min:0.0000}/{drawTime.Max:0.0000} <AI>{ai.Median:0.0000}/{ai.Min:0.0000}/{ai.Max:0.0000} <SH>{updateTime.Median:0.0000}/{updateTime.Min:0.0000}/{updateTime.Max:0.0000} <CH>{charge.Median:0.0000}/{charge.Min:0.0000}/{charge.Max:0.0000} <PR>{projectileTime.Median:0.0000}/{projectileTime.Min:0.0000}/{projectileTime.Max:0.0000} <DB>{db.Median:0.0000}/{db.Min:0.0000}/{db.Max:0.0000}> AiReq:[{TargetRequests}] Targ:[{TargetChecks}] Bloc:[{BlockChecks}] Aim:[{CanShoot}] CCast:[{ClosestRayCasts}] RndCast[{RandomRayCasts}] TopCast[{TopRayCasts}]");
                    TargetRequests = 0;
                    TargetChecks = 0;
                    BlockChecks = 0;
                    CanShoot = 0;
                    ClosestRayCasts = 0;
                    RandomRayCasts = 0;
                    TopRayCasts = 0;
                    TargetTransfers = 0;
                    TargetSets = 0;
                    TargetResets = 0;
                    AmmoMoveTriggered = 0;
                    AmmoPulls = 0;
                    Load = 0d;
                    DsUtil.Clean();
                }
                FutureEvents.Tick(Tick);
                if (UiInput.PlayerCamera && !InMenu) WheelUi.UpdatePosition();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void Simulate()
        {
            try
            {
                if (!DedicatedServer)
                {
                    EntityControlUpdate();
                    CameraMatrix = Session.Camera.WorldMatrix;
                    CameraPos = CameraMatrix.Translation;
                }

                DsUtil.Start("ai");
                if (GameLoaded) AiLoop();
                DsUtil.Complete("ai", true);

                DsUtil.Start("charge");
                if (ChargingWeapons.Count > 0) UpdateChargeWeapons();
                DsUtil.Complete("charge", true);

                DsUtil.Start("acquire");
                if (AcquireTargets.Count > 0) CheckAcquire();
                DsUtil.Complete("acquire", true);

                DsUtil.Start("shoot");
                if (ShootingWeapons.Count > 0) ShootWeapons();
                DsUtil.Complete("shoot", true);
                if (!WheelUi.WheelActive && !InMenu)
                {
                    UpdateLocalAiAndCockpit();
                    if (UiInput.PlayerCamera) 
                        TargetSelection();
                }
                PTask = MyAPIGateway.Parallel.StartBackground(Projectiles.Update);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionSim: {ex}"); }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (Placer != null) UpdatePlacer();
                if (!DedicatedServer)
                    ProcessAnimations();

                DsUtil.Start("projectiles");

                if (!PTask.IsComplete)
                    PTask.Wait();

                if (PTask.IsComplete && PTask.valid && PTask.Exceptions != null)
                    TaskHasErrors(ref PTask, "PTask");

                DsUtil.Complete("projectiles", true);

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

                if (GridTask.IsComplete)
                    CheckDirtyGrids();

                DsUtil.Start("damage");
                if (Hits.Count > 0) ProcessHits();
                DsUtil.Complete("damage", true);

            }
            catch (Exception ex) { Log.Line($"Exception in SessionAfterSim: {ex}"); }
        }

        public override void Draw()
        {
            try
            {
                if (_lastDrawTick == Tick || _paused)return;
                _lastDrawTick = Tick;
                DsUtil.Start("draw");
                CameraMatrix = Session.Camera.WorldMatrix;
                CameraPos = CameraMatrix.Translation;
                CameraFrustrum.Matrix = (Camera.ViewMatrix * Camera.ProjectionMatrix);

                if ((UiInput.PlayerCamera || UiInput.FirstPersonView) && !InMenu && !Session.Config.MinimalHud && !MyAPIGateway.Gui.IsCursorVisible)
                {
                    if (WheelUi.WheelActive) WheelUi.DrawWheel();
                    TargetUi.DrawTargetUi();
                }

                Av.Run();
                DsUtil.Complete("draw", true);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }


        public override void HandleInput()
        {
            UiInput.UpdateInputState();
        }

        public override void LoadData()
        {
            try
            {
                AllDefinitions = Static.GetAllDefinitions();
                SoundDefinitions = Static.GetSoundDefinitions();
                MyEntities.OnEntityCreate += OnEntityCreate;
                MyAPIGateway.Gui.GuiControlCreated += MenuOpened;
                MyAPIGateway.Gui.GuiControlRemoved += MenuClosed;

                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);
                IsCreative = MyAPIGateway.Session.CreativeMode;

                foreach (var mod in Session.Mods)
                {
                    if (mod.PublishedFileId == 1365616918) ShieldMod = true;
                    else if (mod.PublishedFileId == 1931509062) ReplaceVanilla = true;
                }

                //if (ModContext.ModPath.Contains("AppData\\Roaming\\SpaceEngineers\\Mods\\WeaponCore")) 
                    ReplaceVanilla = true;

                TriggerEntityModel = ModContext.ModPath + "\\Models\\Environment\\JumpNullField.mwm";
                TriggerEntityPool = new MyConcurrentPool<MyEntity>(0, TriggerEntityClear, 10000, TriggerEntityActivator);

                var list = Static.GetAllSessionPreloadObjectBuilders();
                var comparer = new HackEqualityComparer();
                for (int i = 0; i < list.Count; i++)
                {
                    var tuple = (IStructuralEquatable)list[i];
                    if (tuple != null)
                    {
                        tuple.GetHashCode(comparer);
                        var hacked = comparer.Def;
                        if (hacked?.CubeBlocks != null)
                        {
                            foreach (var cube in hacked.CubeBlocks)
                            {
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in LoadData: {ex}"); }
        }

        protected override void UnloadData()
        {
            try
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketId, ReceivedPacket);
                MyAPIGateway.Utilities.UnregisterMessageHandler(7771, Handler);
                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlHandler;
                MyEntities.OnEntityCreate -= OnEntityCreate;
                MyAPIGateway.Gui.GuiControlCreated -= MenuOpened;
                MyAPIGateway.Gui.GuiControlRemoved -= MenuClosed;

                MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
                MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;
                MyVisualScriptLogicProvider.ToolbarItemChanged -= RemoveAction;
                ApiServer.Unload();

                PurgeAll();

                Log.Line("Logging stopped.");
                Log.Close();
            }
            catch (Exception ex) { Log.Line($"Exception in UnloadData: {ex}"); }
        }
    }
}

