using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;

namespace CoreSystems.Control
{
    public static class TerminalHelpers
    {
        internal static void AddUiControls<T>(Session session) where T : IMyTerminalBlock
        {
            AddWeaponOnOff<T>(session, "Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", BlockUi.GetGuidance, BlockUi.RequestSetGuidance, UiGuidance);

            AddSliderDamage<T>(session, "Weapon Damage", "Change Damage Per Shot", "Change the damage per shot", BlockUi.GetDps, BlockUi.RequestSetDps, UiStrengthSlider);

            AddSliderRof<T>(session, "Weapon ROF", "Change Rate of Fire", "Change rate of fire", BlockUi.GetRof, BlockUi.RequestSetRof, UiRofSlider);

            AddCheckbox<T>(session, "Overload", "Overload Damage", "Overload damage", BlockUi.GetOverload, BlockUi.RequestSetOverload, true, UiOverLoad);


            AddWeaponCrticalTimeSliderRange<T>(session, "Detonation", "Detonation time", "Detonation time", BlockUi.GetArmedTimer, BlockUi.RequestSetArmedTimer, NotCounting, CanBeArmed, BlockUi.GetMinCriticalTime, BlockUi.GetMaxCriticalTime, true);
            AddButtonNoAction<T>(session, "StartCount", "Start Countdown", "Start Countdown", BlockUi.StartCountDown, NotCounting, CanBeArmed);
            AddButtonNoAction<T>(session, "StopCount", "Stop Countdown", "Stop Countdown", BlockUi.StopCountDown, IsCounting, CanBeArmed);
            AddCheckboxNoAction<T>(session, "Arm", "Arm Reaction", "Arm Reaction", BlockUi.GetArmed, BlockUi.RequestSetArmed, true, CanBeArmed);
            AddButtonNoAction<T>(session, "Trigger", "Trigger", "Trigger", BlockUi.TriggerCriticalReaction, IsArmed, CanBeArmed);
        }

        internal static void AddTurretOrTrackingControls<T>(Session session) where T : IMyTerminalBlock
        {
            Separator<T>(session, "WC_sep2", HasTracking);

            AddWeaponRangeSliderNoAction<T>(session, "Weapon Range", "Aiming Radius", "Change the min/max targeting range", BlockUi.GetRange, BlockUi.RequestSetRange, BlockUi.ShowRange, BlockUi.GetMinRange, BlockUi.GetMaxRange, true);

            AddOnOffSwitchNoAction<T>(session, "Neutrals", "Target Neutrals", "Fire on targets that are neutral", BlockUi.GetNeutrals, BlockUi.RequestSetNeutrals, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, "Unowned", "Target Unowned", "Fire on targets with no owner", BlockUi.GetUnowned, BlockUi.RequestSetUnowned, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, "Biologicals", "Target Biologicals", "Fire on players and biological NPCs", BlockUi.GetBiologicals, BlockUi.RequestSetBiologicals, true, TrackBiologicals);

            AddOnOffSwitchNoAction<T>(session,  "Projectiles", "Target Projectiles", "Fire on incoming projectiles", BlockUi.GetProjectiles, BlockUi.RequestSetProjectiles, true, TrackProjectiles);

            AddOnOffSwitchNoAction<T>(session, "Meteors", "Target Meteors", "Target Meteors", BlockUi.GetMeteors, BlockUi.RequestSetMeteors, true, TrackMeteors);

            AddOnOffSwitchNoAction<T>(session,  "Grids", "Target Grids", "Target Grids", BlockUi.GetGrids, BlockUi.RequestSetGrids, true, TrackGrids);

            AddOnOffSwitchNoAction<T>(session, "FocusFire", "Target FocusFire", "Focus all fire on the specified target", BlockUi.GetFocusFire, BlockUi.RequestSetFocusFire, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, "SubSystems", "Target SubSystems", "Target specific SubSystems of a target", BlockUi.GetSubSystems, BlockUi.RequestSetSubSystems, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, "Repel", "Repel Mode", "Aggressively focus and repel small threats", BlockUi.GetRepel, BlockUi.RequestSetRepel, true, HasTracking);

            Separator<T>(session, "WC_sep3", HasTracking);

            AddComboboxNoAction<T>(session, "PickSubSystem", "Pick SubSystem", "Select the target subsystem to focus fire on", BlockUi.GetSubSystem, BlockUi.RequestSubSystem, BlockUi.ListSubSystems, HasTracking);

            AddComboboxNoAction<T>(session, "TrackingMode", "Tracking Mode", "Movement fire control requirements", BlockUi.GetMovementMode, BlockUi.RequestMovementMode, BlockUi.ListMovementModes, HasTracking);

            AddComboboxNoAction<T>(session,  "ControlModes", "Control Mode", "Select the aim control mode for the weapon", BlockUi.GetControlMode, BlockUi.RequestControlMode, BlockUi.ListControlModes, TurretOrGuidedAmmo);

            AddWeaponCameraSliderRange<T>(session, "Camera Channel", "Weapon Camera Channel", "Assign this weapon to a camera channel", BlockUi.GetWeaponCamera, BlockUi.RequestSetBlockCamera, HasTracking, BlockUi.GetMinCameraChannel, BlockUi.GetMaxCameraChannel, true);

            AddLeadGroupSliderRange<T>(session, "Target Group", "Target Lead Group", "Assign this weapon to target lead group", BlockUi.GetLeadGroup, BlockUi.RequestSetLeadGroup, TargetLead, BlockUi.GetMinLeadGroup, BlockUi.GetMaxLeadGroup, true);

            Separator<T>(session, "WC_sep4", HasTracking);
        }

