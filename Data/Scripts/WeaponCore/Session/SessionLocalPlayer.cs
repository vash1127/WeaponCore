using System.Collections.Concurrent;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.Support.WeaponDefinition.TargetingDef.BlockTypes;
namespace WeaponCore
{
    public partial class Session
    {
        internal bool UpdateLocalAiAndCockpit()
        {
            InGridAiBlock = false;
            ActiveControlBlock = ControlledEntity as MyCubeBlock;
            ActiveCockPit = ActiveControlBlock as MyCockpit;

            var activeBlock = ActiveCockPit ?? ActiveControlBlock;
            if (activeBlock != null && GridToMasterAi.TryGetValue(activeBlock.CubeGrid, out TrackingAi))
            {
                InGridAiBlock = true;
                MyCubeBlock oldBlock;
                TrackingAi.ControllingPlayers.TryGetValue(PlayerId, out oldBlock);
                TrackingAi.ControllingPlayers[PlayerId] = ActiveControlBlock;

                if (HandlesInput && oldBlock != ActiveControlBlock)
                    SendActiveControlUpdate(activeBlock, true);
            }
            else
            {
                if (TrackingAi != null)
                {
                    TrackingAi.Focus.IsFocused(TrackingAi);

                    MyCubeBlock oldBlock;
                    if (HandlesInput && TrackingAi.ControllingPlayers.TryGetValue(PlayerId, out oldBlock))
                        SendActiveControlUpdate(oldBlock, false);

                    TrackingAi.ControllingPlayers.Remove(PlayerId);
                }

                TrackingAi = null;
                ActiveCockPit = null;
                ActiveControlBlock = null;
            }
            return InGridAiBlock;
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
                
                if (ControlledEntity is IMyGunBaseUser && !(lastControlledEnt is IMyGunBaseUser))
                {
                    var cube = (MyCubeBlock)ControlledEntity;
                    GridAi gridAi;
                    if (GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                    {
                        WeaponComponent comp;
                        if (gridAi.WeaponBase.TryGetValue(cube, out comp))
                        {
                            GunnerBlackList = true;
                            comp.State.Value.CurrentPlayerControl.PlayerId = PlayerId;
                            comp.State.Value.CurrentPlayerControl.ControlType = ControlType.Camera;
                            ActiveControlBlock = (MyCubeBlock)ControlledEntity;
                            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, PlayerId, false);
                            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, PlayerId, false);
                            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, PlayerId, false);

                            if (HandlesInput && MpActive)
                                SendControlingPlayer(comp);
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
                        var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, PlayerId, true);
                        var oldCube = lastControlledEnt as MyCubeBlock;
                        GridAi gridAi;
                        if (oldCube != null && GridTargetingAIs.TryGetValue(oldCube.CubeGrid, out gridAi))
                        {
                            WeaponComponent comp;
                            if (gridAi.WeaponBase.TryGetValue(oldCube, out comp))
                            {
                                comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                                comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
                                ActiveControlBlock = null;
                            }
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
                GridAi gridAi;
                if (grid != null && GridToMasterAi.TryGetValue(grid, out gridAi))
                {
                    if (MyCubeBuilder.Static.CurrentBlockDefinition != null)
                    {
                        var subtypeIdHash = MyCubeBuilder.Static.CurrentBlockDefinition.Id.SubtypeId;
                        WeaponCount weaponCount;
                        if (gridAi.WeaponCounter.TryGetValue(subtypeIdHash, out weaponCount))
                        {
                            if (weaponCount.Max > 0 && gridAi.Construct.GetWeaponCount(subtypeIdHash) >= weaponCount.Max)
                            {
                                MyCubeBuilder.Static.NotifyPlacementUnable();
                                MyCubeBuilder.Static.Deactivate();
                            }
                        }
                    }
                }
            }
        }

        internal void TargetSelection()
        {
            if ((UiInput.AltPressed && UiInput.ShiftReleased || TargetUi.DrawReticle && UiInput.ClientInputState.MouseButtonRight) && InGridAiBlock)
                TrackingAi.Focus.ReleaseActive(TrackingAi);

            if (InGridAiBlock)
            {
                if ((TargetUi.DrawReticle || UiInput.FirstPersonView) && MyAPIGateway.Input.IsNewLeftMouseReleased())
                    TargetUi.SelectTarget();
                else
                {
                    if (UiInput.CurrentWheel != UiInput.PreviousWheel)
                        TargetUi.SelectNext();
                    else if (UiInput.LongShift || UiInput.ShiftReleased && !UiInput.LongShift)
                        TrackingAi.Focus.NextActive(UiInput.LongShift, TrackingAi);
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

        internal bool CheckTarget(GridAi ai)
        {
            if (!ai.Focus.IsFocused(ai)) return false;

            if (ai != TrackingAi)
            {
                TrackingAi = null;
                return false;
            }

            return ai.Focus.HasFocus;
        }

        internal void SetTarget(MyEntity entity, GridAi ai)
        {
            
            TrackingAi = ai;
            TrackingAi.Focus.AddFocus(entity, ai);

            GridAi gridAi;
            TargetArmed = false;
            var grid = entity as MyCubeGrid;
            if (grid != null && GridToMasterAi.TryGetValue(grid, out gridAi))
            {
                TargetArmed = true;
            }
            else {

                TargetInfo info;
                if (!ai.Targets.TryGetValue(entity, out info)) return;
                ConcurrentDictionary<BlockTypes, ConcurrentCachingList<MyCubeBlock>> typeDict;
                
                if (info.IsGrid && GridToBlockTypeMap.TryGetValue((MyCubeGrid)info.Target, out typeDict)) {

                    ConcurrentCachingList<MyCubeBlock> fatList;
                    if (typeDict.TryGetValue(WeaponDefinition.TargetingDef.BlockTypes.Offense, out fatList))
                        TargetArmed = fatList.Count > 0;
                    else TargetArmed = false;
                }
                else TargetArmed = false;
            }
        }
    }
}
