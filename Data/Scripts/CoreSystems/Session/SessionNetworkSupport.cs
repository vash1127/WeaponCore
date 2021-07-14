﻿using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static CoreSystems.Support.CoreComponent;

namespace CoreSystems
{
    public partial class Session
    {

        #region Packet Creation Methods
        internal class PacketObj
        {
            internal readonly ErrorPacket ErrorPacket = new ErrorPacket();
            internal Packet Packet;
            internal NetworkReporter.Report Report;
            internal int PacketSize;

            internal void Clean()
            {
                Packet = null;
                Report = null;
                PacketSize = 0;
                ErrorPacket.CleanUp();
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

        private long _lastFakeTargetUpdateErrorId = long.MinValue;
        private bool Error(PacketObj data, params NetResult[] messages)
        {
            var fakeTargetUpdateError = data.Packet.PType == PacketType.AimTargetUpdate;

            if (fakeTargetUpdateError)
            {
                if (data.Packet.EntityId == _lastFakeTargetUpdateErrorId)
                    return false;
                _lastFakeTargetUpdateErrorId = data.Packet.EntityId;
            }

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

            public void CleanUp()
            {
                RecievedTick = 0;
                RetryTick = 0;
                RetryDelayTicks = 0;
                RetryAttempt = 0;
                MaxAttempts = 0;
                NoReprocess = false;
                Retry = false;
                Error = string.Empty;
            }
        }
        #endregion

        #region ServerOnly
        internal void SendConstruct(Ai ai)
        {
            if (IsServer)
            {

                PrunedPacketsToClient.Remove(ai.Construct.Data.Repo.FocusData);
                ++ai.Construct.Data.Repo.FocusData.Revision;

                PacketInfo oldInfo;
                ConstructPacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(ai.Construct.Data.Repo, out oldInfo))
                {
                    iPacket = (ConstructPacket)oldInfo.Packet;
                    iPacket.EntityId = ai.TopEntity.EntityId;
                    iPacket.Data = ai.Construct.Data.Repo;
                }
                else
                {
                    iPacket = PacketConstructPool.Get();
                    iPacket.EntityId = ai.TopEntity.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = PacketType.Construct;
                    iPacket.Data = ai.Construct.Data.Repo;
                }

                PrunedPacketsToClient[ai.Construct.Data.Repo] = new PacketInfo
                {
                    Entity = ai.TopEntity,
                    Packet = iPacket,
                };
            }
            else Log.Line("SendConstruct should never be called on Client");
        }

        internal void SendConstructFoci(Ai ai)
        {
            if (IsServer)
            {

                ++ai.Construct.Data.Repo.FocusData.Revision;

                if (!PrunedPacketsToClient.ContainsKey(ai.Construct.Data.Repo))
                {
                    PacketInfo oldInfo;
                    ConstructFociPacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(ai.Construct.Data.Repo.FocusData, out oldInfo))
                    {
                        iPacket = (ConstructFociPacket)oldInfo.Packet;
                        iPacket.EntityId = ai.TopEntity.EntityId;
                        iPacket.Data = ai.Construct.Data.Repo.FocusData;
                    }
                    else
                    {
                        iPacket = PacketConstructFociPool.Get();
                        iPacket.EntityId = ai.TopEntity.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = PacketType.ConstructFoci;
                        iPacket.Data = ai.Construct.Data.Repo.FocusData;
                    }

                    PrunedPacketsToClient[ai.Construct.Data.Repo.FocusData] = new PacketInfo
                    {
                        Entity = ai.TopEntity,
                        Packet = iPacket,
                    };
                }
                else SendConstruct(ai);

            }
            else Log.Line("SendConstructGroups should never be called on Client");
        }

        internal void SendAiData(Ai ai)
        {
            if (IsServer)
            {

                PacketInfo oldInfo;
                AiDataPacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(ai.Data.Repo, out oldInfo))
                {
                    iPacket = (AiDataPacket)oldInfo.Packet;
                    iPacket.EntityId = ai.TopEntity.EntityId;
                    iPacket.Data = ai.Data.Repo;
                }
                else
                {

                    iPacket = PacketAiPool.Get();
                    iPacket.EntityId = ai.TopEntity.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = PacketType.AiData;
                    iPacket.Data = ai.Data.Repo;
                }

                ++ai.Data.Repo.Revision;

                PrunedPacketsToClient[ai.Data.Repo] = new PacketInfo
                {
                    Entity = ai.TopEntity,
                    Packet = iPacket,
                };
            }
            else Log.Line("SendAiData should never be called on Client");
        }