        internal static void AddDecoyControls<T>(Session session) where T : IMyTerminalBlock
        {
            Separator<T>(session, "WC_decoySep1", Istrue);
            AddComboboxNoAction<T>(session, "PickSubSystem", "Pick SubSystem", "Pick what subsystem this decoy will imitate", BlockUi.GetDecoySubSystem, BlockUi.RequestDecoySubSystem, BlockUi.ListDecoySubSystems, Istrue);
        }

        internal static void AddCameraControls<T>(Session session) where T : IMyTerminalBlock
        {
            Separator<T>(session,  "WC_cameraSep1", Istrue);
            AddBlockCameraSliderRange<T>(session, "WC_PickCameraChannel", "Camera Channel", "Assign the camera weapon channel to this camera", BlockUi.GetBlockCamera, BlockUi.RequestBlockCamera, BlockUi.ShowCamera, BlockUi.GetMinCameraChannel, BlockUi.GetMaxCameraChannel, true);
        }

        internal static void CreateGenericControls<T>(Session session) where T : IMyTerminalBlock
        {
            AddOnOffSwitchNoAction<T>(session,  "Debug", "Debug", "Debug On/Off", BlockUi.GetDebug, BlockUi.RequestDebug, true, IsReady);
            Separator<T>(session, "WC_sep4", HasTracking);
            AddOnOffSwitchNoAction<T>(session,  "Shoot", "Shoot", "Shoot On/Off", BlockUi.GetShoot, BlockUi.RequestSetShoot, true, IsNotBomb);

        }

        internal static void CreateGenericArmor<T>(Session session) where T : IMyTerminalBlock
        {
            AddOnOffSwitchNoAction<T>(session, "Show Enhanced Area", "Area Influence", "Show On/Off", BlockUi.GetShowArea, BlockUi.RequestSetShowArea, true, SupportIsReady);
        }

        internal static bool Istrue(IMyTerminalBlock block)
        {
            return true;
        }

        internal static bool ShootOnceWeapon(IMyTerminalBlock block)
        {
            var comp = block.Components.Get<CoreComponent>();

            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Type == CoreComponent.CompType.Weapon;
        }

        internal static bool WeaponIsReady(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Type == CoreComponent.CompType.Weapon;
        }

        internal static bool SupportIsReady(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.TypeSpecific == CoreComponent.CompTypeSpecific.Support;
        }

        internal static bool UiRofSlider(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasRofSlider;
        }

        internal static bool UiStrengthSlider(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasStrengthSlider;
        }

        internal static bool UiOverLoad(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.CanOverload;
        }

        internal static bool UiGuidance(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasGuidanceToggle;
        }

        internal static bool TrackMeteors(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.TrackingWeapon.System.TrackMeteors && !comp.IsBlock;
        }

        internal static bool TrackGrids(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.TrackingWeapon.System.TrackGrids && !comp.IsBlock;
        }

        internal static bool TrackProjectiles(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.TrackingWeapon.System.TrackProjectile && !comp.IsBlock;
        }

        internal static bool TrackBiologicals(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.TrackingWeapon.System.TrackCharacters && !comp.IsBlock;
        }

        internal static bool AmmoSelection(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.ConsumableSelectionPartIds.Count > 0 && comp.Type == CoreComponent.CompType.Weapon;
        }

        internal static bool HasTracking(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasTracking;
        }

        internal static bool CanBeArmed(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasArming;
        }

        internal static bool IsCounting(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Data.Repo.Values.State.CountingDown;
        }

        internal static bool NotCounting(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && !comp.Data.Repo.Values.State.CountingDown;
        }

        internal static bool IsArmed(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Data.Repo.Values.Set.Overrides.Armed;
        }

        internal static bool IsReady(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready;
        }

        internal static bool IsNotBomb(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && !comp.IsBomb;
        }
        internal static bool HasSupport(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Type == CoreComponent.CompType.Support;
        }

        internal static bool HasTurret(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasTurret && comp.Type == CoreComponent.CompType.Weapon; 
        }

        internal static bool NoTurret(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && !comp.HasTurret && comp.Type == CoreComponent.CompType.Weapon;
        }

