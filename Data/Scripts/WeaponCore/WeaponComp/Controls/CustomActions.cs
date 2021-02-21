using System.Text;
using Sandbox.ModAPI;
using WeaponCore.Support;
using WeaponCore.Platform;
using static WeaponCore.Support.WeaponComponent.ShootActions;

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

        internal static void TerminalActionControlMode(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;
            
            var numValue = (int)comp.Data.Repo.Base.Set.Overrides.Control;
            var value = numValue + 1 <= 2 ? numValue + 1 : 0;

            WeaponComponent.RequestSetValue(comp, "ControlModes", value, comp.Session.PlayerId);
        }

        internal static void TerminalActionMovementMode(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var numValue = (int)comp.Data.Repo.Base.Set.Overrides.MoveMode;
            var value = numValue + 1 <= 3 ? numValue + 1 : 0;

            WeaponComponent.RequestSetValue(comp, "MovementModes", value, comp.Session.PlayerId);
        }

        internal static void TerminActionCycleSubSystem(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var numValue = (int)comp.Data.Repo.Base.Set.Overrides.SubSystem;
            var value = numValue + 1 <= 7 ? numValue + 1 : 0;

            WeaponComponent.RequestSetValue(comp, "SubSystems", value, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleNeutrals(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Neutrals;
            var newValue = newBool ? 1 : 0;

            WeaponComponent.RequestSetValue(comp, "Neutrals", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleProjectiles(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Projectiles;
            var newValue = newBool ? 1 : 0;

            WeaponComponent.RequestSetValue(comp, "Projectiles", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleBiologicals(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Biologicals;
            var newValue = newBool ? 1 : 0;

            WeaponComponent.RequestSetValue(comp, "Biologicals", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleMeteors(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Meteors;
            var newValue = newBool ? 1 : 0;

            WeaponComponent.RequestSetValue(comp, "Meteors", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleGrids(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Grids;
            var newValue = newBool ? 1 : 0;

            WeaponComponent.RequestSetValue(comp, "Grids", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleFriendly(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Friendly;
            var newValue = newBool ? 1 : 0;

            WeaponComponent.RequestSetValue(comp, "Friendly", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleUnowned(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.Unowned;
            var newValue = newBool ? 1 : 0;

            WeaponComponent.RequestSetValue(comp, "Unowned", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleFocusTargets(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.FocusTargets;
            var newValue = newBool ? 1 : 0;

            WeaponComponent.RequestSetValue(comp, "FocusTargets", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleFocusSubSystem(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Base.Set.Overrides.FocusSubSystem;
            var newValue = newBool ? 1 : 0;

            WeaponComponent.RequestSetValue(comp, "FocusSubSystem", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMaxSizeIncrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Base.Set.Overrides.MaxSize * 2;
            var newValue = nextValue > 0 && nextValue < 16384 ? nextValue : 16384;

            WeaponComponent.RequestSetValue(comp, "MaxSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMaxSizeDecrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Base.Set.Overrides.MaxSize / 2;
            var newValue = nextValue > 0 && nextValue < 16384 ? nextValue : 1;

            WeaponComponent.RequestSetValue(comp, "MaxSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMinSizeIncrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Base.Set.Overrides.MinSize == 0 ? 1 : comp.Data.Repo.Base.Set.Overrides.MinSize * 2;
            var newValue = nextValue > 0 && nextValue < 128 ? nextValue : 128;

            WeaponComponent.RequestSetValue(comp, "MinSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMinSizeDecrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Base.Set.Overrides.MinSize / 2;
            var newValue = nextValue > 0 && nextValue < 128 ? nextValue : 0;

            WeaponComponent.RequestSetValue(comp, "MinSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionCycleAmmo(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp?.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                if (!w.System.HasAmmoSelection)
                    continue;

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

        internal static void GridsWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Base.Set.Overrides.Grids)
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
            sb.Append(comp.Data.Repo.Base.Set.Overrides.Control);
        }

        internal static void MovementModeWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            sb.Append(comp.Data.Repo.Base.Set.Overrides.MoveMode);
        }

        internal static void SubSystemWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            sb.Append(comp.Data.Repo.Base.Set.Overrides.SubSystem);
        }

        internal static void AmmoSelectionWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || comp.AmmoSelectionWeaponIds.Count == 0) return;
            var w = comp.Platform.Weapons[comp.AmmoSelectionWeaponIds[0]];
            sb.Append(w.ActiveAmmoDef.AmmoDef.AmmoRound);
        }
        #endregion
    }
}
