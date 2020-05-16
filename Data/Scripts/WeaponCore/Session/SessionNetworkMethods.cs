using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Control;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Session;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition;

namespace WeaponCore
{
    public partial class Session
    {

        private void SendCompStateUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
                var statePacket = packet as StatePacket;
                var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                var comp = ent?.Components.Get<WeaponComponent>();
                if (statePacket?.Data == null || comp == null)
                {
                    data.ErrorPacket.Error = $"Data was null: {statePacket?.Data == null} Comp was null: {comp == null}";
                    break;
                }

                comp.MIds[(int)packet.PType] = statePacket.MId;
                comp.State.Value.Sync(statePacket.Data);
                data.Report.PacketValid = true;
            }
        }

        private void CompSettingsUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
                var setPacket = packet as SettingPacket;
                var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                var comp = ent?.Components.Get<WeaponComponent>();
                if (setPacket?.Data == null || comp == null)
                {
                    data.ErrorPacket.Error = $"Data was null: {setPacket?.Data == null} Comp was null: {comp == null}";
                    break;
                }

                comp.MIds[(int)packet.PType] = setPacket.MId;
                comp.Set.Value.Sync(comp, setPacket.Data);

                data.Report.PacketValid = true;
            }
        }
        private void WeaponSyncUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
                var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                var targetPacket = packet as GridWeaponPacket;
                if (targetPacket?.Data == null || ent == null)
                {
                    data.ErrorPacket.Error = $"Data was null: {targetPacket?.Data == null} Grid was null: {ent == null}";

                    break;
                }

                for (int j = 0; j < targetPacket.Data.Count; j++)
                {
                    var weaponData = targetPacket.Data[j];
                    var block = MyEntities.GetEntityByIdOrDefault(weaponData.CompEntityId) as MyCubeBlock;
                    var comp = block?.Components.Get<WeaponComponent>();

                    if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        continue;

                    Weapon weapon;

                    if (weaponData.Timmings != null && weaponData.SyncData != null && weaponData.WeaponRng != null)
                    {
                        weapon = comp.Platform.Weapons[weaponData.SyncData.WeaponId];
                        var timings = weaponData.Timmings.SyncOffsetClient(Tick);
                        SyncWeapon(weapon, timings, ref weaponData.SyncData);

                        weapon.Comp.WeaponValues.WeaponRandom[weapon.WeaponId].Sync(weaponData.WeaponRng);
                    }

                    if (weaponData.TargetData != null)
                    {
                        weapon = comp.Platform.Weapons[weaponData.TargetData.WeaponId];
                        weaponData.TargetData.SyncTarget(weapon.Target);

                        if (weapon.Target.HasTarget)
                        {
                            if (!weapon.Target.IsProjectile && !weapon.Target.IsFakeTarget && weapon.Target.Entity == null)
                            {
                                var oldChange = weapon.Target.TargetChanged;
                                weapon.Target.StateChange(true, Target.States.Invalid);
                                weapon.Target.TargetChanged = !weapon.FirstSync && oldChange;
                                weapon.FirstSync = false;
                            }
                            else if (weapon.Target.IsProjectile)
                            {
                                GridAi.TargetType targetType;
                                AcquireProjectile(weapon, out targetType);

                                if (targetType == GridAi.TargetType.None)
                                {
                                    if (weapon.NewTarget.CurrentState != Target.States.NoTargetsSeen)
                                        weapon.NewTarget.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen);
                                    if (weapon.Target.CurrentState != Target.States.NoTargetsSeen) weapon.Target.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen, !weapon.Comp.TrackReticle);
                                }
                            }
                        }
                    }

                    data.Report.PacketValid = true;
                }
            }
        }

        private void FakeTargetUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
                //noReproccess = true; // WHAT IS THIS?!@#?@!#?@!#?@!?#@!#?!@ @!?# @!? #@!?# @!?# @!?
                var targetPacket = packet as FakeTargetPacket;
                if (targetPacket?.Data == null)
                {
                    data.ErrorPacket.Error = $"Data was null:";
                    break;
                }

                var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                GridAi ai;
                if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                {
                    ai.DummyTarget.Update(targetPacket.Data, ai, null, true);
                    data.Report.PacketValid = true;
                }
                else
                    data.ErrorPacket.Error = $"myGrid was null {myGrid == null} GridTargetingAIs Not Found";

            }
        }

        private void PlayerIdUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
                var updatePacket = packet as BoolUpdatePacket;
                if (updatePacket == null)
                {
                    data.ErrorPacket.Error = $"updatePacket was null";
                    break;
                }

                if (updatePacket.Data)
                    PlayerConnected(updatePacket.EntityId);
                else //remove
                    PlayerDisconnected(updatePacket.EntityId);

                data.Report.PacketValid = true;
            }
        }

        private void ClientMouseEvent(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
                var mousePacket = packet as InputPacket;
                if (mousePacket?.Data == null)
                {
                    data.ErrorPacket.Error = $"Data was null {mousePacket?.Data == null}";
                    break;
                }

                long playerId;
                if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                {
                    PlayerMouseStates[playerId] = mousePacket.Data;

                    data.Report.PacketValid = true;
                }
                else
                    data.ErrorPacket.Error = "No Player Mouse State Found";

            }
        }

        private void ActiveControlUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
                var dPacket = packet as BoolUpdatePacket;

                var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;

                if (block == null || dPacket?.Data == null)
                {
                    data.ErrorPacket.Error = $"Data was null {dPacket?.Data == null} block was null {block == null}";
                    break;
                }
                long playerId;
                SteamToPlayer.TryGetValue(packet.SenderId, out playerId);

                UpdateActiveControlDictionary(block, playerId, dPacket.Data);

                data.Report.PacketValid = true;
            }
        }

        private void ActiveControlFullUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void ReticleUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void OverRidesUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void PlayerControlUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void TargetExpireUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void FullMouseUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void RangeUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void GridAiUiMidUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void CycleAmmo(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void GridOverRidesSync(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

        private void RescanGroupRequest(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }
        private void ClientMidUpdate(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }
        private void FocusStates(List<PacketObj> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var data = queue[i];
                var packet = data.Packet;
            }
        }

    }
}
