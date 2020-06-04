using System.Diagnostics;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;

namespace WeaponCore
{
    public partial class Session
    {

        #region Packet Creation Methods
        internal class PacketObj
        {
            internal Packet Packet;
            internal NetworkReporter.Report Report;
            internal ErrorPacket ErrorPacket;
            internal int PacketSize;

            internal void Clean()
            {
                Packet = null;
                Report = null;
                ErrorPacket = null;
                PacketSize = 0;
            }

            public virtual bool Equals(ErrorPacket other)
            {
                if (Packet == null) return false;

                return Packet.Equals(other.Packet);
            }

            public override bool Equals(object obj)
            {
                if (Packet == null) return false;
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ErrorPacket)obj);
            }

            public override int GetHashCode()
            {
                if (Packet == null) return 0;

                return Packet.GetHashCode();
            }
        }

        public struct NetResult
        {
            public string Message;
            public bool Valid;
        }

        private NetResult Msg(string message, bool valid = false)
        {
            return new NetResult { Message = message, Valid = valid };
        }

        private bool Error(PacketObj data, params NetResult[] messages)
        {
            var message = $"[{data.Packet.PType.ToString()} - PacketError] - ";

            for (int i = 0; i < messages.Length; i++)
            {
                var resultPair = messages[i];
                message += $"{resultPair.Message}: {resultPair.Valid} - ";
            }
            data.ErrorPacket.Error = message;
            Log.LineShortDate(data.ErrorPacket.Error, "net");
            return false;
        }

        internal struct PacketInfo
        {
            internal MyEntity Entity;
            internal Packet Packet;
            internal bool SingleClient;
        }

        internal class ErrorPacket
        {
            internal uint RecievedTick;
            internal uint RetryTick;
            internal uint RetryDelayTicks;
            internal int RetryAttempt;
            internal int MaxAttempts;
            internal bool NoReprocess;
            internal bool Retry;
            internal string Error;
            internal PacketType PType;
            internal Packet Packet;

            public virtual bool Equals(ErrorPacket other)
            {
                if (Packet == null) return false;

                return Packet.Equals(other.Packet);
            }

            public override bool Equals(object obj)
            {
                if (Packet == null) return false;
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ErrorPacket)obj);
            }

            public override int GetHashCode()
            {
                if (Packet == null) return 0;

                return Packet.GetHashCode();
            }
        }

        internal void SendMouseUpdate(MyEntity entity)
        {
            if (!HandlesInput) return;

            if (IsClient)
            {
                PacketsToServer.Add(new InputPacket
                {
                    EntityId = entity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ClientMouseEvent,
                    Data = UiInput.ClientInputState
                });
            }
            else
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = entity,
                    Packet = new InputPacket
                    {
                        EntityId = entity.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ClientMouseEvent,
                        Data = UiInput.ClientInputState
                    }
                });
            }
        }

        internal void SendActiveControlUpdate(MyCubeBlock controlBlock, bool active)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = controlBlock.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ActiveControlUpdate,
                    Data = active
                });
            }
            else
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = controlBlock,
                    Packet = new BoolUpdatePacket
                    {
                        EntityId = controlBlock.EntityId,
                        SenderId = 0,
                        PType = PacketType.ActiveControlUpdate,
                        Data = active
                    }
                });
            }
        }

        internal void SendCycleAmmoNetworkUpdate(Weapon weapon, int ammoId)
        {
            weapon.Comp.MIds[(int)PacketType.CycleAmmo]++;
            if (IsClient)
            {
                PacketsToServer.Add(new CycleAmmoPacket
                {
                    EntityId = weapon.Comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.CycleAmmo,
                    AmmoId = ammoId,
                    MId = weapon.Comp.MIds[(int)PacketType.CycleAmmo],
                    WeaponId = weapon.WeaponId
                });
            }
            else
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = weapon.Comp.MyCube,
                    Packet = new CycleAmmoPacket
                    {
                        EntityId = weapon.Comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = PacketType.CycleAmmo,
                        AmmoId = ammoId,
                        MId = weapon.Comp.MIds[(int)PacketType.CycleAmmo],
                        WeaponId = weapon.WeaponId
                    }
                });
            }
        }

        internal void SendOverRidesUpdate(GridAi ai, string groupName, GroupOverrides overRides)
        {
            if (MpActive)
            {
                ai.UiMId++;
                if (IsClient)
                {
                    PacketsToServer.Add(new OverRidesPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        MId = ai.UiMId,
                        GroupName = groupName,
                        PType = PacketType.OverRidesUpdate,
                        Data = overRides,
                    });
                }
                else if (IsServer)
                {
                    PacketsToClient.Add(new PacketInfo
                    {
                        Entity = ai.MyGrid,
                        Packet = new OverRidesPacket
                        {
                            EntityId = ai.MyGrid.EntityId,
                            SenderId = 0,
                            MId = ai.UiMId,
                            GroupName = groupName,
                            PType = PacketType.OverRidesUpdate,
                            Data = overRides,
                        }
                    });
                }
            }
        }

        internal void SendOverRidesUpdate(WeaponComponent comp, GroupOverrides overRides)
        {
            if (MpActive)
            {
                comp.MIds[(int)PacketType.OverRidesUpdate]++;
                if (IsClient)
                {
                    PacketsToServer.Add(new OverRidesPacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = MultiplayerId,
                        MId = comp.MIds[(int)PacketType.OverRidesUpdate],
                        PType = PacketType.OverRidesUpdate,
                        Data = comp.Set.Value.Overrides,
                    });
                }
                else
                {
                    PacketsToClient.Add(new PacketInfo
                    {
                        Entity = comp.MyCube,
                        Packet = new OverRidesPacket
                        {
                            EntityId = comp.MyCube.EntityId,
                            SenderId = 0,
                            MId = comp.MIds[(int)PacketType.OverRidesUpdate],
                            PType = PacketType.OverRidesUpdate,
                            Data = comp.Set.Value.Overrides,
                        }
                    });
                }
            }
        }

        internal void SendActionShootUpdate(WeaponComponent comp, ManualShootActionState state)
        {
            if (!HandlesInput) return;

                
            comp.MIds[(int)PacketType.CompToolbarShootState]++;
            var mId = comp.MIds[(int)PacketType.CompToolbarShootState];

            if (IsClient)
            {
                comp.Session.PacketsToServer.Add(new ShootStatePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = comp.Session.MultiplayerId,
                    PType = PacketType.CompToolbarShootState,
                    MId = mId,
                    Data = state,
                });
            }
            else
            {
                comp.Session.PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new ShootStatePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = comp.Session.MultiplayerId,
                        PType = PacketType.CompToolbarShootState,
                        MId = mId,
                        Data = state,
                    }
                });
            }
        }

        internal void SendRangeUpdate(WeaponComponent comp, float range)
        {
            comp.MIds[(int)PacketType.RangeUpdate]++;

            if (IsClient)
            {
                comp.Session.PacketsToServer.Add(new RangePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = comp.Session.MultiplayerId,
                    PType = PacketType.RangeUpdate,
                    MId = comp.MIds[(int)PacketType.RangeUpdate],
                    Data = range,
                });
            }
            else if (HandlesInput)
            {
                comp.Session.PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new RangePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = comp.Session.MultiplayerId,
                        PType = PacketType.RangeUpdate,
                        MId = comp.MIds[(int)PacketType.RangeUpdate],
                        Data = range,
                    }
                });
            }
        }

        internal void SendControlingPlayer(WeaponComponent comp)
        {
            comp.MIds[(int)PacketType.PlayerControlUpdate]++;
            if (IsClient)
            {
                PacketsToServer.Add(new ControllingPlayerPacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    MId = comp.MIds[(int)PacketType.PlayerControlUpdate],
                    PType = PacketType.PlayerControlUpdate,
                    Data = comp.State.Value.CurrentPlayerControl,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new ControllingPlayerPacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        MId = comp.MIds[(int)PacketType.PlayerControlUpdate],
                        PType = PacketType.PlayerControlUpdate,
                        Data = comp.State.Value.CurrentPlayerControl,
                    }
                });
            }
        }
        
        internal void SendFakeTargetUpdate(GridAi ai, Vector3 hitPos)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FakeTargetPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = ai.Session.MultiplayerId,
                    PType = PacketType.FakeTargetUpdate,
                    Data = hitPos,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FakeTargetPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = ai.Session.MultiplayerId,
                        PType = PacketType.FakeTargetUpdate,
                        Data = hitPos,
                    }
                });
            }
        }
        
        internal void SendCompStateUpdate(WeaponComponent comp)
        {
            comp.MIds[(int)PacketType.CompStateUpdate]++;

            if (IsClient)// client, send settings to server
            {
                PacketsToServer.Add(new StatePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    PType = PacketType.CompStateUpdate,
                    SenderId = MultiplayerId,
                    Data = comp.State.Value,
                    MId = comp.MIds[(int)PacketType.CompStateUpdate]
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new StatePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = PacketType.CompStateUpdate,
                        Data = comp.State.Value
                    }
                });
            }
        }

        internal void SendCompSettingUpdate(WeaponComponent comp)
        {
            comp.MIds[(int)PacketType.CompSettingsUpdate]++;

            if (IsClient)// client, send settings to server
            {
                PacketsToServer.Add(new SettingPacket
                {
                    EntityId = comp.MyCube.EntityId,
                    PType = PacketType.CompSettingsUpdate,
                    SenderId = MultiplayerId,
                    Data = comp.Set.Value,
                    MId = comp.MIds[(int)PacketType.CompSettingsUpdate]
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new SettingPacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = PacketType.CompSettingsUpdate,
                        Data = comp.Set.Value,
                        MId = comp.MIds[(int)PacketType.CompSettingsUpdate]
                    }
                });
            }
        }

        internal void SendUpdateRequest(long entityId, PacketType ptype)
        {
            PacketsToServer.Add(new Packet
            {
                EntityId = entityId,
                SenderId = MultiplayerId,
                PType = ptype
            });
        }
        
        internal void SendTrackReticleUpdate(WeaponComponent comp)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReticleUpdate,
                    Data = comp.TrackReticle
                });
            }
            else if (IsServer)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new BoolUpdatePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        PType = PacketType.ReticleUpdate,
                        Data = comp.TrackReticle
                    }
                });
            }
        }

        internal void SendPlayerConnectionUpdate(long id, bool connected)
        {
            PacketsToClient.Add(new PacketInfo
            {
                Entity = null,
                Packet = new BoolUpdatePacket
                {
                    EntityId = id,
                    SenderId = 0,
                    PType = PacketType.PlayerIdUpdate,
                    Data = connected
                }
            });
        }
        
        internal void SendTargetExpiredUpdate(WeaponComponent comp, int weaponId)
        {
            PacketsToClient.Add(new PacketInfo
            {
                Entity = comp.MyCube,
                Packet = new WeaponIdPacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = 0,
                    PType = PacketType.TargetExpireUpdate,
                    WeaponId = weaponId,
                }
            });
        }

        internal void SendGroupUpdate(GridAi ai)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new Packet
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.RescanGroupRequest,
                });
            }
            else if (IsServer)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new Packet
                    {
                        EntityId = ai.MyGrid.EntityId,
                        PType = PacketType.RescanGroupRequest,
                    }
                });
            }
        }

        internal void SendFixedGunHitEvent(MyCubeBlock firingCube, MyEntity hitEnt, Vector3 origin, Vector3 velocity, Vector3 up, int muzzleId, int systemId, int ammoIndex, float maxTrajectory)
        {
            if (firingCube == null) return;

            var comp = firingCube.Components.Get<WeaponComponent>();

            int weaponId;
            if(comp.Ai?.MyGrid != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.Platform.Structure.HashToId.TryGetValue(systemId, out weaponId))
            {
                PacketsToServer.Add(new FixedWeaponHitPacket
                {
                    EntityId = firingCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.FixedWeaponHitEvent,
                    HitEnt = hitEnt.EntityId,
                    HitOffset = hitEnt.PositionComp.WorldMatrixRef.Translation - origin,
                    Up = up,
                    MuzzleId = muzzleId,
                    WeaponId = weaponId,
                    Velocity = velocity,
                    AmmoIndex = ammoIndex,
                    MaxTrajectory = maxTrajectory,
                });
            }
        }

        internal void SendMidResync(PacketType type, uint mid, ulong playerId, MyEntity ent, WeaponComponent comp)
        {
            var hash = -1;
            if (comp != null)
                hash = comp.GetSyncHash();

            PacketsToClient.Add(new PacketInfo
            {
                Entity = ent,
                Packet = new ClientMIdUpdatePacket
                {
                    EntityId = ent.EntityId,
                    SenderId = playerId,
                    PType = PacketType.ClientMidUpdate,
                    MidType = type,
                    MId = mid,
                    HashCheck = hash,
                },
                SingleClient = true,
            });
        }

        internal void RequestCompSync(WeaponComponent comp)
        {
            PacketsToServer.Add(new Packet
            {
                EntityId = comp.MyCube.EntityId,
                SenderId = MultiplayerId,
                PType = PacketType.CompSyncRequest,
            });
        }

        #region AIFocus packets
        internal void SendFocusTargetUpdate(GridAi ai, long targetId)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.FocusUpdate,
                    TargetId = targetId
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.FocusUpdate,
                        TargetId = targetId
                    }
                });
            }
        }

        internal void SendReassignTargetUpdate(GridAi ai, long targetId, int focusId)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReassignTargetUpdate,
                    TargetId = targetId,
                    FocusId = focusId
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ReassignTargetUpdate,
                        TargetId = targetId,
                        FocusId = focusId
                    }
                });
            }
        }

        internal void SendNextActiveUpdate(GridAi ai, bool addSecondary)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.NextActiveUpdate,
                    AddSecondary = addSecondary
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.NextActiveUpdate,
                        AddSecondary = addSecondary
                    }
                });
            }
        }

        internal void SendReleaseActiveUpdate(GridAi ai)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReleaseActiveUpdate
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ReleaseActiveUpdate
                    }
                });
            }
        }
        #endregion
        #endregion


        #region Misc Network Methods
        internal void SyncWeapon(Weapon weapon, WeaponTimings timings, ref WeaponSyncValues weaponData, bool setState = true)
        {
            if (weapon.System.DesignatorWeapon) return;

            var comp = weapon.Comp;
            var cState = comp.State.Value;
            var wState = weapon.State;

            var wasReloading = wState.Sync.Reloading;

            if (setState)
            {
                comp.CurrentHeat -= weapon.State.Sync.Heat;
                cState.CurrentCharge -= weapon.State.Sync.CurrentCharge;


                weaponData.SetState(wState.Sync);

                comp.CurrentHeat += weapon.State.Sync.Heat;
                cState.CurrentCharge += weapon.State.Sync.CurrentCharge;
            }

            comp.WeaponValues.Timings[weapon.WeaponId].Sync(timings);
            weapon.Timings.Sync(timings);

            var hasMags = weapon.State.Sync.CurrentMags > 0 || IsCreative;
            var hasAmmo = weapon.State.Sync.CurrentAmmo > 0;

            var canReload = weapon.CanReload;

            if (!canReload)
                weapon.ChangeActiveAmmo(weapon.System.WeaponAmmoTypes[weapon.Set.AmmoTypeId]);

            if (canReload)
                weapon.StartReload();

            else if (wasReloading && !weapon.State.Sync.Reloading && hasAmmo)
            {
                if (!weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && weapon.ReloadSubscribed)
                {
                    weapon.ReloadSubscribed = false;
                    weapon.CancelableReloadAction -= weapon.Reloaded;
                }

                weapon.EventTriggerStateChanged(EventTriggers.Reloading, false);
            }
            else if (wasReloading && weapon.State.Sync.Reloading && !weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge)
            {
                if (weapon.ReloadSubscribed)
                {
                    weapon.ReloadSubscribed = false;
                    weapon.CancelableReloadAction -= weapon.Reloaded;
                }

                comp.Session.FutureEvents.Schedule(weapon.Reloaded, null, weapon.Timings.ReloadedTick);
            }

            else if (weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && weapon.State.Sync.Reloading && !comp.Session.ChargingWeaponsIndexer.ContainsKey(weapon))
                weapon.ChargeReload(true);

            if (weapon.State.Sync.Heat > 0 && !weapon.HeatLoopRunning)
            {
                weapon.HeatLoopRunning = true;
                var delay = weapon.Timings.LastHeatUpdateTick > 0 ? weapon.Timings.LastHeatUpdateTick : 20;
                comp.Session.FutureEvents.Schedule(weapon.UpdateWeaponHeat, null, delay);
            }
        }

        public void UpdateActiveControlDictionary(MyCubeBlock cube, long playerId, bool updateAdd, bool applyToRoot = false)
        {
            GridAi trackingAi;
            if (updateAdd) //update/add
            {

                if (applyToRoot && GridToMasterAi.TryGetValue(cube.CubeGrid, out trackingAi) || GridTargetingAIs.TryGetValue(cube.CubeGrid, out trackingAi)) {
                    trackingAi.ControllingPlayers[playerId] = cube;
                    trackingAi.AiSleep = false;
                }
            }
            else //remove
            {
                if (applyToRoot && GridToMasterAi.TryGetValue(cube.CubeGrid, out trackingAi) || GridTargetingAIs.TryGetValue(cube.CubeGrid, out trackingAi)) {
                    trackingAi.ControllingPlayers.Remove(playerId);
                    trackingAi.AiSleep = false;
                }
            }
        }

        internal static void SyncGridOverrides(GridAi ai, string groupName, GroupOverrides o)
        {
            ai.BlockGroups[groupName].Settings["Active"] = o.Activate ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Neutrals"] = o.Neutrals ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Projectiles"] = o.Projectiles ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Biologicals"] = o.Biologicals ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Meteors"] = o.Meteors ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Friendly"] = o.Friendly ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Unowned"] = o.Unowned ? 1 : 0;
            ai.BlockGroups[groupName].Settings["TargetPainter"] = o.TargetPainter ? 1 : 0;
            ai.BlockGroups[groupName].Settings["ManualControl"] = o.ManualControl ? 1 : 0;
            ai.BlockGroups[groupName].Settings["FocusTargets"] = o.FocusTargets ? 1 : 0;
            ai.BlockGroups[groupName].Settings["FocusSubSystem"] = o.FocusSubSystem ? 1 : 0;
            ai.BlockGroups[groupName].Settings["SubSystems"] = (int)o.SubSystem;
        }

        internal static GroupOverrides GetOverrides(GridAi ai, string groupName)
        {
            var o = new GroupOverrides();
            o.Activate = ai.BlockGroups[groupName].Settings["Active"] == 1 ? true : false;
            o.Neutrals = ai.BlockGroups[groupName].Settings["Neutrals"] == 1 ? true : false;
            o.Projectiles = ai.BlockGroups[groupName].Settings["Projectiles"] == 1 ? true : false;
            o.Biologicals = ai.BlockGroups[groupName].Settings["Biologicals"] == 1 ? true : false;
            o.Meteors = ai.BlockGroups[groupName].Settings["Meteors"] == 1 ? true : false;
            o.Friendly = ai.BlockGroups[groupName].Settings["Friendly"] == 1 ? true : false;
            o.Unowned = ai.BlockGroups[groupName].Settings["Unowned"] == 1 ? true : false;
            o.TargetPainter = ai.BlockGroups[groupName].Settings["TargetPainter"] == 1 ? true : false;
            o.ManualControl = ai.BlockGroups[groupName].Settings["ManualControl"] == 1 ? true : false;
            o.FocusTargets = ai.BlockGroups[groupName].Settings["FocusTargets"] == 1 ? true : false;
            o.FocusSubSystem = ai.BlockGroups[groupName].Settings["FocusSubSystem"] == 1 ? true : false;
            o.SubSystem = (BlockTypes)ai.BlockGroups[groupName].Settings["SubSystems"];

            return o;
        }

        /*
        internal static void CreateFixedWeaponProjectile(Weapon weapon, MyEntity targetEntity, Vector3 origin, Vector3 direction, Vector3 velocity, Vector3 originUp, int muzzleId, AmmoDef ammoDef, float maxTrajectory)
        {
            var comp = weapon.Comp;
            var p = comp.Session.Projectiles.ProjectilePool.Count > 0 ? comp.Session.Projectiles.ProjectilePool.Pop() : new Projectile();
            p.Info.Id = comp.Session.Projectiles.CurrentProjectileId++;
            p.Info.System = weapon.System;
            p.Info.Ai = comp.Ai;
            p.Info.ClientSent = true;
            p.Info.AmmoDef = ammoDef;
            p.Info.Overrides = comp.Set.Value.Overrides;
            p.Info.Target.Entity = targetEntity;
            p.Info.Target.Projectile = null;
            p.Info.Target.IsProjectile = false;
            p.Info.Target.IsFakeTarget = false;
            p.Info.Target.FiringCube = comp.MyCube;
            p.Info.WeaponId = weapon.WeaponId;
            p.Info.MuzzleId = muzzleId;
            p.Info.BaseDamagePool = weapon.BaseDamage;
            p.Info.EnableGuidance = false;
            p.Info.WeaponCache = weapon.WeaponCache;
            p.Info.WeaponCache.VirutalId = -1;
            p.Info.WeaponRng = comp.WeaponValues.WeaponRandom[weapon.WeaponId];
            p.Info.LockOnFireState = false;
            p.Info.ShooterVel = comp.Ai.GridVel;
            p.Velocity = velocity;
            p.Info.Origin = origin;
            p.Info.OriginUp = originUp;
            p.PredictedTargetPos = Vector3D.Zero;
            p.Info.Direction = direction;
            p.State = Projectile.ProjectileState.Start;
            p.Info.PrimeEntity = weapon.ActiveAmmoDef.AmmoDef.Const.PrimeModel ? weapon.ActiveAmmoDef.AmmoDef.Const.PrimeEntityPool.Get() : null;
            p.Info.TriggerEntity = weapon.ActiveAmmoDef.AmmoDef.Const.TriggerModel ? comp.Session.TriggerEntityPool.Get() : null;
            p.Info.MaxTrajectory = maxTrajectory;

            comp.Session.Projectiles.ActiveProjetiles.Add(p);
        }*/

        internal static void CreateFixedWeaponProjectile(Weapon weapon, MyEntity targetEntity, Vector3 origin, Vector3 direction, Vector3 velocity, Vector3 originUp, int muzzleId, AmmoDef ammoDef, float maxTrajectory)
        {
            var muzzle = weapon.Muzzles[muzzleId];
            var session = weapon.Comp.Session;
            session.Projectiles.NewProjectiles.Add(new NewProjectile { AmmoDef = ammoDef, Muzzle = muzzle, Weapon = weapon, TargetEnt = targetEntity, Origin = origin, OriginUp = originUp, Direction = direction, Velocity = velocity, MaxTrajectory = maxTrajectory, Type = NewProjectile.Kind.Client });
        }

        private bool AuthorDebug()
        {
            var authorsOffline = ConnectedAuthors.Count == 0;
            if (authorsOffline)
            {
                AuthLogging = false;
                return false;
            }

            foreach (var a in ConnectedAuthors)
            {
                var authorsFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(a.Key);
                if (authorsFaction == null || string.IsNullOrEmpty(authorsFaction.PrivateInfo))
                {
                    AuthLogging = false;
                    continue;
                }

                var length = authorsFaction.PrivateInfo.Length;

                var debugLog = length > 0 && int.TryParse(authorsFaction.PrivateInfo[0].ToString(), out AuthorSettings[0]);
                if (!debugLog) AuthorSettings[0] = -1;

                var perfLog = length > 1 && int.TryParse(authorsFaction.PrivateInfo[1].ToString(), out AuthorSettings[1]);
                if (!perfLog) AuthorSettings[1] = -1;

                var statsLog = length > 2 && int.TryParse(authorsFaction.PrivateInfo[2].ToString(), out AuthorSettings[2]);
                if (!statsLog) AuthorSettings[2] = -1;

                var netLog = length > 3 && int.TryParse(authorsFaction.PrivateInfo[3].ToString(), out AuthorSettings[3]);
                if (!netLog) AuthorSettings[3] = -1;

                var customLog = length > 4 && int.TryParse(authorsFaction.PrivateInfo[4].ToString(), out AuthorSettings[4]);
                if (!customLog) AuthorSettings[4] = -1;

                var hasLevel = length > 5 && int.TryParse(authorsFaction.PrivateInfo[5].ToString(), out AuthorSettings[5]);
                if (!hasLevel) AuthorSettings[5] = -1;


                if ((debugLog || perfLog || netLog || customLog || statsLog) && hasLevel)
                {
                    AuthLogging = true;
                    LogLevel = AuthorSettings[5];
                    return true;
                }

                for (int i = 0; i < AuthorSettings.Length; i++)
                    AuthorSettings[i] = -1;
                AuthLogging = false;

            }
            return false;
        }
        #endregion
    }
}
