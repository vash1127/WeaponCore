using System.Collections.Generic;

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

        internal void ApplySettings()
        {
            Log.Line($"apply group");
            foreach (var comp in Comps)
            {
                var o = comp.Set.Value.Overrides;
                foreach (var setting in Settings)
                {
                    switch (setting.Key)
                    {
                        case "Active":
                            o.Activate = setting.Value > 0;
                            break;
                        case "SubSystems":
                            o.SubSystem = (TargetingDefinition.BlockTypes)setting.Value;
                            break;
                        case "FocusSubSystem":
                            o.FocusSubSystem = setting.Value > 0;
                            break;
                        case "FocusTargets":
                            o.FocusTargets = setting.Value > 0;
                            break;
                        case "ManualControl":
                            o.ManaulControl = setting.Value > 0;
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
            }
        }

        internal void SetValue(WeaponComponent comp, string setting, int value)
        {
            Log.Line($"apply weapon");
            var o = comp.Set.Value.Overrides;
            switch (setting)
            {
                case "Active":
                    o.Activate = value > 0;
                    break;
                case "SubSystems":
                    o.SubSystem = (TargetingDefinition.BlockTypes)value;
                    break;
                case "FocusSubSystem":
                    o.FocusSubSystem = value > 0;
                    break;
                case "FocusTargets":
                    o.FocusTargets = value > 0;
                    break;
                case "ManualControl":
                    o.ManaulControl = value > 0;
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
                    value = o.ManaulControl ? 1 : 0;
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
    }
}
