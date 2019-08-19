using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;

namespace WeaponCore.Support
{ 
    public partial class WeaponComponent
    {
        internal void RegisterEvents(bool register = true)
        {
            if (register)
            {
                Turret.AppendingCustomInfo += AppendingCustomInfo;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);
                BlockInventory.ContentsAdded += OnContentsAdded;
                BlockInventory.ContentsRemoved += OnContentsRemoved;
                Sink.CurrentInputChanged += CurrentInputChanged;
            }
            else
            {
                Turret.AppendingCustomInfo -= AppendingCustomInfo;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
                BlockInventory.ContentsAdded -= OnContentsAdded;
                BlockInventory.ContentsRemoved -= OnContentsRemoved;
                Sink.CurrentInputChanged -= CurrentInputChanged;
                foreach (var w in Platform.Weapons)
                    w.EntityPart.PositionComp.OnPositionChanged -= w.PositionChanged;
            }
        }

        internal void OnContentsAdded(MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                var defId = item.Content.GetId();

                List<int> weaponIds;
                if (!Platform.Structure.AmmoToWeaponIds.TryGetValue(defId, out weaponIds)) return;

                foreach (var id in weaponIds)
                {
                    var weapon = Platform.Weapons[id];
                    Session.ComputeStorage(weapon);
                }
                Session.Instance.InventoryEvent.Enqueue(new InventoryChange(Platform.Weapons[0], item, amount, InventoryChange.ChangeType.Add));
            }
            catch (Exception ex) { Log.Line($"Exception in OnContentsAdded: {ex}"); }
        }

        internal void OnContentsRemoved(MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                var defId = item.Content.GetId();

                List<int> weaponIds;
                if (!Platform.Structure.AmmoToWeaponIds.TryGetValue(defId, out weaponIds)) return;
                foreach (var id in weaponIds)
                {
                    var weapon = Platform.Weapons[id];
                    Session.ComputeStorage(weapon);
                }
                //weapon.SuspendAmmoTick = 0;
                //weapon.UnSuspendAmmoTick = 0;
            }
            catch (Exception ex) { Log.Line($"Exception in OnContentsRemoved: {ex}"); }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            var wasFunctional = IsFunctional;
            IsFunctional = myCubeBlock.IsFunctional;
            if (!wasFunctional && IsFunctional && IsWorkingChangedTick > 0)
                Status = Start.ReInit;

            Log.Line($"isWorking:{IsWorking} - isFunctional:{IsFunctional} - RemoveParts:{!wasFunctional && IsFunctional}");
            IsWorking = myCubeBlock.IsWorking;
            State.Value.Online = IsWorking && IsFunctional;
            TerminalRefresh();
            if (!IsWorking)
            {
                foreach (var w in Platform.Weapons)
                {
                    //WepUi.SetEnable((IMyTerminalBlock)MyCube, w.WeaponId, false);
                    w.StopShooting();
                }
            }
            IsWorkingChangedTick = Session.Instance.Tick;
        }

        internal string GetSystemStatus()
        {
            if (!State.Value.Online && !MyCube.IsFunctional) return "[Systems Fault]";
            if (!State.Value.Online && !MyCube.IsWorking) return "[Systems Offline]";
            if (Charging && SinkPower == 0) return "[Insufficient Power]";
            if (Charging) return "[Systems Charging]";
            return "[Systems Online]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetSystemStatus();
                //if (status == "[Systems Online]" || status == "[Systems Fault]" || status == "[Systems Offline]" || status == "[Insufficient Power]" || status == "[Systems Charging]")
                //{
                    stringBuilder.Append(status +
                                         "\n" +
                                         "\n[Required Power]: " + SinkPower.ToString("0.0") + " Mw");
               // }
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon AppendingCustomInfo: {ex}"); }
        }

        internal void CurrentInputChanged(MyDefinitionId changedResourceTypeId, float oldInput, MyResourceSinkComponent sink)
        {
            var currentInput = sink.CurrentInputByType(changedResourceTypeId);
            var tick = Session.Instance.Tick;
            if (Ai.ResetPower && tick != LastUpdateTick)
            {
                if (currentInput < CurrentSinkPowerRequested)
                {
                    if (Ai.ResetPowerTick != tick)
                    {
                        Ai.CurrentWeaponsDraw = 0;
                        Ai.ResetPowerTick = tick;
                        Ai.RecalcLowPowerTick = tick + 20;
                        Ai.UpdatePowerSources = true;
                    }

                    LastUpdateTick = tick;
                    Ai.CurrentWeaponsDraw += currentInput;
                    //Log.Line($"curent Input: {sink.CurrentInputByType(changedResourceTypeId)} SinkRequested: {CurrentSinkPowerRequested} ratio: {sink.SuppliedRatioByType(changedResourceTypeId)} Current Weapon Draw: {Ai.CurrentWeaponsDraw} Current Tick: {Ai.MySession.Tick}");
                }
                else {
                    DelayTicks = 0;
                    ShootTick = 0;
                }
            }
        }
    }
}
