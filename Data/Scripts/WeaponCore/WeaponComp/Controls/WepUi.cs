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

        internal static float GetDps(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return 0;
            return comp.Set.Value.DPSModifier;
        }

        internal static void SetDps(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return;
            comp.Set.Value.DPSModifier = newValue;

            comp.MaxRequiredPower = 0;
            comp.HeatPerSecond = 0;
            comp.OptimalDPS = 0;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                var w = comp.Platform.Weapons[i];
                if (!w.System.EnergyAmmo) {
                    comp.OptimalDPS += w.DPS;
                    comp.MaxRequiredPower += w.RequiredPower;
                    comp.HeatPerSecond += (60 / (float)w.TicksPerShot) * w.HeatPShot * w.System.BarrelsPerShot;
                    continue;
                }
                var newBase = (int)Math.Ceiling(w.System.BaseDamage * newValue);

                if (w.System.IsBeamWeapon)
                    newBase *= comp.Set.Value.Overload;


                if (newBase < 1)
                    newBase = 1;

                w.BaseDamage = newBase;

                var oldRequired = w.RequiredPower;
                w.UpdateShotEnergy();
                w.UpdateRequiredPower();

                var mulitplier = (w.System.EnergyAmmo && w.System.BaseDamage > 0) ? w.BaseDamage / w.System.BaseDamage : 1;

                if (w.BaseDamage > w.System.BaseDamage)
                    mulitplier = mulitplier * mulitplier;

                w.HeatPShot = w.System.HeatPerShot * mulitplier;
                w.areaEffectDmg = w.System.AreaEffectDamage * mulitplier;
                w.detonateDmg = w.System.DetonationDamage * mulitplier;


                comp.MaxRequiredPower -= w.RequiredPower;
                w.RequiredPower = w.RequiredPower * mulitplier;
                comp.MaxRequiredPower += w.RequiredPower;

                w.TicksPerShot = (uint)(3600 / w.RateOfFire);
                w.TimePerShot = (3600d / w.RateOfFire);

                var oldDps = w.DPS;
                w.DPS = (60 / (float)w.TicksPerShot) * w.BaseDamage * w.System.BarrelsPerShot;

                if (w.System.Values.Ammo.AreaEffect.AreaEffect != AreaDamage.AreaEffectType.Disabled)
                {
                    if (w.System.Values.Ammo.AreaEffect.Detonation.DetonateOnEnd)
                        w.DPS += (w.detonateDmg / 2) * (w.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                          ? w.System.Values.Ammo.Trajectory.AccelPerSec /
                                            w.System.Values.Ammo.Trajectory.DesiredSpeed
                                          : 1);
                    else
                        w.DPS += (w.areaEffectDmg / 2) *
                                      (w.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                          ? w.System.Values.Ammo.Trajectory.AccelPerSec /
                                            w.System.Values.Ammo.Trajectory.DesiredSpeed
                                          : 1);
                }
                comp.HeatPerSecond += (60 / (float)w.TicksPerShot) * w.HeatPShot * w.System.BarrelsPerShot;
                comp.OptimalDPS += w.DPS;

                if (w.IsShooting)
                {
                    comp.CurrentSinkPowerRequested -= (oldRequired - w.RequiredPower);
                    comp.CurrentDPS -= (oldDps - w.DPS);
                }

                comp.Ai.TotalSinkPower -= (oldRequired - w.RequiredPower);
            }
            comp.TerminalRefresh();
            comp.Ai.RecalcPowerPercent = true;
            comp.Ai.UpdatePowerSources = true;
            comp.Ai.AvailablePowerIncrease = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static float GetRof(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return 0;
            return comp.Set.Value.ROFModifier;
        }

        internal static void SetRof(IMyTerminalBlock block, float newValue)
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

                var newRate = (int)(w.System.RateOfFire * comp.Set.Value.ROFModifier);

                if (newRate < 1)
                    newRate = 1;

                w.RateOfFire = newRate;
                var oldRequired = w.RequiredPower;
                w.UpdateRequiredPower();


                w.TicksPerShot = (uint)(3600 / w.RateOfFire);
                w.TimePerShot = (3600d / w.RateOfFire);

                var oldDps = w.DPS;
                w.DPS = (60 / (float)w.TicksPerShot) * w.BaseDamage * w.System.BarrelsPerShot;

                if (w.System.Values.Ammo.AreaEffect.AreaEffect != AreaDamage.AreaEffectType.Disabled)
                {
                    if (w.System.Values.Ammo.AreaEffect.Detonation.DetonateOnEnd)
                        w.DPS += (w.detonateDmg / 2) * (w.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                          ? w.System.Values.Ammo.Trajectory.AccelPerSec /
                                            w.System.Values.Ammo.Trajectory.DesiredSpeed
                                          : 1);
                    else
                        w.DPS += (w.areaEffectDmg / 2) *
                                      (w.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                          ? w.System.Values.Ammo.Trajectory.AccelPerSec /
                                            w.System.Values.Ammo.Trajectory.DesiredSpeed
                                          : 1);
                }

                comp.HeatPerSecond += (60 / (float)w.TicksPerShot) * w.HeatPShot * w.System.BarrelsPerShot;
                comp.OptimalDPS += w.DPS;

                if (w.IsShooting)
                {
                    comp.CurrentSinkPowerRequested -= (oldRequired - w.RequiredPower);
                    comp.CurrentDPS -= (oldDps - w.DPS);
                }

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
                    SetDps(block, comp.Set.Value.DPSModifier);
            }
        }

        internal static bool CoreWeaponEnableCheck(IMyTerminalBlock block, int id)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return false;
            if (id == 0) return true;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                //Log.Line($"w.System.Values.Ui.ToggleGuidance");
                var w = comp.Platform.Weapons[i];
                switch (id) {
                    case -1:
                        if (w.System.Values.HardPoint.Ui.ToggleGuidance && w.System.Values.Ammo.Trajectory.Guidance != None) {
                            return true;
                        }
                        break;
                    case -2:
                        if (w.System.Values.HardPoint.Ui.DamageModifier && w.System.EnergyAmmo || w.System.IsHybrid)
                        {
                            return true;
                        }
                        break;
                    case -3:
                        if (w.System.Values.HardPoint.Ui.RateOfFire)
                        {
                            return true;
                        }
                        break;
                    case -4:
                        if (w.System.Values.HardPoint.Ui.EnableOverload && w.System.IsBeamWeapon)
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
