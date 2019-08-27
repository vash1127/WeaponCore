using System;
using Sandbox.ModAPI;
using WeaponCore.Support;
using static WeaponCore.Support.AmmoTrajectory.GuidanceType;

namespace WeaponCore
{
    internal static class WepUi
    {
        internal static bool GetGuidance(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp?.Set.Value.Guidance ?? false;
        }

        internal static void SetGuidance(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return;
            comp.Set.Value.Guidance = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static float GetDPS(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp?.Set.Value.DPSModifier ?? 0f;
        }

        internal static void SetDPS(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return;
            comp.Set.Value.DPSModifier = newValue;

            comp.MaxRequiredPower = 0;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                var w = comp.Platform.Weapons[i];
                var newBase = (int)Math.Ceiling(w.System.Values.Ammo.BaseDamage * newValue);

                if (newBase < 1)
                    newBase = 1;

                w.System.BaseDamage = comp.State.Value.Weapons[w.WeaponId].BaseDamage = newBase;

                var oldRequired = w.RequiredPower;
                w.UpdateShotEnergy();
                w.UpdateRequiredPower();

                if (w.IsShooting)
                    comp.CurrentSinkPowerRequested -= (oldRequired - w.RequiredPower);

                comp.Ai.TotalSinkPower -= (oldRequired - w.RequiredPower);
            }
            comp.TerminalRefresh();
            comp.Ai.RecalcPowerPercent = true;
            comp.Ai.UpdatePowerSources = true;
            comp.Ai.AvailablePowerIncrease = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static float GetROF(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp?.Set.Value.ROFModifier ?? 0f;
        }

        internal static void SetROF(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return;
            comp.Set.Value.ROFModifier = newValue;

            comp.MaxRequiredPower = 0;
            comp.HeatPerSecond = 0;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];
                var newRate = (int)(w.System.Values.HardPoint.Loading.RateOfFire * newValue * comp.Set.Value.Overload);

                var oldRequired = w.RequiredPower;
                w.UpdateRequiredPower();

                if (newRate < 1)
                    newRate = 1;
                else if (newRate > w.System.Values.HardPoint.Loading.RateOfFire) {
                    w.System.HeatPShot = w.System.Values.HardPoint.Loading.HeatPerShot * (newRate / w.System.Values.HardPoint.Loading.RateOfFire);

                    if(w.System.EnergyAmmo || w.System.IsHybrid)
                        w.RequiredPower = w.RequiredPower * (newRate / w.System.Values.HardPoint.Loading.RateOfFire);
                }

                w.RateOfFire = comp.State.Value.Weapons[w.WeaponId].ROF = newRate;

                
                w.TicksPerShot = (uint)(3600 / w.RateOfFire);
                w.TimePerShot = (3600d / w.RateOfFire);

                comp.HeatPerSecond += (60 / w.TicksPerShot) * w.System.HeatPShot;

                if (w.IsShooting)
                    comp.CurrentSinkPowerRequested -= (oldRequired - w.RequiredPower);

                comp.Ai.TotalSinkPower -= (oldRequired - w.RequiredPower);


            }
            comp.TerminalRefresh();
            comp.Ai.RecalcPowerPercent = true;
            comp.Ai.UpdatePowerSources = true;
            comp.Ai.AvailablePowerIncrease = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static bool GetOverload(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp?.Set.Value.Overload == 2;
        }

        internal static void SetOverload(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return;

            if (newValue)
                comp.Set.Value.Overload = 2;
            else
            {
                comp.Set.Value.Overload = 1;
                comp.MaxRequiredPower = 0;
            }

            comp.HeatPerSecond = 0;
            var refresh = false;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                var w = comp.Platform.Weapons[i];
                var oldRequired = w.RequiredPower;

                if (w.System.EnergyAmmo)
                {
                    
                    w.RateOfFire = comp.State.Value.Weapons[w.WeaponId].ROF = (int)(w.System.Values.HardPoint.Loading.RateOfFire * comp.Set.Value.ROFModifier) * comp.Set.Value.Overload;

                    if (w.RateOfFire > w.System.Values.HardPoint.Loading.RateOfFire)
                    {
                        comp.MaxRequiredPower -= oldRequired;

                        w.System.HeatPShot = w.System.Values.HardPoint.Loading.HeatPerShot * ((w.RateOfFire / w.System.Values.HardPoint.Loading.RateOfFire) * (w.RateOfFire / w.System.Values.HardPoint.Loading.RateOfFire));

                        w.RequiredPower = w.RequiredPower * ((w.RateOfFire / w.System.Values.HardPoint.Loading.RateOfFire) * (w.RateOfFire / w.System.Values.HardPoint.Loading.RateOfFire));
                        comp.MaxRequiredPower += w.RequiredPower;
                    }
                    else
                    {
                        w.System.HeatPShot = w.System.Values.HardPoint.Loading.HeatPerShot;
                        w.UpdateRequiredPower();
                    }

                    w.TicksPerShot = (uint)(3600 / w.RateOfFire);
                    w.TimePerShot = (3600d / w.RateOfFire);

                    comp.HeatPerSecond += (60 / w.TicksPerShot) * w.System.HeatPShot;

                    if (w.IsShooting)
                        comp.CurrentSinkPowerRequested -= (oldRequired - w.RequiredPower);

                    comp.Ai.TotalSinkPower -= (oldRequired - w.RequiredPower);
                    refresh = true;
                }
            }

            if (refresh)
            {
                comp.TerminalRefresh();
                comp.Ai.RecalcPowerPercent = true;
                comp.Ai.UpdatePowerSources = true;
                comp.Ai.AvailablePowerIncrease = true;
                comp.SettingsUpdated = true;
                comp.ClientUiUpdate = true;
            }
        }

        internal static bool CoreWeaponEnableCheck(IMyTerminalBlock block, int id)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return false;
            else if (id == 0) return true;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                //Log.Line($"w.System.Values.Ui.ToggleGuidance");
                var w = comp.Platform.Weapons[i];
                switch (id) {
                    case -1:
                        if (w.System.Values.Ui.ToggleGuidance && w.System.Values.Ammo.Trajectory.Guidance != None) {
                            return true;
                        }
                        break;
                    case -2:
                        if (w.System.Values.Ui.DamageModifier.Enable && w.System.EnergyAmmo)
                        {
                            return true;
                        }
                        break;
                    case -3:
                        if (w.System.Values.Ui.RateOfFire.Enable)
                        {
                            return true;
                        }
                        break;
                    case -4:
                        if (w.System.Values.Ui.EnableOverload && w.System.EnergyAmmo)
                        {
                            return true;
                        }
                        break;
                }
            }

            return false;
        }
    }
}
