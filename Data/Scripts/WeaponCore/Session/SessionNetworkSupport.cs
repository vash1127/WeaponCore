using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
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
                PacketsToServer.Add(new InputPacket
                {
                    MId = ++ai.MIds[(int)PacketType.ClientMouseEvent],
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
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    MId = ++ai.MIds[(int)PacketType.ActiveControlUpdate],
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
                        MId = ++ai.MIds[(int)PacketType.ActiveControlUpdate],
                        EntityId = controlBlock.EntityId,
                        SenderId = 0,
                        PType = PacketType.ActiveControlUpdate,
                        Data = active
                    }
                });
            }
        }

        internal void SendOverRidesClientComp(WeaponComponent comp, string groupName, string settings, int value)
        {
            PacketsToServer.Add(new OverRidesPacket
            {
                MId = ++comp.MIds[(int)PacketType.OverRidesUpdate],
                PType = PacketType.OverRidesUpdate,
                EntityId = comp.MyCube.EntityId,
                SenderId = MultiplayerId,
                GroupName = groupName,
                Setting = settings,
                Value = value,
            });
        }



        internal void SendOverRidesClientAi(GridAi ai, string groupName, string settings, int value)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new OverRidesPacket
                {
                    MId = ++ai.MIds[(int)PacketType.OverRidesUpdate],
                    PType = PacketType.OverRidesUpdate,
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    GroupName = groupName,
                    Setting = settings,
                    Value = value,
                });
            }
        }

        internal void SendActionShootUpdate(WeaponComponent comp, ShootActions action)
        {
            comp.Session.PacketsToServer.Add(new ShootStatePacket
            {
                MId = ++comp.MIds[(int)PacketType.RequestShootUpdate],
                EntityId = comp.MyCube.EntityId,
                SenderId = comp.Session.MultiplayerId,
                PType = PacketType.RequestShootUpdate,
                Action = action,
                PlayerId = PlayerId,
            });
        }

        internal void SendActiveTerminal(WeaponComponent comp)
        {
            PacketsToServer.Add(new TerminalMonitorPacket
            {
                SenderId = MultiplayerId,
                PType = PacketType.TerminalMonitor,
                EntityId = comp.MyCube.EntityId,
                State = TerminalMonitorPacket.Change.Update,
                MId = ++comp.MIds[(int)PacketType.TerminalMonitor],
            });
        }

        internal void SendFakeTargetUpdate(GridAi ai, Vector3 hitPos)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FakeTargetPacket
                {
                    MId = ++ai.MIds[(int)PacketType.FakeTargetUpdate],
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
                PacketsToServer.Add(new PlayerControlRequestPacket
                {
                    MId = ++comp.MIds[(int)PacketType.PlayerControlRequest],
                    EntityId = comp.MyCube.EntityId,
                    SenderId = 0,
                    PType = PacketType.PlayerControlRequest,
                    PlayerId = playerId,
                    Mode = mode,
                });
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
            else Log.Line($"SendAmmoCycleRequest should never be called on Server");
        }


        internal void SendAmmoCycleRequest(WeaponComponent comp, int weaponId, int newAmmoId)
        {
            if (IsServer)
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
            else Log.Line($"SendAmmoCycleRequest should never be called on Client");
        }

        internal void SendTrackReticleUpdate(WeaponComponent comp)
        {
            if (IsClient) {

                PacketsToServer.Add(new BoolUpdatePacket {
                    MId = ++comp.MIds[(int)PacketType.ReticleUpdate],
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReticleUpdate,
                    Data = comp.TrackReticle
                });
            }
            else {
                comp.Data.Repo.State.OtherPlayerTrackingReticle = comp.TrackReticle;
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

        internal void SendAiSync(GridAi ai)
        {
            if (IsServer)
            {
                ++ai.Data.Repo.Revision;
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new AiSyncPacket
                    {
                        MId = ++ai.MIds[(int)PacketType.AiSyncUpdate],
                        SenderId = 0,
                        EntityId = ai.MyGrid.EntityId,
                        PType = PacketType.AiSyncUpdate,
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
                PacketsToServer.Add(new Packet
                {
                    MId = ++ai.MIds[(int)PacketType.RescanGroupRequest],
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.RescanGroupRequest,
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
                    MId = ++comp.MIds[(int)PacketType.FixedWeaponHitEvent],
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

        /*
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
        */
        /*
        internal void RequestCompSync(WeaponComponent comp)
        {
            PacketsToServer.Add(new Packet
            {
                EntityId = comp.MyCube.EntityId,
                SenderId = MultiplayerId,
                PType = PacketType.CompSyncRequest,
            });
        }
        */

        #endregion
        #region AIFocus packets
        internal void SendFocusTargetUpdate(GridAi ai, long targetId)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    MId = ++ai.MIds[(int)PacketType.FocusUpdate],
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
                PacketsToServer.Add(new FocusPacket
                {
                    MId = ++ai.MIds[(int)PacketType.ReassignTargetUpdate],
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
                PacketsToServer.Add(new FocusPacket
                {
                    MId = ++ai.MIds[(int)PacketType.NextActiveUpdate],
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
                PacketsToServer.Add(new FocusPacket
                {
                    MId = ++ai.MIds[(int)PacketType.ReleaseActiveUpdate],
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
        internal void SyncWeapon(Weapon weapon, ref WeaponStateValues weaponData, bool setState = true)
        {
            if (weapon.System.DesignatorWeapon) return;

            if (setState)
                weaponData.Sync(weapon.State, weapon);

            if (weapon.ActiveAmmoDef.AmmoDef.Const.Reloadable && !weapon.Reloading)
                weapon.Reload();
        }

        public void UpdateActiveControlDictionary(MyCubeBlock cube, long playerId, bool updateAdd, bool applyToRoot = false)
        {
            GridAi trackingAi;
            if (updateAdd) //update/add
            {

                if (applyToRoot && GridToMasterAi.TryGetValue(cube.CubeGrid, out trackingAi) || GridTargetingAIs.TryGetValue(cube.CubeGrid, out trackingAi)) {
                    trackingAi.Data.Repo.ControllingPlayers[playerId] = cube.EntityId;
                    trackingAi.AiSleep = false;
                }
            }
            else //remove
            {
                if (applyToRoot && GridToMasterAi.TryGetValue(cube.CubeGrid, out trackingAi) || GridTargetingAIs.TryGetValue(cube.CubeGrid, out trackingAi)) {
                    trackingAi.Data.Repo.ControllingPlayers.Remove(playerId);
                    trackingAi.AiSleep = false;
                }
            }
            if (MpActive && trackingAi != null)
                SendAiSync(trackingAi);
        }
        /*
        internal static bool SyncGridOverrides(GridAi ai, Packet packet, GroupOverrides o, string groupName)
        {
            if (ai.MIds[(int) packet.PType] < packet.MId) {
                ai.MIds[(int) packet.PType] = packet.MId;

                ai.ReScanBlockGroups();

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
                return true;
            }

            return false;
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
        */

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
