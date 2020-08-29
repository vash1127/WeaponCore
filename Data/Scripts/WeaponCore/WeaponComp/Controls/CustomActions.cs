using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using WeaponCore.Platform;
using static WeaponCore.Support.WeaponComponent.ShootActions;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef.EventTriggers;

namespace WeaponCore.Control
{
    public static class CustomActions
    {
        #region Call Actions
        internal static void TerminalActionShootClick(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            comp.RequestShootUpdate(ShootClick, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminActionToggleShoot(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            comp.RequestShootUpdate(ShootOn, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminalActionShootOn(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            comp.RequestShootUpdate(ShootOn, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminalActionShootOff(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            comp.RequestShootUpdate(ShootOff, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminalActionShootOnce(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            comp.RequestShootUpdate(ShootOnce, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminActionControlMode(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var manual = comp.Data.Repo.Base.Set.Overrides.ManualControl;
            var painter = comp.Data.Repo.Base.Set.Overrides.TargetPainter;
            var auto = !manual && !painter;

            var newMode = auto ? "ManualControl" : "Painter";

            var newBool = auto || manual;
            var value = newBool ? 1 : 0;

            GroupInfo.SetValue(comp, newMode, value, comp.Session.PlayerId);
        }

        internal static void TerminalActionCycleAmmo(IMyTerminalBlock blk, int id)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            int weaponId;
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId) || comp.Platform.Weapons[weaponId].System.WeaponIdHash != id)
                return;

            var w = comp.Platform.Weapons[weaponId];

            var availAmmo = w.System.AmmoTypes.Length;
            var currActive = w.System.AmmoTypes[w.Ammo.AmmoTypeId];
            var next = (w.Ammo.AmmoTypeId + 1) % availAmmo;
            var currDef = w.System.AmmoTypes[next];

            var change = false;

            while (!(currActive.Equals(currDef)))
            {
                if (currDef.AmmoDef.Const.IsTurretSelectable)
                {
                    change = true;
                    break;
                }

                next = (next + 1) % availAmmo;
                currDef = w.System.AmmoTypes[next];
            }

            if (change)
                w.ChangeAmmo(next);
        }

        internal static void TerminActionCycleSubSystem(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var numValue = (int)comp.Data.Repo.Base.Set.Overrides.SubSystem;
            var value = numValue + 1 <= 7 ? numValue + 1 : 0;

            GroupInfo.SetValue(comp, "SubSystems", value, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleNeutrals(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Neutrals;
            var newValue = newBool ? 1 : 0;

            GroupInfo.SetValue(comp, "Neutrals", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleProjectiles(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Neutrals;
            var newValue = newBool ? 1 : 0;

            GroupInfo.SetValue(comp, "Projectiles", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleBiologicals(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Biologicals;
            var newValue = newBool ? 1 : 0;

            GroupInfo.SetValue(comp, "Biologicals", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleMeteors(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Meteors;
            var newValue = newBool ? 1 : 0;

            GroupInfo.SetValue(comp, "Meteors", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleFriendly(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Friendly;
            var newValue = newBool ? 1 : 0;

            GroupInfo.SetValue(comp, "Friendly", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleUnowned(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Unowned;
            var newValue = newBool ? 1 : 0;

            GroupInfo.SetValue(comp, "Unowned", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleFocusTargets(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.FocusTargets;
            var newValue = newBool ? 1 : 0;

            GroupInfo.SetValue(comp, "FocusTargets", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleFocusSubSystem(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.FocusSubSystem;
            var newValue = newBool ? 1 : 0;

            GroupInfo.SetValue(comp, "FocusSubSystem", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMaxSizeIncrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Base.Set.Overrides.MaxSize * 2;
            var newValue = nextValue > 0 && nextValue < 16384 ? nextValue : 16384;

            GroupInfo.SetValue(comp, "MaxSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMaxSizeDecrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Base.Set.Overrides.MaxSize / 2;
            var newValue = nextValue > 0 && nextValue < 16384 ? nextValue : 1;

            GroupInfo.SetValue(comp, "MaxSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMinSizeIncrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Base.Set.Overrides.MinSize == 0 ? 1 : comp.Data.Repo.Base.Set.Overrides.MinSize * 2;
            var newValue = nextValue > 0 && nextValue < 128 ? nextValue : 128;

            GroupInfo.SetValue(comp, "MinSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMinSizeDecrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Base.Set.Overrides.MinSize / 2;
            var newValue = nextValue > 0 && nextValue < 128 ? nextValue : 0;

            GroupInfo.SetValue(comp, "MinSize", newValue, comp.Session.PlayerId);
        }
        #endregion


        #region Writters
        internal static void ClickShootWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var on = blk.Components.Get<WeaponComponent>()?.Data.Repo?.Base.State.TerminalAction == ShootClick;

            if (on)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void ShootStateWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.State.TerminalAction == ShootOn)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void NeutralWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.Neutrals)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void ProjectilesWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.Projectiles)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void BiologicalsWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.Biologicals)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void MeteorsWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.Meteors)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void FriendlyWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.Friendly)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void UnownedWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.Unowned)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void FocusTargetsWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.FocusTargets)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void FocusSubSystemWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.FocusSubSystem)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void MaxSizeWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            sb.Append(comp.Data.Repo.Base.Set.Overrides.MaxSize);
        }

        internal static void MinSizeWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            sb.Append(comp.Data.Repo.Base.Set.Overrides.MinSize);
        }

        internal static void ControlStateWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.ManualControl)
                sb.Append("Manual");
            else if (comp.Data.Repo.Base.Set.Overrides.TargetPainter)
                sb.Append("Painter");
            else
                sb.Append("Auto");
        }

        internal static void SubSystemWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            var numValue = (int)comp.Data.Repo.Base.Set.Overrides.SubSystem;
            var newValue = numValue + 1 <= 7 ? numValue + 1 : 0;
            sb.Append((WeaponDefinition.TargetingDef.BlockTypes)newValue);
        }
        #endregion

        internal static bool CompReady(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready;
        }
    }
}