        internal static bool TargetLead(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && !comp.IsBlock && (!comp.HasTurret && !comp.OverrideLeads || comp.HasTurret && comp.OverrideLeads) && comp.Type == CoreComponent.CompType.Weapon;
        }

        internal static bool GuidedAmmo(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.TrackingWeapon.System.HasGuidedAmmo;
        }
        internal static bool TurretOrGuidedAmmo(IMyTerminalBlock block)
        {
            return HasTurret(block) || GuidedAmmo(block);
        }
        internal static void SliderWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append(BlockUi.GetRange(block).ToString("N2"));
        }

        internal static void SliderWriterDamage(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append(BlockUi.GetDps(block).ToString("N2"));
        }

        internal static void SliderWriterRof(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append(BlockUi.GetRof(block).ToString("N2"));
        }

        internal static void EmptyStringBuilder(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append("");
        }

        internal static bool NotWcBlock(IMyTerminalBlock block)
        {
            return !block.Components.Has<CoreComponent>(); 
        }

        internal static bool NotWcOrIsTurret(IMyTerminalBlock block)
        {
            CoreComponent comp;
            return !block.Components.TryGet(out comp) || comp.HasTurret;
        }

        internal static void SliderBlockCameraWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {
            long value = -1;
            string message;
            if (string.IsNullOrEmpty(block.CustomData) || long.TryParse(block.CustomData, out value))
            {
                var group = value >= 0 ? value : 0;
                message = value == 0 ? "Disabled" : group.ToString();
            }
            else message = "Invalid CustomData";

            builder.Append(message);
        }

        internal static void SliderWeaponCameraWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = Convert.ToInt64(BlockUi.GetWeaponCamera(block));
            var message = value > 0 ? value.ToString() : "Disabled";

            builder.Append(message);
        }

        internal static void SliderCriticalTimerWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = BlockUi.GetArmedTimeRemaining(block);

            string message;
            if (value >=59.95)
                message = "01:00:00";
            else if (value < 0.33)
                message = "00:00:00";
            else {
                message = $"00:{value}";
                message = message.Replace(".", ":");
            }

            builder.Append(message);
        }

        internal static void SliderLeadGroupWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = Convert.ToInt64(BlockUi.GetLeadGroup(block));
            var message = value > 0 ? value.ToString() : "Disabled";

            builder.Append(message);
        }

        #region terminal control methods
        internal static IMyTerminalControlSlider AddBlockCameraSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = Istrue;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderBlockCameraWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateSliderActionSet(session, c, name, 0, 1, .1f, visibleGetter, group);
            return c;
        }


        internal static IMyTerminalControlSlider AddWeaponCameraSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = Istrue;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWeaponCameraWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddWeaponCrticalTimeSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> enableGetter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = enableGetter;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderCriticalTimerWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddLeadGroupSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = Istrue;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderLeadGroupWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);
            return c;
        }

        internal static IMyTerminalControlOnOffSwitch AddWeaponOnOff<T>(Session session, string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, int, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, bool> visibleGetter) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"WC_Enable");

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

            CreateCustomActions<T>.CreateOnOffActionSet(session, c, name, visibleGetter);

            return c;
        }

        internal static IMyTerminalControlSeparator Separator<T>(Session session, string name, Func<IMyTerminalBlock,bool> visibleGettter) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(name);

            c.Enabled = Istrue;
            c.Visible = visibleGettter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddWeaponRangeSliderNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false, bool addAction = true) where T : IMyTerminalBlock
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

            return c;
        }

        internal static IMyTerminalControlSlider AddSliderDamage<T>(Session session,  string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null) where T : IMyTerminalBlock
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

            CreateCustomActions<T>.CreateSliderActionSet(session, c, name, 0, 1, .1f, visibleGetter, false);
            return c;
        }

        internal static IMyTerminalControlSlider AddSliderRof<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null) where T : IMyTerminalBlock
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

            CreateCustomActions<T>.CreateSliderActionSet(session, c, name, 0, 1, .1f, visibleGetter, false);
            return c;
        }

        internal static IMyTerminalControlCheckbox AddCheckbox<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
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

            CreateCustomActions<T>.CreateOnOffActionSet(session, c, name, visibleGetter, allowGroup);

            return c;
        }

        internal static IMyTerminalControlCheckbox AddCheckboxNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
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

        internal static IMyTerminalControlOnOffSwitch AddOnOffSwitchNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
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

        internal static IMyTerminalControlCombobox AddComboboxNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter, Action<List<MyTerminalControlComboBoxItem>> fillAction, Func<IMyTerminalBlock,  bool> visibleGetter = null) where T : IMyTerminalBlock {
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

        internal static IMyTerminalControlButton AddButtonNoAction<T>(Session session, string name, string title, string tooltip, Action<IMyTerminalBlock> action, Func<IMyTerminalBlock, bool> enableGetter, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Action = action;
            c.Visible = visibleGetter;
            c.Enabled = enableGetter;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        #endregion
    }
}
