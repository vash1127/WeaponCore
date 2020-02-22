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
                //TODO change this
                Registered = true;
                TerminalBlock.AppendingCustomInfo += AppendingCustomInfo;

                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);

                if(!Session.IsClient)
                    BlockInventory.ContentsChanged += OnContentsChanged;

            }
            else
            {
                if (Registered)
                {
                    //TODO change this
                    Registered = false;
                    TerminalBlock.AppendingCustomInfo -= AppendingCustomInfo;

                    MyCube.IsWorkingChanged -= IsWorkingChanged;

                    if (BlockInventory == null) Log.Line($"BlockInventory is null");
                    else
                        if (!Session.IsClient) BlockInventory.ContentsChanged -= OnContentsChanged;
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
                    if(HasChargeWeapon) stringBuilder.Append("\n[Current Charge]: " + State.Value.CurrentCharge.ToString("0.0") + " Mw");
                    stringBuilder.Append("\n[Required Power]: " + MaxRequiredPower.ToString("0.0") + " Mw");
                }

                stringBuilder.Append("\n\n** Use Weapon Wheel Menu\n** to control weapons using\n** MMB outside of this terminal");
                if (Debug)
                {
                    foreach (var weapon in Platform.Weapons)
                    {
                        stringBuilder.Append($"\n\nWeapon: {weapon.System.WeaponName} - Enabled: {weapon.Set.Enable && weapon.Comp.State.Value.Online && weapon.Comp.Set.Value.Overrides.Activate}");
                        stringBuilder.Append($"\nTargetState: {weapon.Target.State} - Manual: {weapon.Comp.UserControlled || weapon.Target.IsFakeTarget}");
                        stringBuilder.Append($"\nEvent: {weapon.LastEvent} - Ammo :{!weapon.OutOfAmmo}");
                        stringBuilder.Append($"\nOverHeat: {weapon.State.Overheated} - Shooting: {weapon.IsShooting}");
                        stringBuilder.Append($"\nisAligned: {weapon.Target.IsAligned} - Tracking: {weapon.Target.IsTracking}");
                        stringBuilder.Append($"\nCanShoot: {weapon.Timings.ShootDelayTick <= weapon.Comp.Session.Tick} - Charging: {weapon.State.Charging}");
                        stringBuilder.Append($"\nAiShooting: {weapon.AiShooting} - lastCheck: {weapon.Comp.Session.Tick - weapon.Target.CheckTick}");
                        stringBuilder.Append($"\nMagSize: {weapon.System.EnergyMagSize} - CurrentCharge: {State.Value.CurrentCharge}({weapon.State.CurrentCharge})");
                        stringBuilder.Append($"\nChargeTime: {weapon.Timings.ChargeUntilTick}({weapon.Comp.Ai.Session.Tick}) - Delay: {weapon.Timings.ChargeDelayTicks}");
                        stringBuilder.Append($"\nCharging: {weapon.State.Charging}({weapon.System.MustCharge}) - Delay: {weapon.Timings.ChargeDelayTicks}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon AppendingCustomInfo: {ex}"); }
        }
    }
}
