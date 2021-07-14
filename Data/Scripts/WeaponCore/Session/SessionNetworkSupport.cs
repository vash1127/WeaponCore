﻿using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent;

namespace WeaponCore
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
            var fakeTargetUpdateError = data.Packet.PType == PacketType.AimTargetUpdate; // why does this happen so often
           
            if (fakeTargetUpdateError) {
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
        internal void SendConstruct(GridAi ai)
        {
            if (IsServer) {

                PrunedPacketsToClient.Remove(ai.Construct.Data.Repo.FocusData);
                ++ai.Construct.Data.Repo.FocusData.Revision;

                PacketInfo oldInfo;
                ConstructPacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(ai.Construct.Data.Repo, out oldInfo)) {
                    iPacket = (ConstructPacket)oldInfo.Packet;
                    iPacket.EntityId = ai.MyGrid.EntityId;
                    iPacket.Data = ai.Construct.Data.Repo;
                }
                else {
                    iPacket = PacketConstructPool.Get();
                    iPacket.EntityId = ai.MyGrid.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = PacketType.Construct;
                    iPacket.Data = ai.Construct.Data.Repo;
                }

                PrunedPacketsToClient[ai.Construct.Data.Repo] = new PacketInfo {
                    Entity = ai.MyGrid,
                    Packet = iPacket,
                };
            }
            else Log.Line($"SendConstruct should never be called on Client");
        }

        internal void SendConstructFoci(GridAi ai)
        {
            if (IsServer) {

                ++ai.Construct.Data.Repo.FocusData.Revision;

                if (!PrunedPacketsToClient.ContainsKey(ai.Construct.Data.Repo)) {
                    PacketInfo oldInfo;
                    ConstructFociPacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(ai.Construct.Data.Repo.FocusData, out oldInfo)) {
                        iPacket = (ConstructFociPacket)oldInfo.Packet;
                        iPacket.EntityId = ai.MyGrid.EntityId;
                        iPacket.Data = ai.Construct.Data.Repo.FocusData;
                    }
                    else {
                        iPacket = PacketConstructFociPool.Get();
                        iPacket.EntityId = ai.MyGrid.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = PacketType.ConstructFoci;
                        iPacket.Data = ai.Construct.Data.Repo.FocusData;
                    }

                    PrunedPacketsToClient[ai.Construct.Data.Repo.FocusData] = new PacketInfo {
                        Entity = ai.MyGrid,
                        Packet = iPacket,
                    };
                }
                else SendConstruct(ai);

            }
            else Log.Line($"SendConstructGroups should never be called on Client");
        }

        internal void SendAiData(GridAi ai)
        {
            if (IsServer) {

                PacketInfo oldInfo;
                AiDataPacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(ai.Data.Repo, out oldInfo)) {
                    iPacket = (AiDataPacket)oldInfo.Packet;
                    iPacket.EntityId = ai.MyGrid.EntityId;
                    iPacket.Data = ai.Data.Repo;
                }
                else {

                    iPacket = PacketAiPool.Get();
                    iPacket.EntityId = ai.MyGrid.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = PacketType.AiData;
                    iPacket.Data = ai.Data.Repo;
                }

                ++ai.Data.Repo.Revision;

                PrunedPacketsToClient[ai.Data.Repo] = new PacketInfo {
                    Entity = ai.MyGrid,
                    Packet = iPacket,
                };
            }
            else Log.Line($"SendAiData should never be called on Client");
        }

        internal void SendWeaponAmmoData(Weapon w)
        {
            if (IsServer) {

                const PacketType type = PacketType.WeaponAmmo;
                ++w.Ammo.Revision;

                PacketInfo oldInfo;
                WeaponAmmoPacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(w.Ammo, out oldInfo)) {
                    iPacket = (WeaponAmmoPacket)oldInfo.Packet;
                    iPacket.EntityId = w.Comp.MyCube.EntityId;
                    iPacket.Data = w.Ammo;
                }
                else {

                    iPacket = PacketAmmoPool.Get();
                    iPacket.EntityId = w.Comp.MyCube.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = type;
                    iPacket.Data = w.Ammo;
                    iPacket.WeaponId = w.WeaponId;
                }


                PrunedPacketsToClient[w.Ammo] = new PacketInfo {
                    Entity = w.Comp.MyCube,
                    Packet = iPacket,
                };
            }
            else Log.Line($"SendWeaponAmmoData should never be called on Client");
        }

        internal void SendCompBaseData(WeaponComponent comp)
        {
            if (IsServer) {

                const PacketType type = PacketType.CompBase;
                comp.Data.Repo.Base.UpdateCompBasePacketInfo(comp, true);

                PacketInfo oldInfo;
                CompBasePacket iPacket;
                if (PrunedPacketsToClient.TryGetValue(comp.Data.Repo.Base, out oldInfo)) {
                    iPacket = (CompBasePacket)oldInfo.Packet;
                    iPacket.EntityId = comp.MyCube.EntityId;
                    iPacket.Data = comp.Data.Repo.Base;
                }
                else {

                    iPacket = PacketCompBasePool.Get();
                    iPacket.EntityId = comp.MyCube.EntityId;
                    iPacket.SenderId = MultiplayerId;
                    iPacket.PType = type;
                    iPacket.Data = comp.Data.Repo.Base;
                }

                PrunedPacketsToClient[comp.Data.Repo.Base] = new PacketInfo {
                    Entity = comp.MyCube,
                    Packet = iPacket,
                };
            }
            else Log.Line($"SendCompData should never be called on Client");
        }

        internal void SendTargetChange(WeaponComponent comp, int weaponId)
        {
            if (IsServer) {

                if (!comp.Session.PrunedPacketsToClient.ContainsKey(comp.Data.Repo.Base)) {

                    const PacketType type = PacketType.TargetChange;
                    comp.Data.Repo.Base.UpdateCompBasePacketInfo(comp);

                    var w = comp.Platform.Weapons[weaponId];
                    PacketInfo oldInfo;
                    TargetPacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(w.TargetData, out oldInfo)) {
                        iPacket = (TargetPacket)oldInfo.Packet;
                        iPacket.EntityId = comp.MyCube.EntityId;
                        iPacket.Target = w.TargetData;
                    }
                    else {
                        iPacket = PacketTargetPool.Get();
                        iPacket.EntityId = comp.MyCube.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = type;
                        iPacket.Target = w.TargetData;
                    }


                    PrunedPacketsToClient[w.TargetData] = new PacketInfo {
                        Entity = comp.MyCube,
                        Packet = iPacket,
                    };
                }
                else
                    SendCompBaseData(comp);
            }
            else Log.Line($"SendTargetChange should never be called on Client");
        }

        internal void SendCompState(WeaponComponent comp)
        {
            if (IsServer) {

                if (!comp.Session.PrunedPacketsToClient.ContainsKey(comp.Data.Repo.Base)) {

                    const PacketType type = PacketType.CompState;
                    comp.Data.Repo.Base.UpdateCompBasePacketInfo(comp);

                    PacketInfo oldInfo;
                    CompStatePacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(comp.Data.Repo.Base.State, out oldInfo)) {
                        iPacket = (CompStatePacket)oldInfo.Packet;
                        iPacket.EntityId = comp.MyCube.EntityId;
                        iPacket.Data = comp.Data.Repo.Base.State;
                    }
                    else {
                        iPacket = PacketStatePool.Get();
                        iPacket.EntityId = comp.MyCube.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = type;
                        iPacket.Data = comp.Data.Repo.Base.State;
                    }

                    PrunedPacketsToClient[comp.Data.Repo.Base.State] = new PacketInfo {
                        Entity = comp.MyCube,
                        Packet = iPacket,
                    };
                }
                else
                    SendCompBaseData(comp);

            }
            else Log.Line($"SendCompState should never be called on Client");
        }

        internal void SendWeaponReload(Weapon w)
        {
            if (IsServer) {

                if (!PrunedPacketsToClient.ContainsKey(w.Comp.Data.Repo.Base)) {

                    const PacketType type = PacketType.WeaponReload;
                    w.Comp.Data.Repo.Base.UpdateCompBasePacketInfo(w.Comp);

                    PacketInfo oldInfo;
                    WeaponReloadPacket iPacket;
                    if (PrunedPacketsToClient.TryGetValue(w.Reload, out oldInfo)) {
                        iPacket = (WeaponReloadPacket)oldInfo.Packet;
                        iPacket.EntityId = w.Comp.MyCube.EntityId;
                        iPacket.Data = w.Reload;
                    }
                    else {
                        iPacket = PacketReloadPool.Get();
                        iPacket.EntityId = w.Comp.MyCube.EntityId;
                        iPacket.SenderId = MultiplayerId;
                        iPacket.PType = type;
                        iPacket.Data = w.Reload;
                        iPacket.WeaponId = w.WeaponId;
                    }

                    PrunedPacketsToClient[w.Reload] = new PacketInfo {
                        Entity = w.Comp.MyCube,
                        Packet = iPacket,
                    };
                }
                else 
                    SendCompBaseData(w.Comp);
            }
            else Log.Line($"SendWeaponReload should never be called on Client");
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
            else Log.Line($"SendUpdateRequest should only be called on clients");
        }

        internal void SendOverRidesClientComp(WeaponComponent comp, string settings, int value)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new OverRidesPacket
                {
                    PType = PacketType.OverRidesUpdate,
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    Setting = settings,
                    Value = value,
                });
            }
            else Log.Line($"SendOverRidesClientComp should only be called on clients");
        }

        internal void SendFixedGunHitEvent(MyCubeBlock firingCube, MyEntity hitEnt, Vector3 origin, Vector3 velocity, Vector3 up, int muzzleId, int systemId, int ammoIndex, float maxTrajectory)
        {
            if (firingCube == null) return;

            var comp = firingCube.Components.Get<WeaponComponent>();
            int weaponId;
            if (comp.Ai?.MyGrid != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.Platform.Structure.HashToId.TryGetValue(systemId, out weaponId))
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

        internal void SendFocusLockUpdate(GridAi ai)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.FocusLockUpdate,
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
                        PType = PacketType.FocusLockUpdate,
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


        internal void SendMouseUpdate(GridAi ai, MyEntity entity)
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
            else Log.Line($"SendMouseUpdate should never be called on Dedicated");
        }

        internal void SendActiveControlUpdate(GridAi ai, MyCubeBlock controlBlock, bool active)
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
            else if (HandlesInput)
            {
                ai.Construct.UpdateConstructsPlayers(controlBlock, PlayerId, active);
            }
            else Log.Line($"SendActiveControlUpdate should never be called on Dedicated");
        }

        internal void SendActionShootUpdate(WeaponComponent comp, ShootActions action)
        {
            if (IsClient)
            {
                comp.Session.PacketsToServer.Add(new ShootStatePacket
                {
                    EntityId = comp.MyCube.EntityId,
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
                    Entity = comp.MyCube,
                    Packet = new ShootStatePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = comp.Session.MultiplayerId,
                        PType = PacketType.RequestShootUpdate,
                        Action = action,
                        PlayerId = PlayerId,
                    }
                });
            }
            else Log.Line($"SendActionShootUpdate should never be called on Dedicated");
        }

        internal void SendActiveTerminal(WeaponComponent comp)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new TerminalMonitorPacket
                {
                    SenderId = MultiplayerId,
                    PType = PacketType.TerminalMonitor,
                    EntityId = comp.MyCube.EntityId,
                    State = TerminalMonitorPacket.Change.Update,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new TerminalMonitorPacket
                    {
                        SenderId = MultiplayerId,
                        PType = PacketType.TerminalMonitor,
                        EntityId = comp.MyCube.EntityId,
                        State = TerminalMonitorPacket.Change.Update,
                    }
                });
            }
            else Log.Line($"SendActiveTerminal should never be called on Dedicated");
        }

        internal void SendAimTargetUpdate(GridAi ai, GridAi.FakeTarget fake)
        {
            if (IsClient)
            {

                PacketsToServer.Add(new FakeTargetPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = ai.Session.MultiplayerId,
                    PType = PacketType.AimTargetUpdate,
                    Pos = fake.EntityId != 0  ? fake.LocalPosition : fake.FakeInfo.WorldPosition,
                    TargetId = fake.EntityId,
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
                        PType = PacketType.AimTargetUpdate,
                        Pos = fake.EntityId != 0 ? fake.LocalPosition : fake.FakeInfo.WorldPosition,
                        TargetId = fake.EntityId,
                    }
                });
            }
            else Log.Line($"SendFakeTargetUpdate should never be called on Dedicated");
        }

        internal void SendPaintedTargetUpdate(GridAi ai, GridAi.FakeTarget fake)
        {
            if (IsClient)
            {

                PacketsToServer.Add(new FakeTargetPacket
                {
                    EntityId = ai.MyGrid.EntityId,
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
                    Entity = ai.MyGrid,
                    Packet = new FakeTargetPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = ai.Session.MultiplayerId,
                        PType = PacketType.PaintedTargetUpdate,
                        Pos = fake.EntityId != 0 ? fake.LocalPosition : fake.FakeInfo.WorldPosition,
                        TargetId = fake.EntityId,
                    }
                });
            }
            else Log.Line($"SendFakeTargetUpdate should never be called on Dedicated");
        }


        internal void SendPlayerControlRequest(WeaponComponent comp, long playerId, CompStateValues.ControlMode mode)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new PlayerControlRequestPacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.PlayerControlRequest,
                    PlayerId = playerId,
                    Mode = mode,
                });
            }
            else if (HandlesInput)
            {
                SendCompBaseData(comp);
            }
            else Log.Line($"SendPlayerControlRequest should never be called on Server");
        }

        internal void SendQueuedShot(Weapon w)
        {
            if (IsServer)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = w.Comp.MyCube,
                    Packet = new QueuedShotPacket
                    {
                        EntityId = w.Comp.MyCube.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.QueueShot,
                        WeaponId = w.WeaponId,
                        PlayerId = w.Comp.Data.Repo.Base.State.PlayerId,
                    }
                });
            }
            else Log.Line($"SendAmmoCycleRequest should never be called on Client");
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

                PacketsToClient.Add(new PacketInfo {Packet = _cachedEwarPacket }); 
            }
            else Log.Line($"SendEwaredBlocks should never be called on Client");
        }

        internal void SendAmmoCycleRequest(Weapon w, int newAmmoId)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new AmmoCycleRequestPacket
                {
                    EntityId = w.Comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.AmmoCycleRequest,
                    WeaponId = w.WeaponId,
                    NewAmmoId = newAmmoId,
                    PlayerId = PlayerId,
                });
            }
            else Log.Line($"SendAmmoCycleRequest should never be called on Non-Client");
        }

        internal void SendSetCompFloatRequest(WeaponComponent comp, float newDps, PacketType type)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FloatUpdatePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = type,
                    Data = newDps,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new FloatUpdatePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = MultiplayerId,
                        PType = type,
                        Data = newDps,
                    }
                });
            }
            else Log.Line($"SendSetFloatRequest should never be called on Non-HandlesInput");
        }

        internal void SendSetCompBoolRequest(WeaponComponent comp, bool newBool, PacketType type)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = type,
                    Data = newBool,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new BoolUpdatePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = MultiplayerId,
                        PType = type,
                        Data = newBool,
                    }
                });
            }
            else Log.Line($"SendSetCompBoolRequest should never be called on Non-HandlesInput");
        }

        internal void SendTrackReticleUpdate(WeaponComponent comp, bool track)
        {
            if (IsClient) {

                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReticleUpdate,
                    Data = track
                });
            }
            else if (HandlesInput) {
                comp.Data.Repo.Base.State.TrackingReticle = track;
                SendCompBaseData(comp);
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
