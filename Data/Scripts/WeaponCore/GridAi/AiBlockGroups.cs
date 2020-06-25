using System.Collections.Generic;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Support.WeaponComponent;

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

        internal void RequestApplySettings(GridAi ai, string setting, int value, Session session)
        {
            if (session.IsServer)
            {
                Log.Line($"RequestApplySettings: Group:{Name} - setting:{setting} - value:{value}");
                Settings[setting] = value;
                ApplySettings();
            }
            else if (session.IsClient)
            {
                Log.Line($"RequestApplySettings: Group:{Name} - setting:{setting} - value:{value}");
                session.SendOverRidesClientAi(ai, Name, setting, value);
            }
        }

        internal void RequestSetValue(WeaponComponent comp, string setting, int value)
        {
            if (comp.Session.IsServer)
            {
                Log.Line($"RequestSetValue: Group:{Name} - setting:{setting} - value:{value}");
                SetValue(comp, setting, value);
            }
            else if (comp.Session.IsClient)
            {
                Log.Line($"RequestSetValue: Group:{Name} - setting:{setting} - value:{value}");
                comp.Session.SendOverRidesClientComp(comp, Name, setting, value);
            }
        }

        internal void ApplySettings()
        {
            foreach (var comp in Comps) {

                var o = comp.Set.Value.Overrides;
                var change = false;

                foreach (var setting in Settings) {

                    var v = setting.Value;
                    var enabled = v > 0;
                    switch (setting.Key) {
                        case "Active":
                            if (!change && o.Activate != enabled) change = true;
                            o.Activate = enabled;
                            if (!comp.Session.IsClient && !o.Activate) ClearTargets(comp);
                            break;
                        case "SubSystems":
                            var blockType = (TargetingDef.BlockTypes)v;
                            if (!change && o.SubSystem != blockType) change = true;
                            o.SubSystem = blockType;
                            break;
                        case "FocusSubSystem":
                            if (!change && o.FocusSubSystem != enabled) change = true;
                            o.FocusSubSystem = enabled;
                            break;
                        case "FocusTargets":
                            if (!change && o.FocusTargets != enabled) change = true;
                            o.FocusTargets = enabled;
                            break;
                        case "ManualControl":
                            if (!change && o.ManualControl != enabled) change = true;
                            o.ManualControl = enabled;
                            break;
                        case "TargetPainter":
                            if (!change && o.TargetPainter != enabled) change = true;
                            o.TargetPainter = enabled;
                            break;
                        case "Unowned":
                            if (!change && o.Unowned != enabled) change = true;
                            o.Unowned = enabled;
                            break;
                        case "Friendly":
                            if (!change && o.Friendly != enabled) change = true;
                            o.Friendly = enabled;
                            break;
                        case "Meteors":
                            if (!change && o.Meteors != enabled) change = true;
                            o.Meteors = enabled;
                            break;
                        case "Biologicals":
                            if (!change && o.Biologicals != enabled) change = true;
                            o.Biologicals = enabled;
                            break;
                        case "Projectiles":
                            if (!change && o.Projectiles != enabled) change = true;
                            o.Projectiles = enabled;
                            break;
                        case "Neutrals":
                            if (!change && o.Neutrals != enabled) change = true;
                            o.Neutrals = enabled;
                            break;
                    }
                }

                if (change) {
                    Log.Line($"ApplySettings change detected");
                    ResetCompState(comp, true);
                    if (comp.Session.MpActive)
                    {
                        comp.Session.SendCompStateUpdate(comp);
                        comp.Session.SendOverRidesServerAi(comp.Ai, Name, o);
                    }
                }
            }
        }

        internal void SetValue(WeaponComponent comp, string setting, int v)
        {
            var o = comp.Set.Value.Overrides;
            var enabled = v > 0;
            switch (setting) {

                case "Active":
                    o.Activate = enabled;
                    if (!comp.Session.IsClient && !o.Activate) ClearTargets(comp);
                    break;
                case "SubSystems":
                    o.SubSystem = (TargetingDef.BlockTypes)v;
                    break;
                case "FocusSubSystem":
                    o.FocusSubSystem = enabled;
                    break;
                case "FocusTargets":
                    o.FocusTargets = enabled;
                    break;
                case "ManualControl":
                    o.ManualControl = enabled;
                    break;
                case "TargetPainter":
                    o.TargetPainter = enabled;
                    break;
                case "Unowned":
                    o.Unowned = enabled;
                    break;
                case "Friendly":
                    o.Friendly = enabled;
                    break;
                case "Meteors":
                    o.Meteors = enabled;
                    break;
                case "Biologicals":
                    o.Biologicals = enabled;
                    break;
                case "Projectiles":
                    o.Projectiles = enabled;
                    break;
                case "Neutrals":
                    o.Neutrals = enabled;
                    break;
            }

            ResetCompState(comp, false);

            if (comp.Session.MpActive) {
                comp.Session.SendCompStateUpdate(comp);
            }
        }

        internal int GetCompSetting(string setting, WeaponComponent comp)
        {
            var value = 0;
            var o = comp.Set.Value.Overrides;
            switch (setting) {

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

        internal void ResetCompState(WeaponComponent comp, bool apply)
        {
            var o = comp.Set.Value.Overrides;
            var userControl = o.ManualControl || o.TargetPainter;

            if (userControl)
            {
                comp.State.Value.CurrentPlayerControl.PlayerId = comp.Session.PlayerId;
                comp.State.Value.CurrentPlayerControl.ControlType = ControlType.Ui;

                if (o.ManualControl) {
                    o.TargetPainter = false;
                    if (apply) Settings["TargetPainter"] = 0;
                }
                else {
                    o.ManualControl = false;
                    if (apply) Settings["ManualControl"] = 0;
                }

                comp.State.Value.ClickShoot = false;
                comp.State.Value.ShootOn = false;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    comp.Platform.Weapons[i].State.ManualShoot = ShootActions.ShootOff;
            }
            else {
                comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
            }
        }

        private void ClearTargets(WeaponComponent comp)
        {
            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {

                var weapon = comp.Platform.Weapons[i];
                if (weapon.Target.HasTarget)
                    comp.Platform.Weapons[i].Target.Reset(comp.Session.Tick, Target.States.Expired);
            }
        }
    }
}
