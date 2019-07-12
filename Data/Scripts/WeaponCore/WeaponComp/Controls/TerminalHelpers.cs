using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore;
using WeaponCore.Support;

namespace WepaonCore.Control
{
    public static class TerminalHelpers
    {
        internal static void Setter(Action<IMyTerminalBlock, int, bool> setter, IMyTerminalBlock block, bool value, int weaponId)
        {
            setter(block, weaponId, value);
        }

        internal static bool Getter(Func<IMyTerminalBlock, int, bool> getter, IMyTerminalBlock block, int weaponId)
        {
            return getter(block, weaponId);
        }

        internal static bool HideButton<T>(T block)
        {
            Log.Line("hide buttons");
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            var startIndex = -1;
            var sep1 = -1;
            //var sep2 = -1;
            var shield = -1;
            for (int i = 0; i < controls.Count; i++)
            {
                var c = controls[i];
                Log.Line($"name:{c.Id} - spot:{i}");
                if (i > 6 && i < 11 || i ==  13)
                {
                    c.Visible = ShowDisplayControl;
                }
            }
            //controls.Move(sep1, startIndex + 1);
            //controls.Move(shield, startIndex + 2);
            //controls.Move(sep2, startIndex + 3);
            return false;
        }

        internal static bool ShowDisplayControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp == null;
        }

        internal static IMyTerminalControlOnOffSwitch AddOnOff<T>(T block, int weaponId, string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, int, bool> getter, Action<IMyTerminalBlock, int, bool> setter, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null)
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyTerminalBlock>(name);
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(onText);
            c.OffText = MyStringId.GetOrCompute(offText);
            c.Enabled = enabledGetter ?? d;
            c.Visible = visibleGetter ?? d;
            c.Setter = (x, v) => Setter(setter, x, v, weaponId);
            c.Getter = x => Getter(getter, x, weaponId);
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(c);

            return c;
        }


        internal static IMyTerminalControlButton AddButton<T>(T block, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>(name);
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Visible = visibleGetter ?? d;

            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(c);
            return c;
        }

        internal static IMyTerminalControlSeparator Separator<T>(T block, string name, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null)
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyTerminalBlock>(name);
            var d = GetDefaultEnabled();

            c.Enabled = enabledGetter ?? d;
            c.Visible = visibleGetter ?? d;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(c);

            return c;
        }

        internal static IMyTerminalControlColor AddColorEditor<T>(T block, string name, string title, string tooltip, Func<IMyTerminalBlock, Color> getter, Action<IMyTerminalBlock, Color> setter, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyTerminalBlock>(name);
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = enabledGetter ?? d;
            c.Visible = visibleGetter ?? d;
            c.Getter = getter;
            c.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddSlider<T>(T block, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null)
        {
            var s = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTerminalBlock>(name);
            var d = GetDefaultEnabled();

            s.Title = MyStringId.GetOrCompute(title);
            s.Tooltip = MyStringId.GetOrCompute(tooltip);
            s.Enabled = enabledGetter ?? d;
            s.Visible = visibleGetter ?? d;
            s.Getter = getter;
            s.Setter = setter;
            s.Writer = (b, v) => v.Append(getter(b).ToString("N2"));
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(s);
            return s;
        }

        internal static IMyTerminalControlCombobox AddCombobox<T>(T block, string name, string title, string tooltip, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter, Action<List<MyTerminalControlComboBoxItem>> fillAction, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null)
        {
            var cmb = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyTerminalBlock>(name);
            var d = GetDefaultEnabled();

            cmb.Title = MyStringId.GetOrCompute(title);
            cmb.Tooltip = MyStringId.GetOrCompute(tooltip);
            cmb.Enabled = enabledGetter ?? d;
            cmb.Visible = visibleGetter ?? d;
            cmb.ComboBoxContent = fillAction;
            cmb.Getter = getter;
            cmb.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(cmb);
            return cmb;
        }

        internal static IMyTerminalControl[] AddVectorEditor<T>(T block, string name, string title, string tooltip, Func<IMyTerminalBlock, Vector3> getter, Action<IMyTerminalBlock, Vector3> setter, float min = -10, float max = 10, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null, string writerFormat = "0.##") where T : IMyTerminalBlock
        {
            var controls = new IMyTerminalControl[4];

            var d = GetDefaultEnabled();

            var lb = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyTerminalBlock>(name + "_Label");
            lb.Label = MyStringId.GetOrCompute(title);
            lb.Enabled = enabledGetter ?? d;
            lb.Visible = visibleGetter ?? d;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(lb);
            controls[0] = lb;

            var x = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTerminalBlock>(name + "_X");
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
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(x);
            controls[1] = x;

            var y = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTerminalBlock>(name + "_Y");
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
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(y);
            controls[2] = y;

            var z = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTerminalBlock>(name + "_Z");
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
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(z);
            controls[3] = z;

            return controls;
        }

        internal static IMyTerminalControlCheckbox AddCheckbox<T>(T block, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null)
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>(name);
            var d = GetDefaultEnabled();

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = visibleGetter ?? d;
            c.Enabled = enabledGetter ?? d;

            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(c);
            return c;
        }

        private static Func<IMyTerminalBlock, bool> GetDefaultEnabled()
        {
            return b => b.BlockDefinition.SubtypeId.StartsWith("WepControl");
        }
    }
}
