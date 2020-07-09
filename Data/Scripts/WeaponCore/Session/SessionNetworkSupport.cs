using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Support.WeaponComponent;

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

        internal void SendMouseUpdate(GridAi ai, MyEntity entity)
        {
            if (!HandlesInput) return;

            if (IsClient)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new InputPacket
                    {
                        MId = ++mIds[(int)PacketType.ClientMouseEvent],
                        EntityId = entity.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ClientMouseEvent,
                        Data = UiInput.ClientInputState
                    });
                }
                else Log.Line($"SendMouseUpdate no player MIds found");
            }
            else
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = entity,
                    Packet = new InputPacket
                    {
                        MId = ++ai.MIds[(int)PacketType.ClientMouseEvent],
                        EntityId = entity.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ClientMouseEvent,
                        Data = UiInput.ClientInputState
                    }
                });
            }
        }

        internal void SendActiveControlUpdate(GridAi ai, MyCubeBlock controlBlock, bool active)
        {
            if (IsClient)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new BoolUpdatePacket
                    {
                        MId = ++mIds[(int)PacketType.ActiveControlUpdate],
                        EntityId = controlBlock.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ActiveControlUpdate,
                        Data = active
                    });
                }
                else Log.Line($"SendActiveControlUpdate no player MIds found");
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = controlBlock,
                    Packet = new BoolUpdatePacket
                    {
                        MId = ++ai.MIds[(int)PacketType.ActiveControlUpdate],
                        EntityId = controlBlock.EntityId,
                        SenderId = 0,
                        PType = PacketType.ActiveControlUpdate,
                        Data = active
                    }
                });
            }
        }

        internal void SendActionShootUpdate(WeaponComponent comp, ShootActions action)
        {
            uint[] mIds;
            if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
            {
                comp.Session.PacketsToServer.Add(new ShootStatePacket
                {
                    MId = ++mIds[(int)PacketType.RequestShootUpdate],
                    EntityId = comp.MyCube.EntityId,
                    SenderId = comp.Session.MultiplayerId,
                    PType = PacketType.RequestShootUpdate,
                    Action = action,
                    PlayerId = PlayerId,
                });
            }
            else Log.Line($"SendActionShootUpdate no player MIds found");
        }

        internal void SendActiveTerminal(WeaponComponent comp)
        {
            uint[] mIds;
            if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
            {
                PacketsToServer.Add(new TerminalMonitorPacket
                {
                    SenderId = MultiplayerId,
                    PType = PacketType.TerminalMonitor,
                    EntityId = comp.MyCube.EntityId,
                    State = TerminalMonitorPacket.Change.Update,
                    MId = ++mIds[(int)PacketType.TerminalMonitor],
                });
            }
            else Log.Line($"SendActiveTerminal no player MIds found");
        }

        internal void SendTargetChange(WeaponComponent comp, int weaponId)
        {
            var w = comp.Platform.Weapons[weaponId];
            ++comp.Data.Repo.Targets[w.WeaponId].Revision;
            PacketsToClient.Add(new PacketInfo
            {
                Entity = comp.MyCube,
                Packet = new TargetPacket
                {
                    MId = ++w.MIds[(int)PacketType.TargetChange],
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.TargetChange,
                    Target = comp.Data.Repo.Targets[weaponId],
                }
            });
        }

        internal void SendFakeTargetUpdate(GridAi ai, Vector3 hitPos)
        {
            if (IsClient)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))  {
                    PacketsToServer.Add(new FakeTargetPacket
                    {
                        MId = ++mIds[(int)PacketType.FakeTargetUpdate],
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = ai.Session.MultiplayerId,
                        PType = PacketType.FakeTargetUpdate,
                        Data = hitPos,
                    });
                }
                else Log.Line($"SendFakeTargetUpdate no player MIds found");
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FakeTargetPacket
                    {
                        MId = ++ai.MIds[(int)PacketType.FakeTargetUpdate],
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = ai.Session.MultiplayerId,
                        PType = PacketType.FakeTargetUpdate,
                        Data = hitPos,
                    }
                });
            }
        }

        internal void SendSingleShot(WeaponComponent comp)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new Packet
                {
                    EntityId = comp.MyCube.EntityId,
                    PType = PacketType.SendSingleShot,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Packet = new Packet
                    {
                        EntityId = comp.MyCube.EntityId,
                        PType = PacketType.SendSingleShot,
                    }
                });
            }
            Log.Line($"SendSingleShot");
        }

        internal void SendUpdateRequest(long entityId, PacketType ptype)
        {
            uint[] mIds;
            if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
            {
                PacketsToServer.Add(new Packet
                {
                    MId = ++mIds[(int)ptype],
                    EntityId = entityId,
                    SenderId = MultiplayerId,
                    PType = ptype
                });
            }
            else Log.Line($"SendUpdateRequest no player MIds found");
        }

        internal void SendPlayerControlRequest(WeaponComponent comp, long playerId, CompStateValues.ControlMode mode)
        {
            if (IsClient)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new PlayerControlRequestPacket
                    {
                        MId = ++mIds[(int)PacketType.PlayerControlRequest],
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = PacketType.PlayerControlRequest,
                        PlayerId = playerId,
                        Mode = mode,
                    });
                }
                else Log.Line($"SendPlayerControlRequest no player MIds found");
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new PlayerControlRequestPacket
                    {
                        MId = ++comp.MIds[(int)PacketType.PlayerControlRequest],
                        EntityId = comp.MyCube.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.PlayerControlRequest,
                        PlayerId = playerId,
                        Mode = mode,
                    }
                });
            }
            else Log.Line($"SendPlayerControlRequest should never be called on Server");
        }


        internal void SendAmmoCycleRequest(WeaponComponent comp, int weaponId, int newAmmoId)
        {
            if (IsClient)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new AmmoCycleRequestPacket
                    {
                        MId = ++mIds[(int)PacketType.AmmoCycleRequest],
                        EntityId = comp.MyCube.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.AmmoCycleRequest,
                        WeaponId = weaponId,
                        NewAmmoId = newAmmoId,
                    });
                }
                else Log.Line($"SendAmmoCycleRequest no player MIds found");
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new AmmoCycleRequestPacket
                    {
                        MId = ++comp.MIds[(int)PacketType.AmmoCycleRequest],
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = PacketType.AmmoCycleRequest,
                        WeaponId = weaponId,
                        NewAmmoId = newAmmoId,
                    }
                });
            }
            else Log.Line($"SendAmmoCycleRequest should never be called on Non-HandlesInput");
        }

        internal void SendTrackReticleUpdate(WeaponComponent comp, bool track)
        {
            if (IsClient) {

                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new BoolUpdatePacket
                    {
                        MId = ++mIds[(int)PacketType.ReticleUpdate],
                        EntityId = comp.MyCube.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ReticleUpdate,
                        Data = track
                    });
                }
                else Log.Line($"SendTrackReticleUpdate no player MIds found");
            }
            else {
                comp.Data.Repo.State.TrackingReticle = track;
                SendCompData(comp);
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
                    MId = ++comp.MIds[(int)PacketType.TargetExpireUpdate],
                    EntityId = comp.MyCube.EntityId,
                    SenderId = 0,
                    PType = PacketType.TargetExpireUpdate,
                    WeaponId = weaponId,
                }
            });
        }

        internal void SendOverRidesClientComp(WeaponComponent comp, string groupName, string settings, int value)
        {
            if (IsClient)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new OverRidesPacket
                    {
                        MId = ++mIds[(int)PacketType.OverRidesUpdate],
                        PType = PacketType.OverRidesUpdate,
                        EntityId = comp.MyCube.EntityId,
                        SenderId = MultiplayerId,
                        GroupName = groupName,
                        Setting = settings,
                        Value = value,
                    });
                }
                else Log.Line($"SendOverRidesClientComp no player MIds found");
            }
        }


        internal void SendOverRidesClientAi(GridAi ai, string groupName, string settings, int value)
        {
            if (IsClient)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new OverRidesPacket
                    {
                        MId = ++mIds[(int)PacketType.OverRidesUpdate],
                        PType = PacketType.OverRidesUpdate,
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        GroupName = groupName,
                        Setting = settings,
                        Value = value,
                    });
                }
                else Log.Line($"SendOverRidesClientAi no player MIds found");
            }
        }


        internal void SendConstructGroups(GridAi ai)
        {
            PacketsToClient.Add(new PacketInfo
            {
                Entity = ai.MyGrid,
                Packet = new ConstructGroupsPacket
                {
                    MId = ++ai.MIds[(int)PacketType.ConstructGroups],
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = 0,
                    PType = PacketType.ConstructGroups,
                    Data = ai.Construct.Data.Repo,
                }
            });
        }

        internal void SendCompState(WeaponComponent comp, PacketType type)
        {
            if (IsServer)
            {
                ++comp.Data.Repo.State.Revision;
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new CompStatePacket
                    {
                        MId = ++comp.MIds[(int)PacketType.CompState],
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = type,
                        Data = comp.Data.Repo.State
                    }
                });
            }
            else Log.Line($"SendCompState should never be called on Client");
        }

        internal void SendAiData(GridAi ai)
        {
            if (IsServer)
            {
                ++ai.Data.Repo.Revision;
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new AiDataPacket
                    {
                        MId = ++ai.MIds[(int)PacketType.AiData],
                        SenderId = 0,
                        EntityId = ai.MyGrid.EntityId,
                        PType = PacketType.AiData,
                        Data = ai.Data.Repo,
                    }
                });
            }
        }

        internal void SendCompData(WeaponComponent comp)
        {
            if (IsServer)
            {
                ++comp.Data.Repo.Revision;
                ++comp.Data.Repo.State.Revision;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    ++comp.Data.Repo.Targets[i].Revision;

                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new CompDataPacket
                    {
                        MId = ++comp.MIds[(int)PacketType.CompData],
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = PacketType.CompData,
                        Data = comp.Data.Repo
                    }
                });
            }
            else Log.Line($"SendCompData should never be called on Client");
        }

        internal void SendGroupUpdate(GridAi ai)
        {
            if (IsClient)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new Packet
                    {
                        MId = ++mIds[(int)PacketType.RescanGroupRequest],
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.RescanGroupRequest,
                    });
                }
                else Log.Line($"SendGroupUpdate no player MIds found");
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

        #endregion
        #region AIFocus packets
        internal void SendFocusTargetUpdate(GridAi ai, long targetId)
        {
            if (IsClient)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new FocusPacket
                    {
                        MId = ++mIds[(int)PacketType.FocusUpdate],
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.FocusUpdate,
                        TargetId = targetId
                    });
                }
                else Log.Line($"SendFocusTargetUpdate no player MIds found");

            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        MId = ++ai.MIds[(int)PacketType.FocusUpdate],
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
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new FocusPacket
                    {
                        MId = ++mIds[(int)PacketType.ReassignTargetUpdate],
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ReassignTargetUpdate,
                        TargetId = targetId,
                        FocusId = focusId
                    });
                }
                else Log.Line($"SendReassignTargetUpdate no player MIds found");


            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        MId = ++ai.MIds[(int)PacketType.ReassignTargetUpdate],
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
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new FocusPacket
                    {
                        MId = ++mIds[(int)PacketType.NextActiveUpdate],
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.NextActiveUpdate,
                        AddSecondary = addSecondary
                    });
                }
                else Log.Line($"SendNextActiveUpdate no player MIds found");

            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        MId = ++ai.MIds[(int)PacketType.NextActiveUpdate],
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
                uint[] mIds;
                if (PlayerMIds.TryGetValue(MultiplayerId, out mIds))
                {
                    PacketsToServer.Add(new FocusPacket
                    {
                        MId = ++mIds[(int)PacketType.ReleaseActiveUpdate],
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ReleaseActiveUpdate
                    });
                }
                else Log.Line($"SendReleaseActiveUpdate no player MIds found");
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        MId = ++ai.MIds[(int)PacketType.ReleaseActiveUpdate],
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ReleaseActiveUpdate
                    }
                });
            }
        }
        #endregion


        #region Misc Network Methods

        internal static void CreateFixedWeaponProjectile(Weapon weapon, MyEntity targetEntity, Vector3 origin, Vector3 direction, Vector3 velocity, Vector3 originUp, int muzzleId, AmmoDef ammoDef, float maxTrajectory)
        {
            weapon.Comp.Session.Projectiles.NewProjectiles.Add(new NewProjectile { AmmoDef = ammoDef, Muzzle = weapon.Muzzles[muzzleId], Weapon = weapon, TargetEnt = targetEntity, Origin = origin, OriginUp = originUp, Direction = direction, Velocity = velocity, MaxTrajectory = maxTrajectory, Type = NewProjectile.Kind.Client });
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
