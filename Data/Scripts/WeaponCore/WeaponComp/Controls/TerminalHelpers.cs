using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;
using WeaponCore.Support;
using WeaponCore.Platform;

namespace WeaponCore.Control
{
    public static class TerminalHelpers
    {
        internal static void AddUiControls<T>(Session session) where T : IMyTerminalBlock
        {
            AddWeaponOnOff<T>(session, -2, "Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", WepUi.GetGuidance, WepUi.RequestSetGuidance, UiGuidance);

            AddSliderDamage<T>(session, -3, "WC_Damage", "Change Damage Per Shot", "Change Damage Per Shot", WepUi.GetDps, WepUi.RequestSetDps, UiDamageSlider);

            AddSliderRof<T>(session, -4, "WC_ROF", "Change Rate of Fire", "Change Rate of Fire", WepUi.GetRof, WepUi.RequestSetRof, UiRofSlider);

            AddCheckbox<T>(session, -5, "Overload", "Overload Damage", "Overload Damage", WepUi.GetOverload, WepUi.RequestSetOverload, true, UiOverLoad);

        }

        internal static void AddTurretOrTrackingControls<T>(Session session) where T : IMyTerminalBlock
        {
            Separator<T>(session, -7, "WC_sep2", HasTracking);

            AddSliderRange<T>(session, -8, "WC_Range", "Aiming Radius", "Range", WepUi.GetRange, WepUi.RequestSetRange, WepUi.ShowRange, WepUi.GetMinRange, WepUi.GetMaxRange, true);

            AddOnOffSwitchNoAction<T>(session, -9, "Neutrals", "Target Neutrals", "Target Neutrals", WepUi.GetNeutrals, WepUi.RequestSetNeutrals, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, -6, "Unowned", "Target Unowned", "Target Unowned", WepUi.GetUnowned, WepUi.RequestSetUnowned, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, -10, "Biologicals", "Target Biologicals", "Target Biologicals", WepUi.GetBiologicals, WepUi.RequestSetBiologicals, true, TrackBiologicals);

            AddOnOffSwitchNoAction<T>(session, -11, "Projectiles", "Target Projectiles", "Target Projectiles", WepUi.GetProjectiles, WepUi.RequestSetProjectiles, true,TrackProjectiles);

            AddOnOffSwitchNoAction<T>(session, -12, "Meteors", "Target Meteors", "Target Meteors", WepUi.GetMeteors, WepUi.RequestSetMeteors, true, TrackMeteors);

            AddOnOffSwitchNoAction<T>(session, -12, "Grids", "Target Grids", "Target Grids", WepUi.GetGrids, WepUi.RequestSetGrids, true, TrackGrids);

            AddOnOffSwitchNoAction<T>(session, -13, "FocusFire", "Target FocusFire", "Target FocusFire", WepUi.GetFocusFire,  WepUi.RequestSetFocusFire, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, -14, "SubSystems", "Target SubSystems", "Target SubSystems", WepUi.GetSubSystems, WepUi.RequestSetSubSystems, true, HasTracking);

            Separator<T>(session, -15, "WC_sep3", HasTracking);

            AddComboboxNoAction<T>(session, -16, "PickSubSystem", "Pick SubSystem", "Pick SubSystem", WepUi.GetSubSystem, WepUi.RequestSubSystem, WepUi.ListSubSystems, HasTracking);

            AddComboboxNoAction<T>(session, -17, "TrackingMode", "Tracking Mode", "Tracking Mode", WepUi.GetMovementMode, WepUi.RequestMovementMode, WepUi.ListMovementModes, HasTracking);
            
            AddComboboxNoAction<T>(session, -18, "ControlModes", "Control Mode", "Control Mode", WepUi.GetControlMode, WepUi.RequestControlMode, WepUi.ListControlModes, TurretOrGuidedAmmo);

            Separator<T>(session, -19, "WC_sep4", HasTracking);
        }

        internal static void AddDecoyControls<T>(Session session) where T: IMyTerminalBlock
        {
            Separator<T>(session, -7, "WC_decoySep1", Istrue);
            AddComboboxNoAction<T>(session, -8, "PickSubSystem", "Pick SubSystem", "Pick SubSystem", WepUi.GetDecoySubSystem, WepUi.RequestDecoySubSystem, WepUi.ListDecoySubSystems, Istrue);
        }

        internal static void CreateGenericControls<T>(Session session) where T : IMyTerminalBlock
        {
            AddOnOffSwitchNoAction<T>(session, -20, "Shoot", "Shoot", "Shoot On/Off", WepUi.GetShoot, WepUi.RequestSetShoot, true, IsReady);
        }

        internal static bool Istrue(IMyTerminalBlock block)
        {
            return true;
        }

