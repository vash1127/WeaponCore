﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using WeaponCore.Data.Scripts.CoreSystems.Ui.Targeting;
using static CoreSystems.Support.Ai;
using static CoreSystems.Support.WeaponDefinition.TargetingDef;
using static CoreSystems.Support.WeaponDefinition.TargetingDef.BlockTypes;
using static CoreSystems.ProtoWeaponState;
namespace CoreSystems
{
    public partial class Session
    {
        internal bool UpdateLocalAiAndCockpit()
        {
            InGridAiBlock = false;
            ActiveControlBlock = ControlledEntity as MyCubeBlock;
            PlayerHandWeapon =  Session.Player?.Character?.EquippedTool as IMyAutomaticRifleGun;
            var cockPit = ControlledEntity as MyCockpit;
            if (cockPit != null && cockPit.EnableShipControl)
                ActiveCockPit = cockPit;
            else ActiveCockPit = null;


            long oldControlId;
            var controlledEntity = ActiveCockPit ?? ActiveControlBlock ?? PlayerHandWeapon?.Owner;
            if (controlledEntity != null && EntityToMasterAi.TryGetValue(ActiveControlBlock != null ? controlledEntity.GetTopMostParent() : controlledEntity, out TrackingAi))
            {
                var camera = Session.CameraController?.Entity as MyCameraBlock;
                if (camera == null || !GroupedCamera(camera))
                    ActiveCameraBlock = null;
                InGridAiBlock = true;
                TrackingAi.Data.Repo.ControllingPlayers.TryGetValue(PlayerId, out oldControlId);

                if (oldControlId != controlledEntity.EntityId)
                {
                    SendActiveControlUpdate(TrackingAi, controlledEntity, true);
                    TargetLeadUpdate();
                }
                else if (LeadGroupsDirty || !MyUtils.IsEqual(LastOptimalDps, TrackingAi.Construct.OptimalDps))
                    TargetLeadUpdate();
            }
            else
            {
                if (TrackingAi != null)
                {
                    TrackingAi.Construct.Focus.ClientIsFocused(TrackingAi);

                    MyCubeBlock oldBlock;
                    if (TrackingAi.Data.Repo.ControllingPlayers.TryGetValue(PlayerId, out oldControlId) && MyEntities.TryGetEntityById(oldControlId, out oldBlock, true))
                    {

                        if (IsServer) TrackingAi.Construct.UpdateConstructsPlayers(controlledEntity, PlayerId, false);

                        SendActiveControlUpdate(TrackingAi, oldBlock, false);
                        foreach (var list in LeadGroups) list.Clear();
                        LeadGroupActive = false;
                    }
                }

                TrackingAi = null;
                ActiveCockPit = null;
                ActiveControlBlock = null;
                ActiveCameraBlock = null;
            }
            return InGridAiBlock;
        }

        private void TargetLeadUpdate()
        {
            LeadGroupActive = false;
            LeadGroupsDirty = false;

            LastOptimalDps = TrackingAi.Construct.OptimalDps;

            foreach (var list in LeadGroups)
                list.Clear();
            
            if (Settings.Enforcement.DisableLeads)
                return;

            foreach (var ai in TrackingAi.Construct.RefreshedAis)
            {
                foreach (var comp in ai.WeaponComps)
                {
                    var collection = comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? comp.Platform.Weapons : comp.Platform.Phantoms;
                    foreach (var w in collection)
                    {
                        if ((!w.Comp.HasTurret && !w.Comp.OverrideLeads || w.Comp.HasTurret && w.Comp.OverrideLeads) && w.Comp.Data.Repo.Values.Set.Overrides.LeadGroup > 0)
                        {
                            LeadGroups[w.Comp.Data.Repo.Values.Set.Overrides.LeadGroup].Add(w);
                            LeadGroupActive = true;
                        }
                    }
                }
            }
        }

        private bool GroupedCamera(MyCameraBlock camera)
        {
            long cameraGroupId;
            if (CameraChannelMappings.TryGetValue(camera, out cameraGroupId))
            {
                ActiveCameraBlock = camera;
                return true;
            }
            ActiveCameraBlock = null;
            return false;
        }


