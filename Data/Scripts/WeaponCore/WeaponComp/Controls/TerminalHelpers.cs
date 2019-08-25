using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WepaonCore.Control
{
    public static class TerminalHelpers
    {
        internal static bool HideControls<T>() where T : IMyLargeTurretBase
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            for (int i = 0; i < controls.Count; i++)
            {
                var c = controls[i];
                //Log.Line($"Count: {i} ID:{c.Id}");
                if ((i > 6 && i < 10) || (i> 12 && i <22))
                    c.Visible = tb => false;
            }
            return false;
        }

        internal static bool HideActions<T>() where T : IMyLargeTurretBase
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);
            for (int i = 0; i < actions.Count; i++)
            {
                var c = actions[i];
                //Log.Line($"Count: {i} ID:{c.Id}");
                if ((i > 2 && i < 6) || (i > 10 && i < 37))
                    c.Enabled = tb => false;
            }
            return false;
        }
        
        internal static void AlterShootActions<T>() where T : IMyLargeTurretBase
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);

            for (int i = 0; i < actions.Count; i++)
            {
                var c = actions[i];
                switch (c.Id)
                {
                    case "ShootOnce":
                    case "Shoot":
                    case "Shoot_On":
                    case "Shoot_Off":
                        actions[i].Action += delegate (IMyTerminalBlock blck)
                        {
                            var comp = blck?.Components?.Get<WeaponComponent>();
                            if (comp == null) return;

                            if (c.Id == "Shoot_Off")
                                comp.ManualShoot = WeaponComponent.TerminalActionState.ShootOff;
                            else if (c.Id == "ShootOnce")
                                comp.ManualShoot = WeaponComponent.TerminalActionState.ShootOnce;
                            else
                                comp.ManualShoot = WeaponComponent.TerminalActionState.ShootOn;
                        };
                        break;
                }
            }
        }

        internal static bool UpdateControls<T>(T block) where T : IMyTerminalBlock
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            var count = 0;
            for (int i = 0; i < controls.Count; i++)
            {
                var c = controls[i];
                if (c.Id.StartsWith("WC-WNAME"))
                {
                    var comp = block.Components.Get<WeaponComponent>();
                    if (comp.Platform.Weapons.Length < count) ((IMyTerminalControlOnOffSwitch) c).Title = MyStringId.GetOrCompute($"Enable {comp.Platform.Weapons[count++].System.WeaponName}");
                    c.RedrawControl();
                }

            }
            return false;
        }

        internal static bool ShowDisplayControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp == null;
        }

        internal static bool WeaponCount(Func<IMyTerminalBlock, int, int, bool> getter, int id, IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && getter(block, comp.Platform.Weapons.Length, id);
        }

        internal static IMyTerminalControlOnOffSwitch AddOnOff<T>(int id, string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"WC - WNAME_{name}");
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(onText);
            c.OffText = MyStringId.GetOrCompute(offText);
            c.Enabled = x => WeaponCount(enabledGetter, id, x);
            c.Visible = x => WeaponCount(enabledGetter, id, x);
            c.Getter = getter;
            c.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            CreateActionSet<T>(c, name);

            return c;
        }

        internal static IMyTerminalControlButton AddButton<T>(T block, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>(name);
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Visible = visibleGetter ?? d;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            return c;
        }

        internal static IMyTerminalControlSeparator Separator<T>(int id, string name, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(name);
            var d = GetDefaultEnabled();

            c.Enabled = x => WeaponCount(enabledGetter, id, x);
            c.Visible = x => WeaponCount(enabledGetter, id, x);
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            return c;
        }

        internal static IMyTerminalControlColor AddColorEditor<T>(T block, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, Color> getter, Action<IMyTerminalBlock, Color> setter, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, T>(name);
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = x => WeaponCount(enabledGetter, id, x);
            c.Visible = x => WeaponCount(enabledGetter, id, x);
            c.Getter = getter;
            c.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddSlider<T>(T block, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var s = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);
            var d = GetDefaultEnabled();

            s.Title = MyStringId.GetOrCompute(title);
            s.Tooltip = MyStringId.GetOrCompute(tooltip);
            s.Enabled = x => WeaponCount(enabledGetter, id, x);
            s.Visible = x => WeaponCount(enabledGetter, id, x);
            s.Getter = getter;
            s.Setter = setter;
            s.Writer = (b, v) => v.Append(getter(b).ToString("N2"));
            MyAPIGateway.TerminalControls.AddControl<T>(s);
            return s;
        }

        internal static IMyTerminalControlCombobox AddCombobox<T>(T block, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter, Action<List<MyTerminalControlComboBoxItem>> fillAction, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null) where T : IMyTerminalBlock
        {
            var cmb = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, T>(name);
            var d = GetDefaultEnabled();

            cmb.Title = MyStringId.GetOrCompute(title);
            cmb.Tooltip = MyStringId.GetOrCompute(tooltip);
            cmb.Enabled = x => WeaponCount(enabledGetter, id, x);
            cmb.Visible = x => WeaponCount(enabledGetter, id, x);
            cmb.ComboBoxContent = fillAction;
            cmb.Getter = getter;
            cmb.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(cmb);
            return cmb;
        }

        internal static void CreateActionSet<T>(IMyTerminalControlOnOffSwitch tc, string name) where T : IMyTerminalBlock
        {
            var noSpName = name.Replace(" ","");
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"{noSpName}Toggle");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"{name} Toggle On/Off");
            action.Action = (b) => tc.Setter(b, !tc.Getter(b));
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => tc.Enabled(b);

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"{noSpName}Toggle_On");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"{name} On");
            action.Action = (b) => tc.Setter(b, true);
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => tc.Enabled(b);

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"{noSpName}Toggle_Off");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action.Name = new StringBuilder($"{name} Off");
            action.Action = (b) => tc.Setter(b, true);
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => tc.Enabled(b);

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }
        /*
        internal static IMyTerminalControl[] AddVectorEditor<T>(T block, string name, string title, string tooltip, Func<IMyTerminalBlock, Vector3> getter, Action<IMyTerminalBlock, Vector3> setter, float min = -10, float max = 10, Func<IMyTerminalBlock, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, bool> enabledGetter = null, string writerFormat = "0.##") where T : IMyTerminalBlock
        {
            var controls = new IMyTerminalControl[4];

            var d = GetDefaultEnabled();

            var lb = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, T>(name + "_Label");
            lb.Label = MyStringId.GetOrCompute(title);
            lb.Enabled = enabledGetter ?? d;
            lb.Visible = visibleGetter ?? d;
            MyAPIGateway.TerminalControls.AddControl<T>(lb);
            controls[0] = lb;

            var x = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name + "_X");
            x.Title = MyStringId.GetOrCompute("X");
            x.Tooltip = MyStringId.GetOrCompute(tooltip);
            x.Writer = (b, s) => s.Append(getter(b).X.ToString(writerFormat));
            x.Getter = b => getter(b).X;
            x.Setter = (b, v) =>
            {
                var vc = getter(b);
                vc.X = v;
                setter(b, vc);
            };
            x.Enabled = enabledGetter ?? d;
            x.Visible = visibleGetter ?? d;
            x.SetLimits(min, max);
            MyAPIGateway.TerminalControls.AddControl<T>(x);
            controls[1] = x;

            var y = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name + "_Y");
            y.Title = MyStringId.GetOrCompute("Y");
            y.Tooltip = MyStringId.GetOrCompute(tooltip);
            y.Writer = (b, s) => s.Append(getter(b).Y.ToString(writerFormat));
            y.Getter = b => getter(b).Y;
            y.Setter = (b, v) =>
            {
                var vc = getter(b);
                vc.Y = v;
                setter(b, vc);
            };
            y.Enabled = enabledGetter ?? d;
            y.Visible = visibleGetter ?? d;
            y.SetLimits(min, max);
            MyAPIGateway.TerminalControls.AddControl<T>(y);
            controls[2] = y;

            var z = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name + "_Z");
            z.Title = MyStringId.GetOrCompute("Z");
            z.Tooltip = MyStringId.GetOrCompute(tooltip);
            z.Writer = (b, s) => s.Append(getter(b).Z.ToString(writerFormat));
            z.Getter = b => getter(b).Z;
            z.Setter = (b, v) =>
            {
                var vc = getter(b);
                vc.Z = v;
                setter(b, vc);
            };
            z.Enabled = enabledGetter ?? d;
            z.Visible = visibleGetter ?? d;
            z.SetLimits(min, max);
            MyAPIGateway.TerminalControls.AddControl<T>(z);
            controls[3] = z;

            return controls;
        }
        */
        internal static IMyTerminalControlCheckbox AddCheckbox<T>(T block, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>(name);
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = x => WeaponCount(enabledGetter, id, x);
            c.Enabled = x => WeaponCount(enabledGetter, id, x);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            return c;
        }

        private static Func<IMyTerminalBlock, bool> GetDefaultEnabled()
        {
            return b => b.BlockDefinition.SubtypeId.StartsWith("DSControl");
        }
    }
}
