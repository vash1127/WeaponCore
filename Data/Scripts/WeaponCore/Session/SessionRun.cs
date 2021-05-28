using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Data.Scripts.WeaponCore.Support.Api;
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
                if (!SuppressWc)
                    BeforeStartInit();
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }

        public override void UpdatingStopped()
        {
            try
            {
                if (!SuppressWc)
                    Paused();

            }
            catch (Exception ex) { Log.Line($"Exception in UpdatingStopped: {ex}"); }
        }


        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (SuppressWc)
                    return;

                if (DeformProtection.Count > 0 && Tick - LastDeform > 0)
                    DeformProtection.Clear();
                
                Timings();

                if (IsClient)  {

                    if (ClientSideErrorPkt.Count > 0)
                        ReproccessClientErrorPackets();

                    if (ClientPacketsToClean.Count > 0)
                        CleanClientPackets();
                }

                if (IsServer) {
                    if (Tick60) AcqManager.Observer();
                    if (Tick600) AcqManager.ReorderSleep();
                }

                if (!DedicatedServer && TerminalMon.Active)
                    TerminalMon.Monitor();

                MyCubeBlock cube;
                if (Tick60 && UiInput.ControlKeyPressed && UiInput.CtrlPressed && GetAimedAtBlock(out cube) && cube.BlockDefinition != null && WeaponCoreBlockDefs.ContainsKey(cube.BlockDefinition.Id.SubtypeName))
                    ProblemRep.GenerateReport(cube);

                if (!IsClient && !InventoryUpdate && WeaponToPullAmmo.Count > 0 && ITask.IsComplete)
                    StartAmmoTask();

                if (!CompsToStart.IsEmpty)
                    StartComps();

                if (Tick120 && CompsDelayed.Count > 0)
                    DelayedComps();

                if (Tick20 && !DelayedAiClean.IsEmpty)
                    DelayedAiCleanup();

                if (CompReAdds.Count > 0)
                    ChangeReAdds();

                if (Tick3600 && MpActive) 
                    NetReport();

                if (Tick180) 
                    ProfilePerformance();

                FutureEvents.Tick(Tick);

                if (HomingWeapons.Count > 0)
                    UpdateHomingWeapons();


                if (MpActive) {
                    if (PacketsToClient.Count > 0 || PrunedPacketsToClient.Count > 0)
                        ProccessServerPacketsForClients();
                    if (PacketsToServer.Count > 0)
                        ProccessClientPacketsForServer();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void Simulate()
        {
            try
            {
                if (SuppressWc)
                    return;

                if (!DedicatedServer) {
                    EntityControlUpdate();
                    CameraMatrix = Session.Camera.WorldMatrix;
                    CameraPos = CameraMatrix.Translation;
                    PlayerPos = Session.Player?.Character?.WorldAABB.Center ?? Vector3D.Zero;
                }

                if (GameLoaded) {
                    DsUtil.Start("ai");
                    AiLoop();
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

                }

                if (!DedicatedServer && !InMenu) {
                    UpdateLocalAiAndCockpit();
                    if (UiInput.PlayerCamera && ActiveCockPit != null || ActiveControlBlock is MyRemoteControl && !UiInput.PlayerCamera || UiInput.CameraBlockView) 
                        TargetSelection();
                }

                DsUtil.Start("ps");
                Projectiles.SpawnAndMove();
                DsUtil.Complete("ps", true);

                DsUtil.Start("pi");
                Projectiles.Intersect();
                DsUtil.Complete("pi", true);

                DsUtil.Start("pd");
                Projectiles.Damage();
                DsUtil.Complete("pd", true);

                DsUtil.Start("pa");
                Projectiles.AvUpdate();
                DsUtil.Complete("pa", true);

                DsUtil.Start("av");
                if (!DedicatedServer) Av.End();
                DsUtil.Complete("av", true);

                if (MpActive)  {
                    
                    DsUtil.Start("network1");
                    if (PacketsToClient.Count > 0 || PrunedPacketsToClient.Count > 0) 
                        ProccessServerPacketsForClients();
                    if (PacketsToServer.Count > 0) 
                        ProccessClientPacketsForServer();

                    if (EwarNetDataDirty)
                        SendEwaredBlocks();

                    DsUtil.Complete("network1", true);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionSim: {ex}"); }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (SuppressWc)
                    return;

                if (Placer != null) UpdatePlacer();

                if (AnimationsToProcess.Count > 0 || ThreadedAnimations.Count > 0) ProcessAnimations();

                if (GridTask.IsComplete)
                    CheckDirtyGridInfos();
                
                if (!DirtyPowerGrids.IsEmpty)
                    UpdateGridPowerState();

                if (WaterApiLoaded && (Tick3600 || WaterMap.IsEmpty))
                    UpdateWaters();

                if (HandlesInput && Tick60)
                    UpdatePlayerPainters();

                if (DebugLos && Tick1800) {
                    var averageMisses = RayMissAmounts > 0 ? RayMissAmounts / Rays : 0; 
                    Log.Line($"RayMissAverage: {averageMisses} - tick:{Tick}");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionAfterSim: {ex}"); }
        }

        public override void Draw()
        {
            try
            {
                if (SuppressWc || DedicatedServer || _lastDrawTick == Tick || _paused) return;
                
                if (DebugLos)
                    LosDebuging();

                _lastDrawTick = Tick;
                DsUtil.Start("draw");
                CameraMatrix = Session.Camera.WorldMatrix;
                CameraPos = CameraMatrix.Translation;
                CameraFrustrum.Matrix = (Camera.ViewMatrix * Camera.ProjectionMatrix);
                var newFov = Camera.FovWithZoom;
                
                if (!MyUtils.IsEqual(newFov, CurrentFovWithZoom))
                    FovChanged();

                CurrentFovWithZoom = newFov;
                AspectRatio = Camera.ViewportSize.X / Camera.ViewportSize.Y;
                AspectRatioInv = Camera.ViewportSize.Y / Camera.ViewportSize.X;

                ScaleFov = Math.Tan(CurrentFovWithZoom * 0.5);

                if (!Session.Config.MinimalHud && InGridAiBlock) {

                    if (HudUi.TexturesToAdd > 0 || HudUi.KeepBackground) 
                        HudUi.DrawTextures();

                    if ((UiInput.PlayerCamera || UiInput.FirstPersonView || UiInput.CameraBlockView) && !InMenu && !MyAPIGateway.Gui.IsCursorVisible)
                        TargetUi.DrawTargetUi();

                    if (HudUi.AgingTextures)
                        HudUi.DrawText();
                }

                Av.Run();
                DrawDisabledGuns();
                DsUtil.Complete("draw", true);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void HandleInput()  
        {
            if (HandlesInput && !SuppressWc) {

                if (ControlRequest != ControlQuery.None)
                    UpdateControlKeys();

                UiInput.UpdateInputState();
                if (MpActive)  {

                    if (UiInput.InputChanged && ActiveControlBlock != null)
                    {
                        SendMouseUpdate(TrackingAi, ActiveControlBlock);
                    }

                    if (TrackingAi != null && TargetUi.DrawReticle)  {
                        var dummyTargets = PlayerDummyTargets[PlayerId];

                        if (dummyTargets.ManualTarget.LastUpdateTick == Tick)
                            SendAimTargetUpdate(TrackingAi, dummyTargets.ManualTarget);

                        if (dummyTargets.PaintedTarget.LastUpdateTick == Tick)
                            SendPaintedTargetUpdate(TrackingAi, dummyTargets.PaintedTarget);
                    }

                    if (PacketsToServer.Count > 0)
                        ProccessClientPacketsForServer();
                }

                if (Tick60 && SoundsToClean.Count > 0)
                    CleanSounds();
            }
        }

        public override void LoadData()
        {
            try
            {
                foreach (var mod in Session.Mods)
                {
                    if (mod.PublishedFileId == 1365616918 || mod.PublishedFileId == 2189703321) ShieldMod = true;
                    else if (mod.GetPath().Contains("AppData\\Roaming\\SpaceEngineers\\Mods\\DefenseShields"))
                        ShieldMod = true;
                    else if (mod.PublishedFileId == 1931509062 || mod.PublishedFileId == 1995197719 || mod.PublishedFileId == 2006751214 || mod.PublishedFileId == 2015560129)
                        ReplaceVanilla = true;
                    else if (mod.GetPath().Contains("AppData\\Roaming\\SpaceEngineers\\Mods\\VanillaReplacement"))
                        ReplaceVanilla = true;
                    else if (mod.PublishedFileId == 2123506303 || mod.PublishedFileId == 2496225055)
                    {
                        if (mod.Name != ModContext.ModId)
                            SuppressWc = true;
                    }
                    else if (mod.PublishedFileId == 2200451495)
                        WaterMod = true;
                }

                if (SuppressWc)
                    return;

                AllDefinitions = Static.GetAllDefinitions();
                SoundDefinitions = Static.GetSoundDefinitions();
                MyEntities.OnEntityCreate += OnEntityCreate;
                MyAPIGateway.Gui.GuiControlCreated += MenuOpened;
                MyAPIGateway.Gui.GuiControlRemoved += MenuClosed;

                MyAPIGateway.Utilities.RegisterMessageHandler(7773, ArmorHandler);
                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);

                TriggerEntityModel = ModContext.ModPath + "\\Models\\Environment\\JumpNullField.mwm";
                TriggerEntityPool = new MyConcurrentPool<MyEntity>(0, TriggerEntityClear, 10000, TriggerEntityActivator);

                ReallyStupidKeenShit();
            }
            catch (Exception ex) { Log.Line($"Exception in LoadData: {ex}"); }
        }

        protected override void UnloadData()
        {
            try
            {
                if (SuppressWc)
                    return;

                if (!PTask.IsComplete)
                    PTask.Wait();

                if (!CTask.IsComplete)
                    CTask.Wait();

                if (!ITask.IsComplete)
                    ITask.Wait();

                if (IsServer || DedicatedServer)
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(ServerPacketId, ProccessServerPacket);
                else
                {
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(ClientPacketId, ClientReceivedPacket);
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(StringPacketId, StringReceived);
                }

                if (HandlesInput)
                    MyAPIGateway.Utilities.MessageEntered -= ChatMessageSet;

                MyAPIGateway.Utilities.UnregisterMessageHandler(7771, Handler);

                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlHandler;

                MyEntities.OnEntityCreate -= OnEntityCreate;
                MyAPIGateway.Gui.GuiControlCreated -= MenuOpened;
                MyAPIGateway.Gui.GuiControlRemoved -= MenuClosed;

                MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
                MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;
                ApiServer.Unload();

                PurgeAll();

                Log.Line("Logging stopped.");
                Log.Close();
            }
            catch (Exception ex) { Log.Line($"Exception in UnloadData: {ex}"); }
        }
    }
}

