using System.Collections.Generic;
using static WeaponCore.Support.WeaponDefinition;
namespace WeaponCore.Support
{
    internal class GroupInfo
    {
        internal readonly HashSet<WeaponComponent> Comps = new HashSet<WeaponComponent>();

        internal readonly Dictionary<string, int> Settings = new Dictionary<string, int>()
        {
            {"Active", 1},
            {"Neutrals", 0},
            {"Projectiles", 0 },
            {"Biologicals", 0 },
            {"Meteors", 0 },
            {"Friendly", 0},
            {"Unowned", 0},
            {"TargetPainter", 0},
            {"ManualControl", 0},
            {"FocusTargets", 0},
            {"FocusSubSystem", 0},
            {"SubSystems", 0},
        };

        internal string Name;
        internal ChangeStates ChangeState;
        internal enum ChangeStates
        {
            None,
            Add,
            Modify
        }

        internal void ApplySettings(string BlockGroup)
        {
            GroupOverrides o = null;
            foreach (var comp in Comps)
            {
                o = comp.Set.Value.Overrides;
                foreach (var setting in Settings)
                {
                    switch (setting.Key)
                    {
                        case "Active":
                            o.Activate = setting.Value > 0;
                            if (!o.Activate) ClearTargets(comp);
                            break;
                        case "SubSystems":
                            o.SubSystem = (TargetingDef.BlockTypes)setting.Value;
                            break;
                        case "FocusSubSystem":
                            o.FocusSubSystem = setting.Value > 0;
                            break;
                        case "FocusTargets":
                            o.FocusTargets = setting.Value > 0;
                            break;
                        case "ManualControl":
                            o.ManualControl = setting.Value > 0;
                            break;
                        case "TargetPainter":
                            o.TargetPainter = setting.Value > 0;
                            break;
                        case "Unowned":
                            o.Unowned = setting.Value > 0;
                            break;
                        case "Friendly":
                            o.Friendly = setting.Value > 0;
                            break;
                        case "Meteors":
                            o.Meteors = setting.Value > 0;
                            break;
                        case "Biologicals":
                            o.Biologicals = setting.Value > 0;
                            break;
                        case "Projectiles":
                            o.Projectiles = setting.Value > 0;
                            break;
                        case "Neutrals":
                            o.Neutrals = setting.Value > 0;
                            break;
                    }
                }

                if (o.ManualControl || o.TargetPainter)
                {
                    comp.State.Value.CurrentPlayerControl.PlayerId = comp.Session.PlayerId;
                    comp.State.Value.CurrentPlayerControl.ControlType = ControlType.Ui;

                    if (o.ManualControl)
                        o.TargetPainter = false;
                    else
                        o.ManualControl = false;

                    comp.State.Value.ClickShoot = false;
                    comp.State.Value.ShootOn = false;
                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                        comp.Platform.Weapons[i].State.ManualShoot = Platform.Weapon.TerminalActionState.ShootOff;
                }
                else
                {
                    comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                    comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
                }

                if(comp.Session.MpActive)
                    comp.SendControlingPlayer();
            }

            var gridAi = Comps?.FirstElement()?.Ai;

            if (gridAi != null && gridAi.Session.HandlesInput && gridAi.Session.MpActive && o != null)
                gridAi.SendOverRides(BlockGroup, o);
        }

        internal void SetValue(WeaponComponent comp, string setting, int value)
        {
            var o = comp.Set.Value.Overrides;
            switch (setting)
            {
                case "Active":
                    o.Activate = value > 0;
                    if (!o.Activate) ClearTargets(comp);
                    break;
                case "SubSystems":
                    o.SubSystem = (TargetingDef.BlockTypes)value;
                    break;
                case "FocusSubSystem":
                    o.FocusSubSystem = value > 0;
                    break;
                case "FocusTargets":
                    o.FocusTargets = value > 0;
                    break;
                case "ManualControl":
                    o.ManualControl = value > 0;
                    break;
                case "TargetPainter":
                    o.TargetPainter = value > 0;
                    break;
                case "Unowned":
                    o.Unowned = value > 0;
                    break;
                case "Friendly":
                    o.Friendly = value > 0;
                    break;
                case "Meteors":
                    o.Meteors = value > 0;
                    break;
                case "Biologicals":
                    o.Biologicals = value > 0;
                    break;
                case "Projectiles":
                    o.Projectiles = value > 0;
                    break;
                case "Neutrals":
                    o.Neutrals = value > 0;
                    break;
            }

            if (o.ManualControl || o.TargetPainter)
            {
                comp.State.Value.CurrentPlayerControl.PlayerId = comp.Session.PlayerId;
                comp.State.Value.CurrentPlayerControl.ControlType = ControlType.Ui;

                if (o.ManualControl)
                    o.TargetPainter = false;
                else
                    o.ManualControl = false;

                comp.State.Value.ClickShoot = false;
                comp.State.Value.ShootOn = false;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    comp.Platform.Weapons[i].State.ManualShoot = Platform.Weapon.TerminalActionState.ShootOff;
            }
            else
            {
                comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
            }
            
            comp.SendControlingPlayer();
            comp.SendOverRides();
        }

        internal int GetCompSetting(string setting, WeaponComponent comp)
        {
            var value = 0;
            var o = comp.Set.Value.Overrides;
            switch (setting)
            {
                case "Active":
                    value = o.Activate ? 1 : 0;
                    break;
                case "SubSystems":
                    value = (int)o.SubSystem;
                    break;
                case "FocusSubSystem":
                    value = o.FocusSubSystem ? 1 : 0;
                    break;
                case "FocusTargets":
                    value = o.FocusTargets ? 1 : 0;
                    break;
                case "ManaulControl":
                    value = o.ManualControl ? 1 : 0;
                    break;
                case "TargetPainter":
                    value = o.TargetPainter ? 1 : 0;
                    break;
                case "Unowned":
                    value = o.Unowned ? 1 : 0;
                    break;
                case "Friendly":
                    value = o.Friendly ? 1 : 0;
                    break;
                case "Meteors":
                    value = o.Meteors ? 1 : 0;
                    break;
                case "Biologicals":
                    value = o.Biologicals ? 1 : 0;
                    break;
                case "Projectiles":
                    value = o.Projectiles ? 1 : 0;
                    break;
                case "Neutrals":
                    value = o.Neutrals ? 1 : 0;
                    break;
            }
            return value;
        }

        private void ClearTargets(WeaponComponent comp)
        {
            if (comp.Session.IsClient) return;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var weapon = comp.Platform.Weapons[i];
                if (weapon.Target.HasTarget)
                    comp.Platform.Weapons[i].Target.Reset(comp.Session.Tick, Target.States.Expired);
            }
        }
    }
}