        internal void EntityControlUpdate()
        {
            var lastControlledEnt = ControlledEntity;
            ControlledEntity = (MyEntity)MyAPIGateway.Session.ControlledObject;

            var entityChanged = lastControlledEnt != null && lastControlledEnt != ControlledEntity;

            if (entityChanged)
            {
                if (lastControlledEnt is MyCockpit || lastControlledEnt is MyRemoteControl)
                    PlayerControlAcquired(lastControlledEnt);

                if (ControlledEntity is MyCockpit || ControlledEntity is MyRemoteControl)
                    PlayerControlNotify(ControlledEntity);

                if (ControlledEntity is IMyGunBaseUser && !(lastControlledEnt is IMyGunBaseUser))
                {
                    var topEntity = ControlledEntity.GetTopMostParent();
                    Ai ai;
                    if (topEntity != null && EntityAIs.TryGetValue(topEntity, out ai))
                    {
                        CoreComponent comp;
                        if (ai.CompBase.TryGetValue(ControlledEntity, out comp) && comp.Type == CoreComponent.CompType.Weapon)
                        {
                            GunnerBlackList = true;
                            if (IsServer)
                            {
                                var wComp = ((Weapon.WeaponComponent)comp);
                                wComp.Data.Repo.Values.State.PlayerId = PlayerId;
                                wComp.Data.Repo.Values.State.Control = ControlMode.Camera;
                            }
                            ActiveControlBlock = (MyCubeBlock)ControlledEntity;
                            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, PlayerId);
                            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, PlayerId);
                            var controlStringMenu = MyAPIGateway.Input.GetControl(UiInput.MouseButtonMenu).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMenu, PlayerId);

                            if (HandlesInput && MpActive)
                                SendPlayerControlRequest(comp, PlayerId, ControlMode.Camera);
                        }
                    }
                }
                else if (!(ControlledEntity is IMyGunBaseUser) && lastControlledEnt is IMyGunBaseUser)
                {
                    if (GunnerBlackList)
                    {
                        GunnerBlackList = false;
                        var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, PlayerId, true);
                        var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, PlayerId, true);
                        var controlStringMenu = MyAPIGateway.Input.GetControl(UiInput.MouseButtonMenu).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMenu, PlayerId, true);
                        var oldCube = lastControlledEnt as MyCubeBlock;
                        Ai ai;
                        if (oldCube != null && EntityAIs.TryGetValue(oldCube.CubeGrid, out ai))
                        {
                            CoreComponent comp;
                            if (ai.CompBase.TryGetValue(oldCube, out comp) && comp.Type == CoreComponent.CompType.Weapon)
                            {
                                if (IsServer)
                                {
                                    var wComp = ((Weapon.WeaponComponent)comp);
                                    wComp.Data.Repo.Values.State.PlayerId = -1;
                                    wComp.Data.Repo.Values.State.Control = ControlMode.None;

                                }

                                if (HandlesInput && MpActive)
                                    SendPlayerControlRequest(comp, -1, ControlMode.None);

                                ActiveControlBlock = null;
                            }
                        }
                    }
                }
            }
        }

        private void FovChanged()
        {
            HudUi.NeedsUpdate = true;
            TargetUi.ResetCache();
        }

        private void ShowClientNotify(ClientNotifyPacket notify)
        {
            MyAPIGateway.Utilities.ShowNotification(notify.Message, notify.Duration > 0 ? notify.Duration : 1000, notify.Color == string.Empty ? "White" : notify.Color);
        }

        internal void ShowLocalNotify(string message, int duration, string color = null)
        {
            MyAPIGateway.Utilities.ShowNotification(message, duration, string.IsNullOrEmpty(color) ? "White" : color);
        }

        private readonly Color _restrictionAreaColor = new Color(128, 0, 128, 96);
        private readonly Color _uninitializedColor = new Color(255, 0, 0, 200);
        private BoundingSphereD _nearbyGridsTestSphere = new BoundingSphereD(Vector3D.Zero, 350);
        private readonly List<MyEntity> _gridsNearCamera = new List<MyEntity>();
        private readonly List<MyCubeBlock> _uninitializedBlocks = new List<MyCubeBlock>();
        private readonly List<Weapon.WeaponComponent> _debugBlocks = new List<Weapon.WeaponComponent>();
        private void DrawDisabledGuns()
        {
            if (Tick600 || Tick60 && QuickDisableGunsCheck)
            {

                QuickDisableGunsCheck = false;
                _nearbyGridsTestSphere.Center = CameraPos;
                _gridsNearCamera.Clear();
                _uninitializedBlocks.Clear();
                _debugBlocks.Clear();

                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref _nearbyGridsTestSphere, _gridsNearCamera);
                for (int i = _gridsNearCamera.Count - 1; i >= 0; i--)
                {
                    var grid = _gridsNearCamera[i] as MyCubeGrid;
                    if (grid?.Physics != null && !grid.MarkedForClose && !grid.IsPreview && !grid.Physics.IsPhantom)
                    {

                        var fatBlocks = grid.GetFatBlocks();
                        for (int j = 0; j < fatBlocks.Count; j++)
                        {

                            var block = fatBlocks[j];
                            if (block.IsFunctional && PartPlatforms.ContainsKey(block.BlockDefinition.Id))
                            {

                                Ai gridAi;
                                CoreComponent comp;
                                if (!EntityAIs.TryGetValue(block.CubeGrid, out gridAi) || !gridAi.CompBase.TryGetValue(block, out comp))
                                    _uninitializedBlocks.Add(block);
                                else {

                                    var wComp = comp as Weapon.WeaponComponent;
                                    if (wComp != null && wComp.Data.Repo.Values.Set.Overrides.Debug)
                                        _debugBlocks.Add(wComp);
                                }
                            }
                        }
                    }

                }
            }
            for (int i = _uninitializedBlocks.Count - 1; i >= 0; i--)
            {

                var badBlock = _uninitializedBlocks[i];

                Ai gridAi;
                CoreComponent comp;
                if (EntityAIs.TryGetValue(badBlock.CubeGrid, out gridAi) && gridAi.CompBase.TryGetValue(badBlock, out comp))
                {
                    _uninitializedBlocks.RemoveAtFast(i);
                    Log.Line($"quick remove");
                    continue;
                }

                if (badBlock.InScene)
                {

                    var lookSphere = new BoundingSphereD(badBlock.PositionComp.WorldAABB.Center, 30f);
                    if (Camera.IsInFrustum(ref lookSphere))
                    {
                        MyOrientedBoundingBoxD blockBox;
                        SUtils.GetBlockOrientedBoundingBox(badBlock, out blockBox);
                        DsDebugDraw.DrawBox(blockBox, _uninitializedColor);
                    }
                }
            }

            for (int i = 0; i < _debugBlocks.Count; i++)
            {

                var comp = _debugBlocks[i];
                if (comp.Cube.InScene)
                {

                    var lookSphere = new BoundingSphereD(comp.Cube.PositionComp.WorldAABB.Center, 100f);

                    if (Camera.IsInFrustum(ref lookSphere))
                    {
                        var collection = comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? comp.Platform.Weapons : comp.Platform.Phantoms;
                        foreach (var w in collection)
                        {

                            if (!w.AiEnabled && w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance == WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType.Smart)
                                w.SmartLosDebug();
                        }
                    }
                }
            }
        }

        private void UpdatePlacer()
        {
            if (!Placer.Visible) Placer = null;
            if (!MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.HitInfo.HasValue)
            {
                var hit = MyCubeBuilder.Static.HitInfo.Value as IHitInfo;
                var grid = hit.HitEntity as MyCubeGrid;
                Ai ai;
                if (grid != null && EntityToMasterAi.TryGetValue(grid, out ai))
                {
                    if (MyCubeBuilder.Static.CurrentBlockDefinition != null)
                    {
                        var subtypeIdHash = MyCubeBuilder.Static.CurrentBlockDefinition.Id.SubtypeId;
                        PartCounter partCounter;
                        if (ai.PartCounting.TryGetValue(subtypeIdHash, out partCounter))
                        {
                            if (partCounter.Max > 0 && ai.Construct.GetPartCount(subtypeIdHash) >= partCounter.Max)
                            {
                                MyCubeBuilder.Static.NotifyPlacementUnable();
                                MyCubeBuilder.Static.Deactivate();
                                return;
                            }
                        }

                        if (AreaRestrictions.ContainsKey(subtypeIdHash))
                        {
                            MyOrientedBoundingBoxD restrictedBox;
                            MyOrientedBoundingBoxD buildBox = MyCubeBuilder.Static.GetBuildBoundingBox();
                            BoundingSphereD restrictedSphere;
                            if (IsPartAreaRestricted(subtypeIdHash, buildBox, grid, 0, null, out restrictedBox, out restrictedSphere))
                            {
                                DsDebugDraw.DrawBox(buildBox, _uninitializedColor);
                            }

                            if (MyAPIGateway.Session.Config.HudState == 1)
                            {
                                if (restrictedBox.HalfExtent.AbsMax() > 0)
                                {
                                    DsDebugDraw.DrawBox(restrictedBox, _restrictionAreaColor);
                                }
                                if (restrictedSphere.Radius > 0)
                                {
                                    DsDebugDraw.DrawSphere(restrictedSphere, _restrictionAreaColor);
                                }

                                for (int i = 0; i < ai.WeaponComps.Count; i++)
                                {
                                    var comp = ai.WeaponComps[i];

                                    if (comp.IsBlock)
                                    {
                                        MyOrientedBoundingBoxD blockBox;
                                        SUtils.GetBlockOrientedBoundingBox(comp.Cube, out blockBox);

                                        BoundingSphereD s;
                                        MyOrientedBoundingBoxD b;
                                        CalculateRestrictedShapes(comp.SubTypeId, blockBox, out b, out s);

                                        if (s.Radius > 0)
                                        {
                                            DsDebugDraw.DrawSphere(s, _restrictionAreaColor);
                                        }
                                        if (b.HalfExtent.AbsMax() > 0)
                                        {
                                            DsDebugDraw.DrawBox(b, _restrictionAreaColor);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        internal void TargetSelection()
        {
            if ((UiInput.AltPressed && UiInput.ShiftReleased || TargetUi.DrawReticle && UiInput.ClientInputState.MouseButtonRight) && InGridAiBlock)
                TrackingAi.Construct.Focus.RequestReleaseActive(TrackingAi);

            if (UiInput.ActionKeyReleased && TrackingAi.Construct.Data.Repo.FocusData.HasFocus && InGridAiBlock)
                TrackingAi.Construct.Focus.RequestAddLock(TrackingAi);

            if (InGridAiBlock)
            {
                if (UiInput.MouseButtonLeftNewPressed || UiInput.MouseButtonLeftReleased && (TargetUi.DrawReticle || UiInput.FirstPersonView))
                    TargetUi.SelectTarget(true, UiInput.MouseButtonLeftNewPressed);
                else if (!UiInput.CameraBlockView)
                {
                    if (UiInput.CurrentWheel != UiInput.PreviousWheel)
                        TargetUi.SelectNext();
                    else if (UiInput.LongShift || UiInput.ShiftReleased && !UiInput.LongShift) 
                        TrackingAi.Construct.Focus.RequestNextActive(UiInput.LongShift, TrackingAi);
                }
            }
        }

        internal void RemoveGps()
        {
            if (TargetGps != null)
            {
                if (TargetGps.ShowOnHud)
                {
                    MyAPIGateway.Session.GPS.RemoveLocalGps(TargetGps);
                    TargetGps.ShowOnHud = false;
                }

            }
        }

        internal void AddGps(Color color = default(Color))
        {
            if (TargetGps != null)
            {
                if (!TargetGps.ShowOnHud)
                {
                    TargetGps.ShowOnHud = true;
                    MyAPIGateway.Session.GPS.AddLocalGps(TargetGps);
                    if (color != default(Color))
                        MyVisualScriptLogicProvider.SetGPSColor(TargetGps?.Name, color);
                }
            }
        }

        internal void SetGpsInfo(Vector3D pos, string name, double dist = 0, Color color = default(Color))
        {
            if (TargetGps != null)
            {
                var newPos = dist > 0 ? pos + (Camera.WorldMatrix.Up * dist) : pos;
                TargetGps.Coords = newPos;
                TargetGps.Name = name;
                if (color != default(Color))
                    MyVisualScriptLogicProvider.SetGPSColor(TargetGps?.Name, color);
            }
        }

        internal bool CheckTarget(Ai ai)
        {
            if (!ai.Construct.Focus.ClientIsFocused(ai)) return false;

            if (ai != TrackingAi)
            {
                TrackingAi = null;
                return false;
            }

            return ai.Construct.Data.Repo.FocusData.HasFocus;
        }

        internal void SetTarget(MyEntity entity, Ai ai, Dictionary<MyEntity, MyTuple<float, TargetUi.TargetControl>> masterTargets)
        {

            TrackingAi = ai;
            ai.Construct.Focus.RequestAddFocus(entity, ai);

            Ai gridAi;
            TargetArmed = false;
            var grid = entity as MyCubeGrid;
            if (grid != null && EntityToMasterAi.TryGetValue(grid, out gridAi))
            {
                TargetArmed = true;
            }
            else
            {

                MyTuple<float, TargetUi.TargetControl> targetInfo;
                if (!masterTargets.TryGetValue(entity, out targetInfo)) return;
                ConcurrentDictionary<BlockTypes, ConcurrentCachingList<MyCubeBlock>> typeDict;

                var tGrid = entity as MyCubeGrid;
                if (tGrid != null && GridToBlockTypeMap.TryGetValue(tGrid, out typeDict))
                {

                    ConcurrentCachingList<MyCubeBlock> fatList;
                    if (typeDict.TryGetValue(Offense, out fatList))
                        TargetArmed = fatList.Count > 0;
                    else TargetArmed = false;
                }
                else TargetArmed = false;
            }
        }
    }
}
