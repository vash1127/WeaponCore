using System;
using Sandbox.ModAPI;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.AreaDamageDef;

namespace WeaponCore
{
    internal static class WepUi
    {

        internal static bool GetGuidance(IMyTerminalBlock block, int wepId)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return false;
            return comp.Set.Value.Guidance;
        }

        internal static void SetGuidance(IMyTerminalBlock block, int wepId, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null ||  comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            comp.Set.Value.Guidance = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static float GetDps(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return 0;
            return comp.Set.Value.DpsModifier;
        }

        internal static void SetDpsFromTerminal(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            SetDps(comp, newValue);
        }

            internal static void SetDps(WeaponComponent comp, float newValue, bool isNetworkUpdate = false)
        {
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            comp.Set.Value.DpsModifier = newValue;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];
                if (!w.ActiveAmmoDef.Const.IsBeamWeapon || w.ActiveAmmoDef.Const.MustCharge) continue;

                comp.Session.FutureEvents.Schedule(SetWeaponDPS, w, 0);
            }

            if(!isNetworkUpdate && comp.Session.IsClient)
                comp.UpdateSettingsMP();

            comp.Ai.UpdatePowerSources = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static void SetWeaponDPS(object o)
        {
            var w = o as Weapon;
            if (w == null) return;

            var comp = w.Comp;
            var newBase = w.ActiveAmmoDef.Const.BaseDamage * comp.Set.Value.DpsModifier;

            if (w.ActiveAmmoDef.Const.IsBeamWeapon)
            {
                newBase *= comp.Set.Value.Overload;
            }

            if (newBase < 0)
                newBase = 0;

            w.BaseDamage = newBase;
            var oldRequired = w.RequiredPower;
            var oldHeatPSec = (60f / w.TicksPerShot) * w.HeatPShot * w.System.BarrelsPerShot;

            w.UpdateShotEnergy();
            w.UpdateRequiredPower();

            var mulitplier = (w.ActiveAmmoDef.Const.EnergyAmmo && w.ActiveAmmoDef.Const.BaseDamage > 0) ? w.BaseDamage / w.ActiveAmmoDef.Const.BaseDamage : 1;

            if (w.BaseDamage > w.ActiveAmmoDef.Const.BaseDamage)
                mulitplier *= mulitplier;

            w.HeatPShot = w.System.HeatPerShot * mulitplier;
            w.AreaEffectDmg = w.ActiveAmmoDef.Const.AreaEffectDamage * mulitplier;
            w.DetonateDmg = w.ActiveAmmoDef.Const.DetonationDamage * mulitplier;
            w.RequiredPower *= mulitplier;

            w.TicksPerShot = (uint)(3600f / w.RateOfFire);
            w.TimePerShot = (3600d / w.RateOfFire);

            var oldDps = w.Dps;
            w.Dps = (60f / w.TicksPerShot) * w.BaseDamage * w.System.BarrelsPerShot;

            if (w.ActiveAmmoDef.AreaEffect.AreaEffect != AreaEffectType.Disabled)
            {
                if (w.ActiveAmmoDef.AreaEffect.Detonation.DetonateOnEnd)
                    w.Dps += (w.DetonateDmg / 2) * (w.ActiveAmmoDef.Trajectory.DesiredSpeed > 0
                                      ? w.ActiveAmmoDef.Trajectory.AccelPerSec /
                                        w.ActiveAmmoDef.Trajectory.DesiredSpeed
                                      : 1);
                else
                    w.Dps += (w.AreaEffectDmg / 2) *
                                  (w.ActiveAmmoDef.Trajectory.DesiredSpeed > 0
                                      ? w.ActiveAmmoDef.Trajectory.AccelPerSec /
                                        w.ActiveAmmoDef.Trajectory.DesiredSpeed
                                      : 1);
            }
            
            var heatPShot = (60f / w.TicksPerShot) * w.HeatPShot * w.System.BarrelsPerShot;

            var heatDif = oldHeatPSec - heatPShot;
            var dpsDif = oldDps - w.Dps;
            var powerDif = oldRequired - w.RequiredPower;

            if (w.IsShooting)
                comp.CurrentDps -= dpsDif;

            if(w.DrawingPower)
                comp.Ai.RequestedWeaponsDraw -= powerDif;

            w.ResetPower = true;

            comp.HeatPerSecond -= heatDif;
            comp.MaxRequiredPower -= powerDif;
            comp.OptimalDps -= dpsDif;
            comp.Ai.OptimalDps -= dpsDif;

            w.Timings.ChargeDelayTicks = 0;
        }

        internal static float GetRof(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return 0;
            return comp.Set.Value.RofModifier;
        }

        internal static void SetRof(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            comp.Set.Value.RofModifier = newValue;


            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                if (!w.ActiveAmmoDef.Const.IsBeamWeapon || w.ActiveAmmoDef.Const.MustCharge) continue;

                var newRate = (int)(w.System.RateOfFire * comp.Set.Value.RofModifier);

                if (newRate < 1)
                    newRate = 1;

                w.RateOfFire = newRate;

            }
            SetDps(comp, comp.Set.Value.DpsModifier);
        }

        internal static bool GetOverload(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return false;
            return comp.Set.Value.Overload == 2;
        }

        internal static void SetOverload(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            if (newValue)
                comp.Set.Value.Overload = 2;
            else
                comp.Set.Value.Overload = 1;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                if(comp.Platform.Weapons[i].ActiveAmmoDef.Const.IsBeamWeapon && !comp.Platform.Weapons[i].ActiveAmmoDef.Const.MustCharge)
                    SetDps(comp, comp.Set.Value.DpsModifier);
            }
        }

        internal static float GetRange(IMyTerminalBlock block) {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return 100;
            return comp.Set.Value.Range;
        }

        internal static void SetRange(IMyTerminalBlock block, float range) {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            comp.Set.Value.Range = range;
        }

        internal static float GetMinRange(IMyTerminalBlock block)
        {
            return 0;
        }

        internal static float GetMaxRange(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return 0;

            var maxTrajectory = 0f;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var curMax = comp.Platform.Weapons[i].ActiveAmmoDef.Const.MaxTrajectory;
                if (curMax > maxTrajectory)
                    maxTrajectory = (float)curMax;
            }
            return maxTrajectory;
        }
    }
}
