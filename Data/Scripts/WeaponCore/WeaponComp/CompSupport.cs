using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Session;
using static WeaponCore.Support.GridAi;
namespace WeaponCore.Support
{
    public partial class WeaponComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            if (Ai?.LastTerminal == MyCube)
            {
                TerminalBlock.RefreshCustomInfo();
            }

            if (update && InControlPanel)
            {
                MyCube.UpdateTerminal();
            }
        }

        internal void SaveAndSendAll()
        {
            _firstSync = true;
            if (_isServer || _isDedicated)
            {
                Set.SaveSettings();
                Set.NetworkUpdate();
                State.SaveState();
                State.NetworkUpdate();
            }
            else
            {
                Set.NetworkUpdate();
                State.NetworkUpdate();
            }
        }

        internal void UpdateStateMP()
        {
            if(Session.MpActive)
                State.NetworkUpdate();
        }

        internal void UpdateSettingsMP()
        {
            if (Session.MpActive)
                Set.NetworkUpdate();
        }

        internal void SendOverRides()
        {
            if (Session.MpActive)
            {
                Set.Value.MId++;
                if (Session.IsClient)
                {
                    Session.PacketsToServer.Add(new OverRidesPacket
                    {
                        EntityId = MyCube.EntityId,
                        SenderId = Session.MultiplayerId,
                        MId = Set.Value.MId,
                        PType = PacketType.OverRidesUpdate,
                        Data = Set.Value.Overrides,
                    });
                }
                else
                {
                    Session.PacketsToClient.Add(new PacketInfo {
                        Entity = MyCube,
                        Packet = new OverRidesPacket
                        {
                            EntityId = MyCube.EntityId,
                            SenderId = 0,
                            MId = Set.Value.MId,
                            PType = PacketType.OverRidesUpdate,
                            Data = Set.Value.Overrides,
                        }
                    });
                }
            }
        }

        internal void SendControlingPlayer()
        {
            if (Session.MpActive)
            {
                Set.Value.MId++;
                if (Session.IsClient)
                {
                    Session.PacketsToServer.Add(new ControllingPlayerPacket
                    {
                        EntityId = MyCube.EntityId,
                        SenderId = Session.MultiplayerId,
                        MId = Set.Value.MId,
                        PType = PacketType.PlayerControlUpdate,
                        Data = State.Value.CurrentPlayerControl,
                    });
                }
                else
                {
                    Session.PacketsToClient.Add(new PacketInfo
                    {
                        Entity = MyCube,
                        Packet = new ControllingPlayerPacket
                        {
                            EntityId = MyCube.EntityId,
                            SenderId = 0,
                            MId = Set.Value.MId,
                            PType = PacketType.PlayerControlUpdate,
                            Data = State.Value.CurrentPlayerControl,
                        }
                    });
                }
            }
        }

        internal void RemoveComp()
        {
            try
            {
                RegisterEvents(false);
                if (Ai != null)
                {
                    Ai.CompChange(false, this);
                    Ai.OptimalDps -= OptimalDps;
                    WeaponComponent comp;
                    if (Ai.WeaponBase.TryRemove(MyCube, out comp))
                    {
                        if (Platform.State == MyWeaponPlatform.PlatformState.Ready)
                        {
                            WeaponCount wCount;
                            if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount))
                            {
                                wCount.Current--;
                                WeaponCount cntRemoved;
                                if (wCount.Current == 0) Ai.WeaponCounter.TryRemove(MyCube.BlockDefinition.Id.SubtypeId, out cntRemoved);
                            }

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.StopShooting();
                                w.WeaponCache.HitEntity.Clean();
                                if (w.DrawingPower)
                                    w.StopPowerDraw();
                            }
                        }
                    }
                    else
                        Log.Line($"RemoveComp Weaponbase didn't have my comp");

                    if (Ai.WeaponBase.Count == 0)
                    {
                        WeaponCount wCount;
                        if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount))
                            Session.WeaponCountPool.Return(wCount);

                        GridAi gridAi;
                        Session.GridTargetingAIs.TryRemove(Ai.MyGrid, out gridAi);
                    }
                    Ai = null;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in RemoveComp: {ex} - AiNull:{Ai == null} - SessionNull:{Session == null}"); }
        }

        public void StopAllSounds()
        {
            foreach (var w in Platform.Weapons)
            {
                w.StopReloadSound();
                w.StopRotateSound();
                w.StopShooting(true);
            }
        }

        public void StopAllGraphics()
        {
            foreach (var w in Platform.Weapons)
                w.StopBarrelAv = true;

            Session.Av.RunAvBarrels1();
            Session.Av.RunAvBarrels2();

        }

        public void StopAllAv()
        {
            if (Platform?.State != MyWeaponPlatform.PlatformState.Ready) return;
            StopAllSounds();
            StopAllGraphics();
        }
    }
}