        internal void SendWeaponAmmoData(Weapon w)
        {
            if (IsServer)
            {

                const PacketType type = PacketType.WeaponAmmo;
                ++w.ProtoWeaponAmmo.Revision;

                PacketInfo oldInfo;
                WeaponAmmoPacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(w.ProtoWeaponAmmo, out oldInfo))
                {
                    iPacket = (WeaponAmmoPacket)oldInfo.Packet;
                    iPacket.EntityId = w.BaseComp.CoreEntity.EntityId;
                    iPacket.Data = w.ProtoWeaponAmmo;
                }
                else
                {

                    iPacket = PacketAmmoPool.Get();
                    iPacket.EntityId = w.BaseComp.CoreEntity.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = type;
                    iPacket.Data = w.ProtoWeaponAmmo;
                    iPacket.PartId = w.PartId;
                }


                PrunedPacketsToClient[w.ProtoWeaponAmmo] = new PacketInfo
                {
                    Entity = w.BaseComp.CoreEntity,
                    Packet = iPacket,
                };
            }
            else Log.Line("SendWeaponAmmoData should never be called on Client");
        }

        internal void SendComp(Weapon.WeaponComponent comp)
        {
            if (IsServer)
            {

                const PacketType type = PacketType.WeaponComp;
                comp.Data.Repo.Values.UpdateCompPacketInfo(comp, true);

                PacketInfo oldInfo;
                WeaponCompPacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(comp.Data.Repo.Values, out oldInfo))
                {
                    iPacket = (WeaponCompPacket)oldInfo.Packet;
                    iPacket.EntityId = comp.CoreEntity.EntityId;
                    iPacket.Data = comp.Data.Repo.Values;
                }
                else
                {

                    iPacket = PacketWeaponCompPool.Get();
                    iPacket.EntityId = comp.CoreEntity.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = type;
                    iPacket.Data = comp.Data.Repo.Values;
                }

                PrunedPacketsToClient[comp.Data.Repo.Values] = new PacketInfo
                {
                    Entity = comp.CoreEntity,
                    Packet = iPacket,
                };
            }
            else Log.Line("SendComp should never be called on Client");
        }

        internal void SendComp(Upgrade.UpgradeComponent comp)
        {
            if (IsServer)
            {

                const PacketType type = PacketType.UpgradeComp;
                comp.Data.Repo.Values.UpdateCompPacketInfo(comp, true);

                PacketInfo oldInfo;
                UpgradeCompPacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(comp.Data.Repo.Values, out oldInfo))
                {
                    iPacket = (UpgradeCompPacket)oldInfo.Packet;
                    iPacket.EntityId = comp.CoreEntity.EntityId;
                    iPacket.Data = comp.Data.Repo.Values;
                }
                else
                {

                    iPacket = PacketUpgradeCompPool.Get();
                    iPacket.EntityId = comp.CoreEntity.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = type;
                    iPacket.Data = comp.Data.Repo.Values;
                }

                PrunedPacketsToClient[comp.Data.Repo.Values] = new PacketInfo
                {
                    Entity = comp.CoreEntity,
                    Packet = iPacket,
                };
            }
            else Log.Line("SendComp should never be called on Client");
        }

        internal void SendComp(SupportSys.SupportComponent comp)
        {
            if (IsServer)
            {

                const PacketType type = PacketType.SupportComp;
                comp.Data.Repo.Values.UpdateCompPacketInfo(comp, true);

                PacketInfo oldInfo;
                SupportCompPacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(comp.Data.Repo.Values, out oldInfo))
                {
                    iPacket = (SupportCompPacket)oldInfo.Packet;
                    iPacket.EntityId = comp.CoreEntity.EntityId;
                    iPacket.Data = comp.Data.Repo.Values;
                }
                else
                {

                    iPacket = PacketSupportCompPool.Get();
                    iPacket.EntityId = comp.CoreEntity.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = type;
                    iPacket.Data = comp.Data.Repo.Values;
                }

                PrunedPacketsToClient[comp.Data.Repo.Values] = new PacketInfo
                {
                    Entity = comp.CoreEntity,
                    Packet = iPacket,
                };
            }
            else Log.Line("SendComp should never be called on Client");
        }

        internal void SendState(Weapon.WeaponComponent comp)
        {
            if (IsServer)
            {

                if (!comp.Session.PrunedPacketsToClient.ContainsKey(comp.Data.Repo.Values))
                {

                    const PacketType type = PacketType.WeaponState;
                    comp.Data.Repo.Values.UpdateCompPacketInfo(comp);

                    PacketInfo oldInfo;
                    WeaponStatePacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(comp.Data.Repo.Values.State, out oldInfo))
                    {
                        iPacket = (WeaponStatePacket)oldInfo.Packet;
                        iPacket.EntityId = comp.CoreEntity.EntityId;
                        iPacket.Data = comp.Data.Repo.Values.State;
                    }
                    else
                    {
                        iPacket = PacketWeaponStatePool.Get();
                        iPacket.EntityId = comp.CoreEntity.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = type;
                        iPacket.Data = comp.Data.Repo.Values.State;
                    }

                    PrunedPacketsToClient[comp.Data.Repo.Values.State] = new PacketInfo
                    {
                        Entity = comp.CoreEntity,
                        Packet = iPacket,
                    };
                }
                else
                    SendComp(comp);

            }
            else Log.Line("SendState should never be called on Client");
        }

        internal void SendState(SupportSys.SupportComponent comp)
        {
            if (IsServer)
            {

                if (!comp.Session.PrunedPacketsToClient.ContainsKey(comp.Data.Repo.Values))
                {

                    const PacketType type = PacketType.SupportState;
                    comp.Data.Repo.Values.UpdateCompPacketInfo(comp);

                    PacketInfo oldInfo;
                    SupportStatePacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(comp.Data.Repo.Values.State, out oldInfo))
                    {
                        iPacket = (SupportStatePacket)oldInfo.Packet;
                        iPacket.EntityId = comp.CoreEntity.EntityId;
                        iPacket.Data = comp.Data.Repo.Values.State;
                    }
                    else
                    {
                        iPacket = PacketSupportStatePool.Get();
                        iPacket.EntityId = comp.CoreEntity.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = type;
                        iPacket.Data = comp.Data.Repo.Values.State;
                    }

                    PrunedPacketsToClient[comp.Data.Repo.Values.State] = new PacketInfo
                    {
                        Entity = comp.CoreEntity,
                        Packet = iPacket,
                    };
                }
                else
                    SendComp(comp);

            }
            else Log.Line("SendState should never be called on Client");
        }

        internal void SendState(Upgrade.UpgradeComponent comp)
        {
            if (IsServer)
            {

                if (!comp.Session.PrunedPacketsToClient.ContainsKey(comp.Data.Repo.Values))
                {

                    const PacketType type = PacketType.UpgradeState;
                    comp.Data.Repo.Values.UpdateCompPacketInfo(comp);

                    PacketInfo oldInfo;
                    UpgradeStatePacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(comp.Data.Repo.Values.State, out oldInfo))
                    {
                        iPacket = (UpgradeStatePacket)oldInfo.Packet;
                        iPacket.EntityId = comp.CoreEntity.EntityId;
                        iPacket.Data = comp.Data.Repo.Values.State;
                    }
                    else
                    {
                        iPacket = PacketUpgradeStatePool.Get();
                        iPacket.EntityId = comp.CoreEntity.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = type;
                        iPacket.Data = comp.Data.Repo.Values.State;
                    }

                    PrunedPacketsToClient[comp.Data.Repo.Values.State] = new PacketInfo
                    {
                        Entity = comp.CoreEntity,
                        Packet = iPacket,
                    };
                }
                else
                    SendComp(comp);

            }
            else Log.Line("SendState should never be called on Client");
        }

        internal void SendTargetChange(Weapon.WeaponComponent comp, int partId)
        {
            if (IsServer)
            {

                if (!comp.Session.PrunedPacketsToClient.ContainsKey(comp.Data.Repo.Values))
                {

                    const PacketType type = PacketType.TargetChange;
                    comp.Data.Repo.Values.UpdateCompPacketInfo(comp);

                    var collection = comp.TypeSpecific != CompTypeSpecific.Phantom ? comp.Platform.Weapons : comp.Platform.Phantoms;
                    var w = collection[partId];
                    PacketInfo oldInfo;
                    TargetPacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(w.TargetData, out oldInfo))
                    {
                        iPacket = (TargetPacket)oldInfo.Packet;
                        iPacket.EntityId = comp.CoreEntity.EntityId;
                        iPacket.Target = w.TargetData;
                    }
                    else
                    {
                        iPacket = PacketTargetPool.Get();
                        iPacket.EntityId = comp.CoreEntity.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = type;
                        iPacket.Target = w.TargetData;
                    }


                    PrunedPacketsToClient[w.TargetData] = new PacketInfo
                    {
                        Entity = comp.CoreEntity,
                        Packet = iPacket,
                    };
                }
                else
                    SendComp(comp);
            }
            else Log.Line("SendTargetChange should never be called on Client");
        }

        internal void SendWeaponReload(Weapon w)
        {
            if (IsServer)
            {

                if (!PrunedPacketsToClient.ContainsKey(w.Comp.Data.Repo.Values))
                {

                    const PacketType type = PacketType.WeaponReload;
                    w.Comp.Data.Repo.Values.UpdateCompPacketInfo(w.Comp);

                    PacketInfo oldInfo;
                    WeaponReloadPacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(w.Reload, out oldInfo))
                    {
                        iPacket = (WeaponReloadPacket)oldInfo.Packet;
                        iPacket.EntityId = w.Comp.CoreEntity.EntityId;
                        iPacket.Data = w.Reload;
                    }
                    else
                    {
                        iPacket = PacketReloadPool.Get();
                        iPacket.EntityId = w.Comp.CoreEntity.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = type;
                        iPacket.Data = w.Reload;
                        iPacket.PartId = w.PartId;
                    }

                    PrunedPacketsToClient[w.Reload] = new PacketInfo
                    {
                        Entity = w.Comp.CoreEntity,
                        Packet = iPacket,
                    };
                }
                else
                    SendComp(w.Comp);
            }
            else Log.Line("SendWeaponReload should never be called on Client");
        }

        internal void SendClientNotify(long id, string message, bool singleClient = false, string color = null, int duration = 0)
        {
            ulong senderId = 0;
            IMyPlayer player = null;
            if (singleClient && Players.TryGetValue(id, out player))
                senderId = player.SteamUserId;

            PacketsToClient.Add(new PacketInfo
            {
                Entity = null,
                SingleClient = singleClient,
                Packet = new ClientNotifyPacket
                {
                    EntityId = id,
                    SenderId = senderId,
                    PType = PacketType.ClientNotify,
                    Message = message,
                    Color = color,
                    Duration = duration,
                }
            });
        }

        internal void SendPlayerConnectionUpdate(long id, bool connected)
        {
            if (IsServer)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = null,
                    Packet = new BoolUpdatePacket
                    {
                        EntityId = id,
                        SenderId = MultiplayerId,
                        PType = PacketType.PlayerIdUpdate,
                        Data = connected
                    }
                });
            }
            else Log.Line("SendPlayerConnectionUpdate should only be called on server");
        }

        internal void SendServerStartup(ulong id)
        {
            if (IsServer)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = null,
                    SingleClient = true,
                    Packet = new ServerPacket
                    {
                        EntityId = 0,
                        SenderId = id,
                        PType = PacketType.ServerData,
                        VersionString = ModContext.ModName,
                        Data = Settings.Enforcement,
                    }
                });
            }
            else Log.Line("SendServerVersion should only be called on server");
        }
        #endregion

        #region ClientOnly
        internal void SendUpdateRequest(long entityId, PacketType ptype)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new Packet
                {
                    EntityId = entityId,
                    SenderId = MultiplayerId,
                    PType = ptype
                });
            }
            else Log.Line("SendUpdateRequest should only be called on clients");
        }

        internal void SendOverRidesClientComp(CoreComponent comp, string settings, int value)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new OverRidesPacket
                {
                    PType = PacketType.OverRidesUpdate,
                    EntityId = comp.CoreEntity.EntityId,
                    SenderId = MultiplayerId,
                    Setting = settings,
                    Value = value,
                });
            }
            else Log.Line("SendOverRidesClientComp should only be called on clients");
        }

        internal void SendFixedGunHitEvent(MyEntity triggerEntity, MyEntity hitEnt, Vector3 origin, Vector3 velocity, Vector3 up, int muzzleId, int systemId, int ammoIndex, float maxTrajectory)
        {
            if (triggerEntity == null) return;

            var comp = triggerEntity.Components.Get<CoreComponent>();

            int weaponId;
            if (comp.Ai?.TopEntity != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Platform.Structure.HashToId.TryGetValue(systemId, out weaponId))
            {
                PacketsToServer.Add(new FixedWeaponHitPacket
                {
                    EntityId = triggerEntity.EntityId,
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

        #endregion

        #region AIFocus packets
        internal void SendFocusTargetUpdate(Ai ai, long targetId)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.TopEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.FocusUpdate,
                    TargetId = targetId
                });

            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.TopEntity,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.TopEntity.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.FocusUpdate,
                        TargetId = targetId
                    }
                });
            }
        }

        internal void SendFocusLockUpdate(Ai ai)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.TopEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.FocusLockUpdate,
                });

            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.TopEntity,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.TopEntity.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.FocusLockUpdate,
                    }
                });
            }
        }

        internal void SendNextActiveUpdate(Ai ai, bool addSecondary)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.TopEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.NextActiveUpdate,
                    AddSecondary = addSecondary
                });

            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.TopEntity,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.TopEntity.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.NextActiveUpdate,
                        AddSecondary = addSecondary
                    }
                });
            }
        }

        internal void SendReleaseActiveUpdate(Ai ai)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.TopEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReleaseActiveUpdate
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.TopEntity,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.TopEntity.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ReleaseActiveUpdate
                    }
                });
            }
        }
        #endregion


        internal void SendMouseUpdate(Ai ai, MyEntity entity)
        {
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
            else if (HandlesInput)
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
            else Log.Line("SendMouseUpdate should never be called on Dedicated");
        }

        internal void SendActiveControlUpdate(Ai ai, MyEntity entity, bool active)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = entity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ActiveControlUpdate,
                    Data = active
                });
            }
            else if (HandlesInput)
            {
                ai.Construct.UpdateConstructsPlayers(entity, PlayerId, active);
            }
            else Log.Line("SendActiveControlUpdate should never be called on Dedicated");
        }

        internal void SendActionShootUpdate(CoreComponent comp, TriggerActions action)
        {
            if (IsClient)
            {
                comp.Session.PacketsToServer.Add(new ShootStatePacket
                {
                    EntityId = comp.CoreEntity.EntityId,
                    SenderId = comp.Session.MultiplayerId,
                    PType = PacketType.RequestShootUpdate,
                    Action = action,
                    PlayerId = PlayerId,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.CoreEntity,
                    Packet = new ShootStatePacket
                    {
                        EntityId = comp.CoreEntity.EntityId,
                        SenderId = comp.Session.MultiplayerId,
                        PType = PacketType.RequestShootUpdate,
                        Action = action,
                        PlayerId = PlayerId,
                    }
                });
            }
            else Log.Line("SendActionShootUpdate should never be called on Dedicated");
        }

        internal void SendActiveTerminal(CoreComponent comp)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new TerminalMonitorPacket
                {
                    SenderId = MultiplayerId,
                    PType = PacketType.TerminalMonitor,
                    EntityId = comp.CoreEntity.EntityId,
                    State = TerminalMonitorPacket.Change.Update,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.CoreEntity,
                    Packet = new TerminalMonitorPacket
                    {
                        SenderId = MultiplayerId,
                        PType = PacketType.TerminalMonitor,
                        EntityId = comp.CoreEntity.EntityId,
                        State = TerminalMonitorPacket.Change.Update,
                    }
                });
            }
            else Log.Line("SendActiveTerminal should never be called on Dedicated");
        }

        internal void SendAimTargetUpdate(Ai ai, Ai.FakeTarget fake)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FakeTargetPacket
                {
                    EntityId = ai.TopEntity.EntityId,
                    SenderId = ai.Session.MultiplayerId,
                    PType = PacketType.AimTargetUpdate,
                    Pos = fake.EntityId != 0 ? fake.LocalPosition : fake.FakeInfo.WorldPosition,
                    TargetId = fake.EntityId,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.TopEntity,
                    Packet = new FakeTargetPacket
                    {
                        EntityId = ai.TopEntity.EntityId,
                        SenderId = ai.Session.MultiplayerId,
                        PType = PacketType.AimTargetUpdate,
                        Pos = fake.EntityId != 0 ? fake.LocalPosition : fake.FakeInfo.WorldPosition,
                        TargetId = fake.EntityId,
                    }
                });
            }
            else Log.Line($"SendAimTargetUpdate should never be called on Dedicated");
        }

        internal void SendPaintedTargetUpdate(Ai ai, Ai.FakeTarget fake)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FakeTargetPacket
                {
                    EntityId = ai.TopEntity.EntityId,
                    SenderId = ai.Session.MultiplayerId,
                    PType = PacketType.PaintedTargetUpdate,
                    Pos = fake.EntityId != 0 ? fake.LocalPosition : fake.FakeInfo.WorldPosition,
                    TargetId = fake.EntityId,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.TopEntity,
                    Packet = new FakeTargetPacket
                    {
                        EntityId = ai.TopEntity.EntityId,
                        SenderId = ai.Session.MultiplayerId,
                        PType = PacketType.PaintedTargetUpdate,
                        Pos = fake.EntityId != 0 ? fake.LocalPosition : fake.FakeInfo.WorldPosition,
                        TargetId = fake.EntityId,
                    }
                });
            }
            else Log.Line($"SendPaintedTargetUpdate should never be called on Dedicated");
        }


        internal void SendPlayerControlRequest(CoreComponent comp, long playerId, ProtoWeaponState.ControlMode mode)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new PlayerControlRequestPacket
                {
                    EntityId = comp.CoreEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.PlayerControlRequest,
                    PlayerId = playerId,
                    Mode = mode,
                });
            }
            else if (HandlesInput)
            {
                SendComp((Weapon.WeaponComponent)comp);
            }
            else Log.Line("SendPlayerControlRequest should never be called on Server");
        }

        internal void SendEwaredBlocks()
        {
            if (IsServer)
            {
                _cachedEwarPacket.CleanUp();
                _cachedEwarPacket.SenderId = MultiplayerId;
                _cachedEwarPacket.PType = PacketType.EwaredBlocks;
                _cachedEwarPacket.Data.AddRange(DirtyEwarData.Values);

                DirtyEwarData.Clear();
                EwarNetDataDirty = false;

                PacketsToClient.Add(new PacketInfo { Packet = _cachedEwarPacket });
            }
            else Log.Line($"SendEwaredBlocks should never be called on Client");
        }

        internal void SendAmmoCycleRequest(Weapon w, int newAmmoId)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new AmmoCycleRequestPacket
                {
                    EntityId = w.BaseComp.CoreEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.AmmoCycleRequest,
                    PartId = w.PartId,
                    NewAmmoId = newAmmoId,
                    PlayerId = PlayerId,
                });
            }
            else Log.Line("SendAmmoCycleRequest should never be called on Non-Client");
        }

        internal void SendSetCompFloatRequest(CoreComponent comp, float newDps, PacketType type)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FloatUpdatePacket
                {
                    EntityId = comp.CoreEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = type,
                    Data = newDps,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.CoreEntity,
                    Packet = new FloatUpdatePacket
                    {
                        EntityId = comp.CoreEntity.EntityId,
                        SenderId = MultiplayerId,
                        PType = type,
                        Data = newDps,
                    }
                });
            }
            else Log.Line("SendSetFloatRequest should never be called on Non-HandlesInput");
        }

        internal void SendSetCompBoolRequest(CoreComponent comp, bool newBool, PacketType type)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = comp.CoreEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = type,
                    Data = newBool,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.CoreEntity,
                    Packet = new BoolUpdatePacket
                    {
                        EntityId = comp.CoreEntity.EntityId,
                        SenderId = MultiplayerId,
                        PType = type,
                        Data = newBool,
                    }
                });
            }
            else Log.Line("SendSetCompBoolRequest should never be called on Non-HandlesInput");
        }

        internal void SendTrackReticleUpdate(Weapon.WeaponComponent comp, bool track)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = comp.CoreEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReticleUpdate,
                    Data = track
                });
            }
            else if (HandlesInput)
            {
                comp.Data.Repo.Values.State.TrackingReticle = track;
                SendComp(comp);
            }
        }


        internal void SendCountingDownUpdate(Weapon.WeaponComponent comp, bool countingDown)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = comp.CoreEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.CountingDownUpdate,
                    Data = countingDown
                });
            }
            else if (IsServer)
            {
                comp.Data.Repo.Values.State.CountingDown = countingDown;
                if (MpActive) SendComp(comp);
            }
        }

        internal void SendTriggerCriticalReaction(Weapon.WeaponComponent comp)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = comp.CoreEntity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.CountingDownUpdate,
                    Data = true
                });
            }
            else if (IsServer)
            {
                comp.Data.Repo.Values.State.CriticalReaction = true;
                if (MpActive) SendComp(comp);
            }
        }

        #region Misc Network Methods
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
