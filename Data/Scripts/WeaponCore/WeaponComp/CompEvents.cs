using System;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        internal void RegisterEvents(bool register = true)
        {
            if (register)
            {
                if(IsAiOnlyTurret)
                    AiOnlyTurret.AppendingCustomInfo += AppendingCustomInfo;
                else
                    ControllableTurret.AppendingCustomInfo += AppendingCustomInfo;

                MyCube.IsWorkingChanged += IsWorkingChanged;
                MyCube.OnClosing += OnClosing;

                IsWorkingChanged(MyCube);

                BlockInventory.ContentsChanged += OnContentsChanged;
                Sink.CurrentInputChanged += CurrentInputChanged;
            }
            else
            {
                if (IsAiOnlyTurret)
                    AiOnlyTurret.AppendingCustomInfo -= AppendingCustomInfo;
                else
                    ControllableTurret.AppendingCustomInfo -= AppendingCustomInfo;

                MyCube.IsWorkingChanged -= IsWorkingChanged;
                MyCube.OnClosing -= OnClosing;
                BlockInventory.ContentsChanged -= OnContentsChanged;
                //BlockInventory.ContentsRemoved -= OnContentsRemoved;
                Sink.CurrentInputChanged -= CurrentInputChanged;

                foreach (var w in Platform.Weapons)
                    w.Comp.MyCube.PositionComp.OnPositionChanged -= w.UpdatePartPos;
            }
        }


        private void OnContentsChanged(MyInventoryBase obj)
        {
            try
            {
                if (LastInventoryChangedTick < Ai.Session.Tick && !IgnoreInvChange)
                {
                    //BlockInventory.Refresh();

                    for (int i = 0; i < Platform.Weapons.Length; i++)
                        Session.ComputeStorage(Platform.Weapons[i]);
                    
                    LastInventoryChangedTick = Ai.Session.Tick;
                }
            }
            catch (Exception ex)
            {
                Log.Line($"Exception in OnContentsChanged: {ex}");
            }
        }

        /*internal void OnContentsAdded(MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                Log.Line("InventoryAdded");
                var defId = item.Content.GetId();

                List<int> weaponIds;
                if (!Platform.Structure.AmmoToWeaponIds.TryGetValue(defId, out weaponIds)) return;

                foreach (var id in weaponIds)
                {
                    var weapon = Platform.Weapons[id];
                    Session.ComputeStorage(weapon);
                }
                Ai.Session.InventoryEvent.Enqueue(new InventoryChange(Platform.Weapons[0], item, amount, InventoryChange.ChangeType.Add));
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
        }*/

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            try
            {
                var wasFunctional = IsFunctional;
                IsFunctional = myCubeBlock.IsFunctional;
                if (!wasFunctional && IsFunctional && IsWorkingChangedTick > 0)
                    Status = Start.ReInit;

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
                IsWorkingChangedTick = Ai.Session.Tick;
            }
            catch (Exception ex) { Log.ThreadedWrite($"Exception in IsWorkingChanged: {ex}"); }
        }

        internal string GetSystemStatus()
        {
            if (!State.Value.Online && !MyCube.IsFunctional) return "[Systems Fault]";
            if (!State.Value.Online && !MyCube.IsWorking) return "[Systems Offline]";
            if (Charging && !(SinkPower > 0)) return "[Insufficient Power]";
            if (Charging) return "[Systems Charging]";
            return "[Systems Online]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetSystemStatus();

                stringBuilder.Append(status +
                    "\n[Optimal DPS]: " + OptimalDps.ToString("0.0") + 
                    "\n[Current DPS]: " + CurrentDps.ToString("0.0") +" ("+ (CurrentDps/OptimalDps).ToString("P") + ")");

                if (HeatPerSecond > 0)
                    stringBuilder.Append("\n__________________________________" +
                    "\n[Heat Generated / s]: " + HeatPerSecond.ToString("0.0") + " W" +
                    "\n[Heat Dissipated / s]: " + HeatSinkRate.ToString("0.0") + " W" +
                    "\n[Current Heat]: " +CurrentHeat.ToString("0.0") + " j (" + (CurrentHeat /MaxHeat).ToString("P")+")");

                if (HeatPerSecond > 0 && HasEnergyWeapon)
                    stringBuilder.Append("\n__________________________________");

                if(HasEnergyWeapon)
                    stringBuilder.Append("\n[Current Draw]: " + SinkPower.ToString("0.0") + " Mw" +
                        "\n[Required Power]: " +CurrentSinkPowerRequested.ToString("0.0") + " Mw"+
                        "\n[Max Required Power]: " +MaxRequiredPower.ToString("0.0") + " Mw");
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon AppendingCustomInfo: {ex}"); }
        }

        internal void CurrentInputChanged(MyDefinitionId changedResourceTypeId, float oldInput, MyResourceSinkComponent sink)
        {
            try
            {

                var currentInput = sink.CurrentInputByType(changedResourceTypeId);
                var tick = Ai.Session.Tick;
                if (Ai.ResetPower && tick != LastUpdateTick)
                {
                    if (currentInput < CurrentSinkPowerRequested && currentInput > IdlePower)
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
                    else
                    {
                        DelayTicks = 0;
                        ShootTick = 0;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon CurrentInputChanged: {ex}"); }
        }

        internal void OnClosing(MyEntity myEntity)
        {
            Log.Line($"cubeclosing");
            SinkInfo.RequiredInputFunc = null;
        }
    }
}
