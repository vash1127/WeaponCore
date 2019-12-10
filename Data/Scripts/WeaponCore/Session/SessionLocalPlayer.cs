using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        internal bool UpdateLocalAiAndCockpit()
        {
            ActiveCockPit = ControlledEntity as MyCockpit;
            InGridAiCockPit = false;
            if (ActiveCockPit != null && GridTargetingAIs.TryGetValue(ActiveCockPit.CubeGrid, out TrackingAi))
            {
                InGridAiCockPit = true;
                return true;
            }
            TrackingAi?.Focus.IsFocused();

            TrackingAi = null;
            ActiveCockPit = null;
            //RemoveGps();
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
                
                if (ControlledEntity is IMyGunBaseUser && !(lastControlledEnt is IMyGunBaseUser))
                {
                    var cube = (MyCubeBlock)ControlledEntity;
                    GridAi gridAi;
                    if (GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                    {
                        GunnerBlackList = true;
                        GridTargetingAIs[cube.CubeGrid].HasGunner = true;
                        var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, false);
                        var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, false);
                        var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, false);
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
                            gridAi.HasGunner = false;
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

        internal void TargetSelection()
        {
            if ((UiInput.AltPressed && UiInput.ShiftReleased || TargetUi.DrawReticle && UiInput.MouseButtonRight) && UpdateLocalAiAndCockpit())
                TrackingAi.Focus.ReleaseActive();

            if (UpdateLocalAiAndCockpit())
            {
                if ((TargetUi.DrawReticle || UiInput.FirstPersonView) && MyAPIGateway.Input.IsNewLeftMouseReleased())
                    TargetUi.SelectTarget();
                else
                {
                    if (UiInput.CurrentWheel != UiInput.PreviousWheel)
                        TargetUi.SelectNext();
                    else if (UiInput.LongShift || UiInput.ShiftReleased && !UiInput.LongShift)
                        TrackingAi.Focus.NextActive(UiInput.LongShift);
                }
            }
        }
        /*

        internal void ResetGps()
        {
            if (TargetGps == null)
            {
                Log.Line("resetgps");
                MyVisualScriptLogicProvider.AddGPS("WEAPONCORE", "", Vector3D.MaxValue, Color.Red);
                
                foreach (var t in MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId))
                {
                    if (t.Name == "WEAPONCORE")
                    {
                        TargetGps = t;
                        break;
                    }
                }
                MyAPIGateway.Session.GPS.AddLocalGps(TargetGps);
                MyVisualScriptLogicProvider.SetGPSColor(TargetGps?.Name, Color.Yellow);
                if (TargetGps != null) TargetGps.ShowOnHud = false;
            }
        }
        */

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

        internal void SetGpsInfo(Vector3D pos, string name, double dist = 0)
        {
            if (TargetGps != null)
            {
                var newPos = dist > 0 ? pos + (Camera.WorldMatrix.Up * dist) : pos;
                TargetGps.Coords = newPos;
                TargetGps.Name = name;
            }
        }

        internal bool CheckTarget(GridAi ai)
        {
            if (!ai.Focus.IsFocused()) return false;

            if (ai != TrackingAi)
            {
                Log.Line("resetting target");
                TrackingAi = null;
                //RemoveGps();
                return false;
            }

            return ai.Focus.HasFocus;
        }

        internal void SetTarget(MyEntity entity, GridAi ai)
        {
            
            TrackingAi = ai;
            TrackingAi.Focus.AddFocus(entity);

            GridAi gridAi;
            TargetArmed = false;
            if (GridTargetingAIs.TryGetValue((MyCubeGrid)entity, out gridAi))
            {
                TargetArmed = true;
            }
            else
            {
                foreach (var info in ai.SortedTargets)
                {
                    if (info.Target != entity) continue;
                    ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>> typeDict;
                    if (info.IsGrid && ai.Session.GridToBlockTypeMap.TryGetValue((MyCubeGrid)info.Target, out typeDict))
                    {
                        MyConcurrentList<MyCubeBlock> fatList;
                        if (typeDict.TryGetValue(TargetingDefinition.BlockTypes.Offense, out fatList))
                            TargetArmed = fatList.Count > 0;
                        else TargetArmed = false;
                    }
                    else TargetArmed = false;
                    break;
                }
            }
        }
    }
}
