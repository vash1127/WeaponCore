using System;
using System.Text;
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
                Registered = true;
                if (IsSorterTurret)
                    SorterBase.AppendingCustomInfo += AppendingCustomInfo;
                else
                    MissileBase.AppendingCustomInfo += AppendingCustomInfo;

                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);

                BlockInventory.ContentsChanged += OnContentsChanged;

            }
            else
            {
                if (Registered)
                {
                    Registered = false;
                    if (IsSorterTurret)
                    {
                        if (SorterBase == null) Log.Line($"SortBase is null");
                        else SorterBase.AppendingCustomInfo -= AppendingCustomInfo;
                    }
                    else
                    {
                        if (MissileBase == null) Log.Line($"MissileBase is null");
                        else MissileBase.AppendingCustomInfo -= AppendingCustomInfo;

                    }

                    MyCube.IsWorkingChanged -= IsWorkingChanged;

                    if (BlockInventory == null) Log.Line($"BlockInventory is null");
                    else BlockInventory.ContentsChanged -= OnContentsChanged;
                }
            }
        }

        private void OnContentsChanged(MyInventoryBase obj)
        {
            try
            {
                if (LastInventoryChangedTick < Session.Tick && !IgnoreInvChange && Registered)
                {
                    for (int i = 0; i < Platform.Weapons.Length; i++)
                    {
                        var w = Platform.Weapons[i];
                        if (!w.System.EnergyAmmo)
                            Session.ComputeStorage(w);
                    }
                    
                    LastInventoryChangedTick = Session.Tick;
                }
            }
            catch (Exception ex)
            {
                Log.Line($"Exception in OnContentsChanged: {ex}");
            }
        }

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
                if (MyCube.ResourceSink.CurrentInputByType(GId) < 0) Log.Line($"IsWorking:{IsWorking}(was:{wasFunctional}) - online:{State.Value.Online} - Func:{IsFunctional} - GridAvailPow:{Ai.GridAvailablePower} - SinkPow:{SinkPower} - SinkReq:{MyCube.ResourceSink.RequiredInputByType(GId)} - SinkCur:{MyCube.ResourceSink.CurrentInputByType(GId)}");

                if(!Session.DedicatedServer)
                    TerminalRefresh();

                if (!IsWorking && Registered)
                {
                    foreach (var w in Platform.Weapons)
                        w.StopShooting();
                }
                IsWorkingChangedTick = Session.Tick;
            }
            catch (Exception ex) { Log.ThreadedWrite($"Exception in IsWorkingChanged: {ex}"); }
        }

        internal string GetSystemStatus()
        {
            if (!State.Value.Online && !MyCube.IsFunctional) return "[Systems Fault]";
            if (!State.Value.Online && !MyCube.IsWorking) return "[Systems Offline]";
            //if (Charging && !(SinkPower > 0)) return "[Insufficient Power]";
            //if (Charging) return "[Systems Charging]";
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

                if (HasEnergyWeapon)
                {
                    stringBuilder.Append("\n[Current Draw]: " + SinkPower.ToString("0.0") + " Mw");
                    if(HasChargeWeapon) stringBuilder.Append("\n[Current Charge]: " + CurrentCharge.ToString("0.0") + " Mw");
                    stringBuilder.Append("\n[Required Power]: " + MaxRequiredPower.ToString("0.0") + " Mw");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon AppendingCustomInfo: {ex}"); }
        }

        /*
        internal void CurrentInputChanged(MyDefinitionId changedResourceTypeId, float oldInput, MyResourceSinkComponent sink)
        {
            try
            {

                var currentInput = sink.CurrentInputByType(changedResourceTypeId);
                var tick = Session.Tick;
                if (Ai.ResetPower && tick != LastUpdateTick)
                {
                    if (currentInput < CurrentSinkPowerRequested && currentInput > IdlePower)
                    {
                        if (Ai.ResetPowerTick != tick)
                        {
                            Ai.CurrentWeaponsDraw = 0;
                            Ai.ResetPowerTick = tick;
                            //Ai.RecalcLowPowerTick = tick + 20;
                            Ai.UpdatePowerSources = true;
                        }

                        LastUpdateTick = tick;
                        Ai.CurrentWeaponsDraw += currentInput;
                    }
                    else
                    {
                        DelayTicks = 0;
                        ShootTick = 0;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon CurrentInputChanged: {ex}"); }
        }*/
    }
}
