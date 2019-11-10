using System;
using Sandbox.ModAPI;
using WeaponCore.Support;
using static WeaponCore.Support.AmmoTrajectory.GuidanceType;

namespace WeaponCore
{
    internal static class WepUi
    {
        internal static bool GetGuidance(IMyTerminalBlock block, int wepId)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform == null || !comp.Platform.Inited) return false;
            return comp.Set.Value.Guidance;
        }

        internal static void SetGuidance(IMyTerminalBlock block, int wepId, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform == null || !comp.Platform.Inited) return;
            comp.Set.Value.Guidance = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static float GetDps(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform == null || !comp.Platform.Inited) return 0;
            return comp.Set.Value.DpsModifier;
        }

        internal static void SetDps(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform == null || !comp.Platform.Inited) return;
            comp.Set.Value.DpsModifier = newValue;

            comp.MaxRequiredPower = 0;
            comp.HeatPerSecond = 0;
            comp.Ai.OptimalDps -= comp.OptimalDps;
            comp.OptimalDps = 0;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                var w = comp.Platform.Weapons[i];
                if (!w.System.EnergyAmmo) {
                    comp.OptimalDps += w.Dps;
                    comp.MaxRequiredPower += w.RequiredPower;
                    comp.HeatPerSecond += (60 / (float)w.TicksPerShot) * w.HeatPShot * w.System.BarrelsPerShot;
                    continue;
                }
                var newBase = w.System.BaseDamage * newValue;

                if (w.System.IsBeamWeapon)
                    newBase *= comp.Set.Value.Overload;


                if (newBase < 0)
                    newBase = 0;

                w.BaseDamage = newBase;

                var oldRequired = w.RequiredPower;
                w.UpdateShotEnergy();
                w.UpdateRequiredPower();

                var mulitplier = (w.System.EnergyAmmo && w.System.BaseDamage > 0) ? w.BaseDamage / w.System.BaseDamage : 1;

                if (w.BaseDamage > w.System.BaseDamage)
                    mulitplier *= mulitplier;

                w.HeatPShot = w.System.HeatPerShot * mulitplier;
                w.AreaEffectDmg = w.System.AreaEffectDamage * mulitplier;
                w.DetonateDmg = w.System.DetonationDamage * mulitplier;


                comp.MaxRequiredPower -= w.RequiredPower;
                w.RequiredPower *= mulitplier;
                comp.MaxRequiredPower += w.RequiredPower;

                w.TicksPerShot = (uint)(3600f / w.RateOfFire);
                w.TimePerShot = (3600d / w.RateOfFire);

                var oldDps = w.Dps;
                w.Dps = (60 / (float)w.TicksPerShot) * w.BaseDamage * w.System.BarrelsPerShot;

                if (w.System.Values.Ammo.AreaEffect.AreaEffect != AreaDamage.AreaEffectType.Disabled)
                {
                    if (w.System.Values.Ammo.AreaEffect.Detonation.DetonateOnEnd)
                        w.Dps += (w.DetonateDmg / 2) * (w.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                          ? w.System.Values.Ammo.Trajectory.AccelPerSec /
                                            w.System.Values.Ammo.Trajectory.DesiredSpeed
                                          : 1);
                    else
                        w.Dps += (w.AreaEffectDmg / 2) *
                                      (w.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                          ? w.System.Values.Ammo.Trajectory.AccelPerSec /
                                            w.System.Values.Ammo.Trajectory.DesiredSpeed
                                          : 1);
                }
                comp.HeatPerSecond += (60 / (float)w.TicksPerShot) * w.HeatPShot * w.System.BarrelsPerShot;
                comp.OptimalDps += w.Dps;

                if (w.IsShooting)
                {
                    comp.CurrentSinkPowerRequested -= (oldRequired - w.RequiredPower);
                    comp.CurrentDps -= (oldDps - w.Dps);
                }

                comp.Ai.TotalSinkPower -= (oldRequired - w.RequiredPower);
            }
            comp.Ai.OptimalDps += comp.OptimalDps;
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
            if (comp?.Platform == null || !comp.Platform.Inited) return 0;
            return comp.Set.Value.RofModifier;
        }

        internal static void SetRof(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform == null || !comp.Platform.Inited) return;
            comp.Set.Value.RofModifier = newValue;

            comp.MaxRequiredPower = 0;
            comp.HeatPerSecond = 0;
            comp.Ai.OptimalDps -= comp.OptimalDps;
            comp.OptimalDps = 0;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                var newRate = (int)(w.System.RateOfFire * comp.Set.Value.RofModifier);

                if (newRate < 1)
                    newRate = 1;

                w.RateOfFire = newRate;
                var oldRequired = w.RequiredPower;
                w.UpdateRequiredPower();


                w.TicksPerShot = (uint)(3600f / w.RateOfFire);
                w.TimePerShot = (3600d / w.RateOfFire);

                w.UpdateBarrelRotation();

                var oldDps = w.Dps;
                w.Dps = (60 / (float)w.TicksPerShot) * w.BaseDamage * w.System.BarrelsPerShot;

                if (w.System.Values.Ammo.AreaEffect.AreaEffect != AreaDamage.AreaEffectType.Disabled)
                {
                    if (w.System.Values.Ammo.AreaEffect.Detonation.DetonateOnEnd)
                        w.Dps += (w.DetonateDmg / 2) * (w.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                          ? w.System.Values.Ammo.Trajectory.AccelPerSec /
                                            w.System.Values.Ammo.Trajectory.DesiredSpeed
                                          : 1);
                    else
                        w.Dps += (w.AreaEffectDmg / 2) *
                                      (w.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                          ? w.System.Values.Ammo.Trajectory.AccelPerSec /
                                            w.System.Values.Ammo.Trajectory.DesiredSpeed
                                          : 1);
                }

                comp.HeatPerSecond += (60 / (float)w.TicksPerShot) * w.HeatPShot * w.System.BarrelsPerShot;
                comp.OptimalDps += w.Dps;

                if (w.IsShooting)
                {
                    comp.CurrentSinkPowerRequested -= (oldRequired - w.RequiredPower);
                    comp.CurrentDps -= (oldDps - w.Dps);
                }

                comp.Ai.TotalSinkPower -= (oldRequired - w.RequiredPower);


            }
            comp.Ai.OptimalDps += comp.OptimalDps;
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
            if (comp?.Platform == null || !comp.Platform.Inited) return false;
            return comp.Set.Value.Overload == 2;
        }

        internal static void SetOverload(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform == null || !comp.Platform.Inited) return;

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
                    SetDps(block, comp.Set.Value.DpsModifier);
            }
        }

        internal static float GetRange(IMyTerminalBlock block) {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform == null || !comp.Platform.Inited) return 100;
            return comp.Set.Value.Range;
        }

        internal static void SetRange(IMyTerminalBlock block, float range) {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform == null || !comp.Platform.Inited) return;
            comp.Set.Value.Range = range;
        }

        internal static bool CoreWeaponEnableCheck(IMyTerminalBlock block, int id)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform == null || !comp.Platform.Inited) return false;
            if (id == 0) return true;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
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