        internal static bool HasComp(IMyTerminalBlock block)
        {
            return block.Components.Has<WeaponComponent>();
        }

        internal static bool IsReady(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready;
        }

        internal static bool UiRofSlider(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.HasRofSlider;
        }

        internal static bool UiDamageSlider(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.HasDamageSlider;
        }

        internal static bool UiOverLoad(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.CanOverload;
        }

        internal static bool UiGuidance(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.HasGuidanceToggle;
        }

        internal static bool TrackMeteors(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.TrackingWeapon.System.TrackMeteors;
        }

        internal static bool TrackGrids(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.TrackingWeapon.System.TrackGrids;
        }

        internal static bool TrackProjectiles(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.TrackingWeapon.System.TrackProjectile;
        }

        internal static bool TrackBiologicals(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.TrackingWeapon.System.TrackCharacters;
        }

        internal static bool AmmoSelection(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.AmmoSelectionWeaponIds.Count > 0;
        }

        internal static bool HasTracking(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.HasTracking;
        }

        internal static bool HasTurret(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.HasTurret;
        }

        internal static bool NoTurret(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && !comp.HasTurret;
        }

        internal static bool GuidedAmmo(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.TrackingWeapon.System.HasGuidedAmmo;
        }
        internal static bool TurretOrGuidedAmmo(IMyTerminalBlock block)
        {
            return HasTurret(block) || GuidedAmmo(block);
        }
        internal static void SliderWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append(WepUi.GetRange(block).ToString("N2"));
        }

        internal static void SliderWriterDamage(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append(WepUi.GetDps(block).ToString("N2"));
        }

        internal static void SliderWriterRof(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append(WepUi.GetRof(block).ToString("N2"));
        }

        internal static void EmptyStringBuilder(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append("");
        }

        internal static bool NotWcBlock(IMyTerminalBlock block)
        {
            return !block.Components.Has<WeaponComponent>(); 
        }

        internal static bool ShootOnceWeapon(IMyTerminalBlock block)
        {
            var comp = block.Components.Get<WeaponComponent>();

            return comp == null || comp.Session.DedicatedServer || !comp.HasDelayToFire;
        }

        internal static bool NotWcOrIsTurret(IMyTerminalBlock block)
        {
            WeaponComponent comp;
            return !block.Components.TryGet(out comp) || comp.HasTurret;
        }

        #region terminal control methods

        internal static IMyTerminalControlOnOffSwitch AddWeaponOnOff<T>(Session session, int id, string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, int, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, bool> visibleGetter) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"WC_{id}_Enable");

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(onText);
            c.OffText = MyStringId.GetOrCompute(offText);
            c.Enabled = Istrue;
            c.Visible = visibleGetter;
            c.Getter = Istrue;
            c.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateOnOffActionSet(session, c, name, id, visibleGetter, false);

            return c;
        }

        internal static IMyTerminalControlSeparator Separator<T>(Session session, int id, string name, Func<IMyTerminalBlock,bool> visibleGettter) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(name);

            c.Enabled = Istrue;
            c.Visible = visibleGettter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddSliderRange<T>(Session session, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = Istrue;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateSliderActionSet(session, c, name, id, 0, 1, .1f, visibleGetter, group);
            return c;
        }

        internal static IMyTerminalControlSlider AddSliderDamage<T>(Session session, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = Istrue;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWriterDamage;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateSliderActionSet(session, c, name, id, 0, 1, .1f, visibleGetter, false);
            return c;
        }

        internal static IMyTerminalControlSlider AddSliderRof<T>(Session session, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = Istrue;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWriterRof;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateSliderActionSet(session, c, name, id, 0, 1, .1f, visibleGetter, false);
            return c;
        }

        internal static IMyTerminalControlCheckbox AddCheckbox<T>(Session session, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = visibleGetter;
            c.Enabled = Istrue;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateOnOffActionSet(session, c, name, id, visibleGetter, allowGroup);

            return c;
        }

        internal static IMyTerminalControlCheckbox AddCheckboxNoAction<T>(Session session, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = visibleGetter;
            c.Enabled = Istrue;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlOnOffSwitch AddOnOffSwitchNoAction<T>(Session session, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>("WC_" + name);
            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute("On");
            c.OffText = MyStringId.GetOrCompute("Off");
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = visibleGetter;
            c.Enabled = Istrue;
            
            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlCombobox AddComboboxNoAction<T>(Session session, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter, Action<List<MyTerminalControlComboBoxItem>> fillAction, Func<IMyTerminalBlock,  bool> visibleGetter = null) where T : IMyTerminalBlock {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.ComboBoxContent = fillAction;
            c.Getter = getter;
            c.Setter = setter;

            c.Visible = visibleGetter;
            c.Enabled = Istrue;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        #endregion
    }
}
