using System;
using System.Collections;
using Sandbox.Engine.Networking;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Input;
using VRage.ModAPI;
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
                   // Log.LineShortDate($"<Acq>{acquire.Median:0.0000}/{acquire.Min:0.0000}/{acquire.Max:0.0000} <DM>{damageTime.Median:0.0000}/{damageTime.Min:0.0000}/{damageTime.Max:0.0000} <DR>{drawTime.Median:0.0000}/{drawTime.Min:0.0000}/{drawTime.Max:0.0000} <AI>{ai.Median:0.0000}/{ai.Min:0.0000}/{ai.Max:0.0000} <SH>{updateTime.Median:0.0000}/{updateTime.Min:0.0000}/{updateTime.Max:0.0000} <CH>{charge.Median:0.0000}/{charge.Min:0.0000}/{charge.Max:0.0000} <PR>{projectileTime.Median:0.0000}/{projectileTime.Min:0.0000}/{projectileTime.Max:0.0000} <DB>{db.Median:0.0000}/{db.Min:0.0000}/{db.Max:0.0000}> AiReq:[{TargetRequests}] Targ:[{TargetChecks}] Bloc:[{BlockChecks}] Aim:[{CanShoot}] CCast:[{ClosestRayCasts}] RndCast[{RandomRayCasts}] TopCast[{TopRayCasts}]");
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

        private bool _inSpyMode = false;
        public override void Simulate()
        {
            try
            {
                /*
                if (TargetUi.DrawReticle)
                {
                    var playerController = MyAPIGateway.Session.ControlledObject;
                    if (playerController == null)
                    {
                        Log.Line($"player controller null");
                        return;
                    }
                    var controllerTopMost = playerController.Entity.GetTopMostParent();
                    if (controllerTopMost == null)
                    {
                        Log.Line($"controller topmost null");
                        return;
                    }
                    var velocity = controllerTopMost.Physics.LinearVelocity * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
                    var rotation = playerController.LastRotationIndicator;

                    if (!Vector3D.IsZero(velocity, 1e-4) || !Vector3D.IsZero(rotation, 1e-4))
                    {
                        Log.Line($"set SpyCam matrix: {SpyCam.IsActive} - {TargetUi.AimMatrix.Translation} - {TargetUi.AimMatrix.Forward} - {velocity.Length()} - {rotation}");
                        var playerPosition = controllerTopMost.PositionComp.WorldAABB.Center;
                        var rotationMultiplier = 0.0025f * 1.04f;
                        MyAPIGateway.Session.SetCameraController(MyCameraControllerEnum.SpectatorFixed, controllerTopMost);
                        var roll = MathHelper.Clamp(rotation.Z, -0.02f, 0.02f);
                        var vec2 = new Vector2(rotation.X, rotation.Y) * rotationMultiplier;

                        var matrix = MySpectator.Static.GetViewMatrix();
                        matrix.Translation -= Vector3D.TransformNormal(velocity, matrix);
                        var quat = Quaternion.CreateFromYawPitchRoll(vec2.Y, vec2.X, roll);
                        MatrixD.Transform(ref matrix, ref quat, out matrix);

                        matrix = MatrixD.Invert(matrix);
                        matrix.Translation = playerPosition + Vector3D.TransformNormal(Vector3D.Backward * 10, matrix);
                        MySpectator.Static.SetViewMatrix(MatrixD.Invert(ref matrix));
                    }

                    _inSpyMode = true;
                    //var matrix2 = ActiveControlBlock.PositionComp.WorldMatrix;
                    //matrix2.Translation += matrix2.Forward * 50;
                    //SpyCam.PositionComp.SetWorldMatrix(matrix2, SpyCam.CubeGrid, false, false);
                    //SpyCam.RequestSetView();
                }
                else if (_inSpyMode)
                {

                    Log.Line("leave spy");
                    MyAPIGateway.Session.SetCameraController(MyCameraControllerEnum.Entity, (IMyEntity) ActiveControlBlock ?? MyAPIGateway.Session.Player.Character);
                    _inSpyMode = false;
                }
                */
                if (!DedicatedServer)
                {
                    EntityControlUpdate();
                    CameraMatrix = Session.Camera.WorldMatrix;
                    CameraPos = CameraMatrix.Translation;
                }
                if (Loaded)
                {
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
                if (!WheelUi.WheelActive && !InMenu)
                {
                    UpdateLocalAiAndCockpit();
                    if (UiInput.PlayerCamera && ActiveCockPit != null) 
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
                if ((UiInput.PlayerCamera || UiInput.FirstPersonView || UiInput.InSpyCam) && !InMenu && !Session.Config.MinimalHud && !MyAPIGateway.Gui.IsCursorVisible)
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
            if(!DedicatedServer)
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
                MultiplayerId = MyAPIGateway.Multiplayer.MyId;

                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);

                foreach (var mod in Session.Mods)
                {
                    if (mod.PublishedFileId == 1365616918) ShieldMod = true;
                    else if (mod.PublishedFileId == 1931509062 || mod.PublishedFileId == 1995197719) ReplaceVanilla = true;
                    else if (mod.GetPath().Contains("AppData\\Roaming\\SpaceEngineers\\Mods\\VanillaReplacement"))
                        ReplaceVanilla = true;
                }

                TriggerEntityModel = ModContext.ModPath + "\\Models\\Environment\\JumpNullField.mwm";
                TriggerEntityPool = new MyConcurrentPool<MyEntity>(0, TriggerEntityClear, 10000, TriggerEntityActivator);
                /*
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
                            foreach (var cube in hacked.Definitions)
                            {
                            }
                        }
                    }
                }
                */
            }
            catch (Exception ex) { Log.Line($"Exception in LoadData: {ex}"); }
        }

        protected override void UnloadData()
        {
            try
            {
                if (IsServer || DedicatedServer)
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(ServerPacketId, ServerReceivedPacket);
                else
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(ClientPacketId, ClientReceivedPacket);

                MyAPIGateway.Utilities.UnregisterMessageHandler(7771, Handler);

                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlHandler;
                MyEntities.OnEntityCreate -= OnEntityCreate;
                MyAPIGateway.Gui.GuiControlCreated -= MenuOpened;
                MyAPIGateway.Gui.GuiControlRemoved -= MenuClosed;

                MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
                MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;
                //MyVisualScriptLogicProvider.ToolbarItemChanged -= RemoveAction;
                ApiServer.Unload();

                PurgeAll();

                Log.Line("Logging stopped.");
                Log.Close();
            }
            catch (Exception ex) { Log.Line($"Exception in UnloadData: {ex}"); }
        }
    }
}

