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

            internal static void SetDps(WeaponComponent comp, float newValue, bool isNetworkUpdate = false, bool ammoChange = false)
        {
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            comp.Set.Value.DpsModifier = newValue;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];
                if (!ammoChange && (!w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon || w.ActiveAmmoDef.AmmoDef.Const.MustCharge)) continue;

                comp.Session.FutureEvents.Schedule(w.SetWeaponDps, null, 1);
            }

            if (!isNetworkUpdate && comp.Session.HandlesInput)
                comp.Session.SendCompSettingUpdate(comp);

            comp.Ai.UpdatePowerSources = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
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

                if (!w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon || w.ActiveAmmoDef.AmmoDef.Const.MustCharge) continue;

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
                if(comp.Platform.Weapons[i].ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && !comp.Platform.Weapons[i].ActiveAmmoDef.AmmoDef.Const.MustCharge)
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
            foreach (var w in comp.Platform.Weapons)
                w.UpdateWeaponRange();
        }

        internal static bool ShowRange(IMyTerminalBlock block, int notUsed)
        {
            return true;
            var comp = block?.Components?.Get<WeaponComponent>();

            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return true;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var weapon = comp.Platform.Weapons[i];
                if (weapon.TrackTarget) return true;
            }

            return false;
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
                var curMax = comp.Platform.Weapons[i].GetMaxWeaponRange();
                if (curMax > maxTrajectory)
                    maxTrajectory = (float)curMax;
            }
            return maxTrajectory;
        }
    }
}
