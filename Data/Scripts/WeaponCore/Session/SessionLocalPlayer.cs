using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Collections;
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
            //var reticlelastOnSelf = Tick - TargetUi.ReticleOnSelfTick;
            if (activeBlock != null && GridTargetingAIs.TryGetValue(activeBlock.CubeGrid, out TrackingAi))
            {
                InGridAiBlock = true;
                MyCubeBlock oldBlock;
                TrackingAi.ControllingPlayers.TryGetValue(Session.Player.IdentityId, out oldBlock);
                TrackingAi.ControllingPlayers[Session.Player.IdentityId] = ActiveControlBlock;

                if (oldBlock != ActiveControlBlock)
                {
                    if (IsClient)
                    {
                        PacketsToServer.Add(new DictionaryUpdatePacket
                            {
                                EntityId = activeBlock.EntityId,
                                SenderId = MultiplayerId,
                                PType = PacketType.ActiveControlUpdate,
                                Data = true
                            });
                        //SendPacketToServer(new DictionaryUpdatePacket { EntityId = activeBlock.EntityId, SenderId = MultiplayerId, PType = PacketType.ActiveControlUpdate, Data = true });
                    }
                    else if (MpActive)
                        PacketsToClient.Add(new PacketInfo
                        {
                            Entity = activeBlock,
                            Packet = new DictionaryUpdatePacket
                            {
                                EntityId = activeBlock.EntityId,
                                SenderId = 0,
                                PType = PacketType.ActiveControlUpdate,
                                Data = true
                            }
                        });
                    //PacketizeToClientsInRange(activeBlock, new DictionaryUpdatePacket { EntityId = activeBlock.EntityId, SenderId = 0, PType = PacketType.ActiveControlUpdate, Data = true });
                }
                
                /*
                if (!TrackingAi.FadeOut && TargetUi.DrawReticle && reticlelastOnSelf <= 1 && TargetUi.ReticleAgeOnSelf > 120)
                {
                    if (!_renderCached) UpdateCache();
                    else ToggleTransparent(TrackingAi, false);
                }
                else if (TrackingAi.FadeOut && (reticlelastOnSelf > 119 || !TargetUi.DrawReticle))
                    ToggleTransparent(TrackingAi, true);
                    */
            }
            else
            {
                if (TrackingAi != null)
                {
                    //if (TrackingAi.FadeOut)
                    //ToggleTransparent(TrackingAi, true);

                    TrackingAi.Focus.IsFocused(TrackingAi);

                    MyCubeBlock oldBlock;
                    if (TrackingAi.ControllingPlayers.TryGetValue(Session.Player.IdentityId, out oldBlock))
                    {
                        if (IsClient)
                        {
                            PacketsToServer.Add(new DictionaryUpdatePacket {
                                    EntityId = oldBlock.EntityId,
                                    SenderId = MultiplayerId,
                                    PType = PacketType.ActiveControlUpdate,
                                    Data = false
                                });
                            //SendPacketToServer(new DictionaryUpdatePacket { EntityId = oldBlock.EntityId, SenderId = MultiplayerId, PType = PacketType.ActiveControlUpdate, Data = false });
                        }
                        else if (MpActive)
                        {
                            PacketsToClient.Add(new PacketInfo
                            {
                                Entity = oldBlock,
                                Packet = new DictionaryUpdatePacket
                                {
                                    EntityId = oldBlock.EntityId,
                                    SenderId = 0,
                                    PType = PacketType.ActiveControlUpdate,
                                    Data = false
                                }
                            });
                        }

                        //PacketizeToClientsInRange(oldBlock, new DictionaryUpdatePacket { EntityId = oldBlock.EntityId, SenderId = 0, PType = PacketType.ActiveControlUpdate, Data = true });

                        TrackingAi.ControllingPlayers.Remove(Session.Player.IdentityId);
                    }
                }

                TrackingAi = null;
                ActiveCockPit = null;
                ActiveControlBlock = null;
            }
            return InGridAiBlock;
        }

        private void UpdateCache(bool clearCache = false)
        {
            if (clearCache)
            {
                _renderCached = false;
                _slimCache.Clear();
                return;
            }
            _renderCached = true;
            _slimCache.AddRange(TrackingAi.MyGrid.CubeBlocks);

            foreach (var sub in TrackingAi.SubGrids)
                _slimCache.AddRange(sub.CubeBlocks);

        }
        /*
        private void ToggleTransparent(GridAi ai, bool setvisible)
        {
            TargetUi.ReticleAgeOnSelf = 0;
            TrackingAi.FadeOut = !setvisible;
            var transparency = setvisible ? 0 : 0.72f;
            var character = MyAPIGateway.Session.Player.Character;
            if (character != null)
            {
                if (setvisible)
                {
                    character.Render.Transparency = 0;
                    character.Render.UpdateTransparency();
                }
                else
                {
                    character.Render.Transparency = 1;
                    character.Render.UpdateTransparency();
                }
            }

            SetTransparency(transparency, setvisible, ai);

            if (setvisible) UpdateCache(clearCache: true);
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
                            GridTargetingAIs[cube.CubeGrid].Gunners.Add(comp, Session.Player.IdentityId);
                            comp.State.Value.PlayerIdInTerminal = -3;
                            ActiveControlBlock = (MyCubeBlock)ControlledEntity;
                            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, Session.Player.IdentityId, false);
                            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, Session.Player.IdentityId, false);
                            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, Session.Player.IdentityId, false);
                        }
                    }
                }
                else if (!(ControlledEntity is IMyGunBaseUser) && lastControlledEnt is IMyGunBaseUser)
                {
                    if (GunnerBlackList)
                    {
                        GunnerBlackList = false;
                        var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, Session.Player.IdentityId, true);
                        var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, Session.Player.IdentityId, true);
                        var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, Session.Player.IdentityId, true);
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
            if ((UiInput.AltPressed && UiInput.ShiftReleased || TargetUi.DrawReticle && UiInput.ClientMouseState.MouseButtonRight) && InGridAiBlock)
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

        /*
        private void SetTransparency(float transparencyOrigin, bool setvisible, GridAi ai)
        {
            for (int i = 0; i < _slimCache.Count; i++)
            {
                var cubeBlock = _slimCache[i];
                var transparency = -transparencyOrigin;
                if (cubeBlock.Dithering == transparency)
                    continue;
                var cube = cubeBlock.FatBlock as MyCubeBlock;
                if (cube != null  && (!cube.IsFunctional || cube is IMyButtonPanel || cube is MyThrust)) continue;

                cubeBlock.Dithering = transparency;
                
                if (cube == null) continue;
                var thruster = cube as MyThrust;
                if (thruster != null && thruster.IsFunctional)
                    thruster.Render.UpdateFlameProperties(setvisible, 0);
                MyEntity renderEntity = cube;
                if (cube.Subparts != null)
                {
                    foreach (KeyValuePair<string, MyEntitySubpart> subpart1 in renderEntity.Subparts)
                    {
                        if (subpart1.Value.Closed) continue;
                        subpart1.Value.Render.Transparency = transparency;
                        //subpart1.Value.Render.UpdateTransparency();
                        subpart1.Value.Render.RemoveRenderObjects();
                        subpart1.Value.Render.AddRenderObjects();
                        if (subpart1.Value?.Subparts != null)
                        {
                            foreach (KeyValuePair<string, MyEntitySubpart> subpart2 in subpart1.Value.Subparts)
                            {
                                if (subpart2.Value.Closed) continue;
                                subpart2.Value.Render.Transparency = transparency;
                                //subpart2.Value.Render.UpdateTransparency();
                                subpart2.Value.Render.RemoveRenderObjects();
                                subpart2.Value.Render.AddRenderObjects();
                                if (subpart2.Value?.Subparts != null)
                                {
                                    foreach (KeyValuePair<string, MyEntitySubpart> subpart3 in subpart2.Value.Subparts)
                                    {
                                        if (subpart3.Value.Closed) continue;
                                        subpart3.Value.Render.Transparency = transparency;
                                        //subpart3.Value.Render.UpdateTransparency();
                                        subpart3.Value.Render.RemoveRenderObjects();
                                        subpart3.Value.Render.AddRenderObjects();
                                        if (subpart3.Value?.Subparts != null)
                                        {
                                            foreach (KeyValuePair<string, MyEntitySubpart> subpart4 in subpart3.Value.Subparts)
                                            {
                                                if (subpart4.Value.Closed) continue;
                                                subpart4.Value.Render.Transparency = transparency;
                                                //subpart4.Value.Render.UpdateTransparency();
                                                subpart4.Value.Render.RemoveRenderObjects();
                                                subpart4.Value.Render.AddRenderObjects();
                                                SetTransparencyForSubparts(subpart4.Value, transparency);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            ai.MyGrid.Render.Transparency = -transparencyOrigin;
            ai.MyGrid.Render.UpdateRenderObject(!setvisible, true);
            foreach (var sub in ai.SubGrids)
            {
                sub.Render.Transparency = -transparencyOrigin;
                sub.Render.UpdateRenderObject(!setvisible, true);
            }

        }

        private void SetTransparencyForSubparts(MyEntity renderEntity, float transparency)
        {
            foreach (KeyValuePair<string, MyEntitySubpart> subpart in renderEntity.Subparts)
            {
                if (subpart.Value.Closed) continue;
                subpart.Value.Render.Transparency = transparency;
                //subpart.Value.Render.UpdateTransparency();
                subpart.Value.Render.RemoveRenderObjects();
                subpart.Value.Render.AddRenderObjects();
                SetTransparencyForSubparts(subpart.Value, transparency);
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
