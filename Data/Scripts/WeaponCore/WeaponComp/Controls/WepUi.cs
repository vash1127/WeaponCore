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
            if (comp == null || !comp.Platform.Inited) return false;
            return comp.Set.Value.Guidance;
        }

        internal static void SetGuidance(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return;
            comp.Set.Value.Guidance = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static float GetDPS(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return 0;
            return comp.Set.Value.DPSModifier;
        }

        internal static void SetDPS(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return;
            comp.Set.Value.DPSModifier = newValue;

            comp.MaxRequiredPower = 0;
            comp.HeatPerSecond = 0;
            comp.OptimalDPS = 0;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                var w = comp.Platform.Weapons[i];
                var newBase = (int)Math.Ceiling((w.System.Values.Ammo.BaseDamage * newValue)* comp.Set.Value.Overload);
                
                if (newBase < 1)
                    newBase = 1;

                w.BaseDamage = comp.State.Value.Weapons[w.WeaponId].BaseDamage = newBase;

                var mulitplier = w.BaseDamage / w.System.Values.Ammo.BaseDamage;

                var oldRequired = w.RequiredPower;
                w.UpdateShotEnergy();
                w.UpdateRequiredPower();

                if (newBase != w.System.Values.Ammo.BaseDamage)
                {
                    w.HeatPShot = w.System.Values.HardPoint.Loading.HeatPerShot * (int)(mulitplier * mulitplier);

                    comp.MaxRequiredPower -= w.RequiredPower;
                    w.RequiredPower = w.RequiredPower * (mulitplier * mulitplier);
                    comp.MaxRequiredPower += w.RequiredPower;
                }
                else
                    w.HeatPShot = w.System.Values.HardPoint.Loading.HeatPerShot;

                w.TicksPerShot = (uint)(3600 / w.RateOfFire);
                w.TimePerShot = (3600d / w.RateOfFire);

                comp.HeatPerSecond += (60 / w.TicksPerShot) * w.HeatPShot;
                comp.OptimalDPS += (int)((60 / w.TicksPerShot) * w.BaseDamage);

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
            if (comp == null || !comp.Platform.Inited) return 0;
            return comp.Set.Value.ROFModifier;
        }

        internal static void SetROF(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return;
            comp.Set.Value.ROFModifier = newValue;

            comp.MaxRequiredPower = 0;
            comp.HeatPerSecond = 0;
            comp.OptimalDPS = 0;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                var newRate = (int)(w.System.Values.HardPoint.Loading.RateOfFire * comp.Set.Value.ROFModifier);

                if (newRate < 1)
                    newRate = 1;

                w.RateOfFire = comp.State.Value.Weapons[w.WeaponId].ROF = newRate;
                var oldRequired = w.RequiredPower;
                w.UpdateRequiredPower();


                w.TicksPerShot = (uint)(3600 / w.RateOfFire);
                w.TimePerShot = (3600d / w.RateOfFire);

                comp.HeatPerSecond += (60 / w.TicksPerShot) * w.HeatPShot;
                comp.OptimalDPS += (int)((60 / w.TicksPerShot) * w.BaseDamage);

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
            if (comp == null || !comp.Platform.Inited) return false;
            return comp.Set.Value.Overload == 2;
        }

        internal static void SetOverload(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return;

            if (newValue)
                comp.Set.Value.Overload = 2;
            else
            {
                comp.Set.Value.Overload = 1;
                comp.MaxRequiredPower = 0;
            }
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                if(comp.Platform.Weapons[i].System.IsBeamWeapon)
                    SetDPS(block, comp.Set.Value.DPSModifier);
            }
        }

        internal static bool CoreWeaponEnableCheck(IMyTerminalBlock block, int id)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return false;
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
                        if (w.System.Values.Ui.DamageModifier && w.System.EnergyAmmo || w.System.IsHybrid)
                        {
                            return true;
                        }
                        break;
                    case -3:
                        if (w.System.Values.Ui.RateOfFire)
                        {
                            return true;
                        }
                        break;
                    case -4:
                        if (w.System.Values.Ui.EnableOverload && w.System.IsBeamWeapon)
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
