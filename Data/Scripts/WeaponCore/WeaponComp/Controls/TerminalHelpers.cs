using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using WeaponCore;

namespace WepaonCore.Control
{
    public static class TerminalHelpers
    {
        internal static bool AlterActions<T>() where T : IMyLargeTurretBase
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);
            for (int i = 0; i < actions.Count; i++)
            {
                var c = actions[i];
                //Log.Line($"Count: {i} ID:{c.Id}");
                if (c.Id != "Control" && !c.Id.Contains("OnOff"))
                    c.Enabled = b => !WepUi.IsCoreWeapon(b,0,0);
            }
            return false;
        }

        internal static bool AlterControls<T>() where T : IMyLargeTurretBase
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            for (int i = 0; i < controls.Count; i++)
            {
                var c = controls[i];
                Log.Line($"Count: {i} ID:{c.Id}");
                if ((i > 6 && i < 10) || i > 11 )
                    c.Visible = b => !WepUi.IsCoreWeapon(b, 0, 0);
            }
            return false;
        }

        internal static IMyTerminalControlOnOffSwitch AddWeaponOnOff<T>(int id, string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"WC_{id}");
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(onText);
            c.OffText = MyStringId.GetOrCompute(offText);
            c.Enabled = x => true;
            c.Visible = x => WeaponFunctionEnabled(id, x);
            c.Getter = getter;
            c.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            CreateOnOffActionSet<T>(c, name, id);

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

            c.Enabled = x => true;
            c.Visible = x => true;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            return c;
        }

        internal static IMyTerminalControlColor AddColorEditor<T>(T block, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, Color> getter, Action<IMyTerminalBlock, Color> setter, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, T>(name);
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = x => true;
            c.Visible = x => true;
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
            s.Enabled = x => true;
            s.Visible = x => true;
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
            cmb.Enabled = x => true;
            cmb.Visible = x => true;
            cmb.ComboBoxContent = fillAction;
            cmb.Getter = getter;
            cmb.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(cmb);
            return cmb;
        }

        internal static void CreateOnOffActionSet<T>(IMyTerminalControlOnOffSwitch tc, string name, int id) where T : IMyTerminalBlock
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"{name} Toggle On/Off");
            action.Action = (b) => tc.Setter(b, !tc.Getter(b));
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => WeaponFunctionEnabled(id, b);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_On");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"{name} On");
            action.Action = (b) => tc.Setter(b, true);
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => WeaponFunctionEnabled(id, b);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_Off");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action.Name = new StringBuilder($"{name} Off");
            action.Action = (b) => tc.Setter(b, true);
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => WeaponFunctionEnabled(id, b);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        internal static bool WeaponFunctionEnabled(int id, IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return false;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                if (comp.Platform.Weapons[i].System.WeaponID == id)
                    return true;
            }
            return false;
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
            c.Visible = x => true;
            c.Enabled = x => true;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            return c;
        }

        private static Func<IMyTerminalBlock, bool> GetDefaultEnabled()
        {
            return b => b.BlockDefinition.SubtypeId.StartsWith("DSControl");
        }
    }
}
