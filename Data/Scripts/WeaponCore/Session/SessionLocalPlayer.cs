using System;
using System.Collections.Concurrent;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
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
            if (activeBlock != null && GridTargetingAIs.TryGetValue(activeBlock.CubeGrid, out TrackingAi))
            {
                InGridAiBlock = true;
                TrackingAi.ControllingPlayers[Session.Player.IdentityId] = ActiveControlBlock;
            }
            else
            {
                TrackingAi?.Focus.IsFocused(TrackingAi);
                TrackingAi?.ControllingPlayers.Remove(Session.Player.IdentityId);
                TrackingAi = null;
                ActiveCockPit = null;
                ActiveControlBlock = null;
            }
            return InGridAiBlock;
        }
        /*
        private void RemoveAction(long entityId, string typeId, string subtypeId, int page, int slot)
        {
            if (entityId != 0)
            {
                var myDefinitionId = MyVisualScriptLogicProvider.GetDefinitionId(typeId, subtypeId);
                if ((ReplaceVanilla && VanillaIds.ContainsKey(myDefinitionId)) || WeaponPlatforms.ContainsKey(myDefinitionId.SubtypeId))
                {
                    try
                    {
                        MyVisualScriptLogicProvider.SetToolbarPage(page, Session.Player.IdentityId);
                        MyVisualScriptLogicProvider.ClearToolbarSlot(slot, Session.Player.IdentityId);
                    }
                    catch (Exception e)
                    {
                        FutureEvents.Schedule((object o) =>
                        {
                        //player is sitting in cockpit on game load and has an action to be removed
                        try
                            {
                                MyVisualScriptLogicProvider.SetToolbarPage(page, Session.Player.IdentityId);
                                MyVisualScriptLogicProvider.ClearToolbarSlot(slot, Session.Player.IdentityId);
                            }
                            catch (Exception e2)
                            {
                                Log.Line($"error in action removal: {e2}");
                            }
                        }, null, 10);
                    }
                }
            }
        }
        */

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
                            GridTargetingAIs[cube.CubeGrid].Gunners.Add(comp, MyAPIGateway.Session.Player.IdentityId);
                            comp.State.Value.PlayerIdInTerminal = -3;
                            ActiveControlBlock = (MyCubeBlock)ControlledEntity;
                            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, false);
                            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, false);
                            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, false);
                        }
                    }
                }
                else if (!(ControlledEntity is IMyGunBaseUser) && lastControlledEnt is IMyGunBaseUser)
                {
                    if (GunnerBlackList)
                    {
                        GunnerBlackList = false;
                        var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, true);
                        var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, true);
                        var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, true);
                        var oldCube = lastControlledEnt as MyCubeBlock;
                        GridAi gridAi;
                        if (oldCube != null && GridTargetingAIs.TryGetValue(oldCube.CubeGrid, out gridAi))
                        {
                            WeaponComponent comp;
                            if (gridAi.WeaponBase.TryGetValue(oldCube, out comp))
                            {
                                GridTargetingAIs[oldCube.CubeGrid].Gunners.Remove(comp);
                                comp.State.Value.PlayerIdInTerminal = -1;
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
                if (grid != null && GridTargetingAIs.TryGetValue(grid, out gridAi))
                {
                    if (MyCubeBuilder.Static.CurrentBlockDefinition != null)
                    {
                        var subtypeIdHash = MyCubeBuilder.Static.CurrentBlockDefinition.Id.SubtypeId;
                        WeaponCount weaponCount;
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

        internal void TargetSelection()
        {
            if ((UiInput.AltPressed && UiInput.ShiftReleased || TargetUi.DrawReticle && UiInput.MouseButtonRight) && InGridAiBlock)
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
            if (grid != null && GridTargetingAIs.TryGetValue(grid, out gridAi))
            {
                TargetArmed = true;
            }
            else {

                TargetInfo info;
                if (!ai.Targets.TryGetValue(entity, out info)) return;
                ConcurrentDictionary<TargetingDefinition.BlockTypes, ConcurrentCachingList<MyCubeBlock>> typeDict;
                
                if (info.IsGrid && GridToBlockTypeMap.TryGetValue((MyCubeGrid)info.Target, out typeDict)) {

                    ConcurrentCachingList<MyCubeBlock> fatList;
                    if (typeDict.TryGetValue(TargetingDefinition.BlockTypes.Offense, out fatList))
                        TargetArmed = fatList.Count > 0;
                    else TargetArmed = false;
                }
                else TargetArmed = false;
            }
        }
    }
}
