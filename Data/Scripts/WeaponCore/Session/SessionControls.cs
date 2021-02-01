using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using WeaponCore.Support;
using WeaponCore.Control;
using WeaponCore.Platform;
using static WeaponCore.Support.CoreComponent.ShootActions;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace WeaponCore
{
    public partial class Session
    {
        #region UI Config
        public static void CreateTerminalUi<T>(Session session) where T : IMyTerminalBlock
        {
            try
            {
                AlterActions<T>(session);
                AlterControls<T>(session);

                TerminalHelpers.CreateGenericControls<T>(session);
                TerminalHelpers.AddUiControls<T>(session);

                if (typeof(T) == typeof(IMyLargeTurretBase) || typeof(T) == typeof(IMySmallMissileLauncher) || typeof(T) == typeof(IMySmallGatlingGun) || typeof(T) == typeof(IMySmallMissileLauncherReload))
                {
                    session.BaseControlsActions = true;
                    CreateCustomActionSet<T>(session);
                }
                else if (typeof(T) == typeof(IMyConveyorSorter))
                {
                    CreateDefaultActions<T>(session);
                }

                TerminalHelpers.AddTurretOrTrackingControls<T>(session);
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlUi: {ex}"); }
        }

        internal static void CreateDefaultActions<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateShoot(session);
            CreateCustomActions<T>.CreateShootOn(session);
            CreateCustomActions<T>.CreateShootOff(session);
            CreateCustomActions<T>.CreateShootOnce(session);
            CreateCustomActionSet<T>(session);
        }

        internal static void CreateCustomActionSet<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateCycleAmmo(session);
            CreateCustomActions<T>.CreateShootClick(session);
            CreateCustomActions<T>.CreateNeutrals(session);
            CreateCustomActions<T>.CreateFriendly(session);
            CreateCustomActions<T>.CreateUnowned(session);
            CreateCustomActions<T>.CreateMaxSize(session);
            CreateCustomActions<T>.CreateMinSize(session);
            CreateCustomActions<T>.CreateMovementState(session);
            CreateCustomActions<T>.CreateControlModes(session);
            CreateCustomActions<T>.CreateSubSystems(session);
            CreateCustomActions<T>.CreateProjectiles(session);
            CreateCustomActions<T>.CreateBiologicals(session);
            CreateCustomActions<T>.CreateMeteors(session);
            CreateCustomActions<T>.CreateGrids(session);
            CreateCustomActions<T>.CreateFocusTargets(session);
            CreateCustomActions<T>.CreateFocusSubSystem(session);
        }

        private void CustomControlHandler(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            LastTerminal = block;

            var cube = (MyCubeBlock)block;
            GridAi gridAi;
            if (GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
            {
                gridAi.LastTerminal = block;
                CoreComponent comp;
                if (gridAi.WeaponBase.TryGetValue(cube, out comp) && comp.Platform.State == CorePlatform.PlatformState.Ready)
                {
                    TerminalMon.HandleInputUpdate(comp);
                    IMyTerminalControl wcRangeControl = null;
                    for (int i = controls.Count - 1; i >= 0; i--)
                    {
                        var control = controls[i];
                        if (control.Id.Equals("Range"))
                        {
                            controls.RemoveAt(i);
                        }
                        else if (control.Id.Equals("UseConveyor"))
                        {
                            controls.RemoveAt(i);
                        }
                        else if (control.Id.Equals("WC_Range"))
                        {
                            wcRangeControl = control;
                            controls.RemoveAt(i);
                        }
                    }

                    if (wcRangeControl != null)
                    {
                        controls.Add(wcRangeControl);
                    }
                }
            }
        }

        internal static void AlterActions<T>(Session session)
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);
            for (int i = 0; i < actions.Count; i++) {

                var a = actions[i];

                if (!a.Id.Contains("OnOff") && !a.Id.Contains("Shoot") && !a.Id.Contains("WC_") && !a.Id.Contains("Control"))
                {
                    a.Enabled = TerminalHelpers.NotWcBlock;
                    session.AlteredActions.Add(a);
                }
                else if (a.Id.Equals("Control")) {

                    a.Enabled = TerminalHelpers.NotWcOrIsTurret;
                    session.AlteredActions.Add(a);
                }
                else if (a.Id.Equals("ShootOnce")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<CoreComponent>();
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            if (comp == null)
                                oldAction(blk);
                            return;
                        }
                        comp.RequestShootUpdate(ShootOnce, comp.Session.DedicatedServer ? 0 : -1);
                    };
                    session.AlteredActions.Add(a);
                }
                else if (a.Id.Equals("Shoot")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<CoreComponent>();
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            if (comp == null)
                                oldAction(blk);
                            return;
                        }
                        comp.RequestShootUpdate(ShootOn, comp.Session.DedicatedServer ? 0 : -1);
                    };

                    var oldWriter = a.Writer;
                    a.Writer = (blk, sb) => {

                        var comp = blk.Components.Get<CoreComponent>();
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            oldWriter(blk, sb);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction == ShootOn)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                    session.AlteredActions.Add(a);
                }
                else if (a.Id.Equals("Shoot_On")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<CoreComponent>();
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            if (comp == null) oldAction(blk);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction != ShootOn)
                            comp.RequestShootUpdate(ShootOn, comp.Session.DedicatedServer ? 0 : -1);
                    };

                    var oldWriter = a.Writer;
                    a.Writer = (blk, sb) =>
                    {
                        var comp = blk.Components.Get<CoreComponent>();
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                        {
                            oldWriter(blk, sb);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction == ShootOn)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                    session.AlteredActions.Add(a);
                }
                else if (a.Id.Equals("Shoot_Off")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<CoreComponent>();
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            if (comp == null)  oldAction(blk);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction != ShootOff)
                            comp.RequestShootUpdate(ShootOff, comp.Session.DedicatedServer ? 0 : -1);
                    };

                    var oldWriter = a.Writer;
                    a.Writer = (blk, sb) => {

                        var comp = blk.Components.Get<CoreComponent>();
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            oldWriter(blk, sb);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction == ShootOn)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                    session.AlteredActions.Add(a);
                }
            }
        }

        internal static void AlterControls<T>(Session session) where T : IMyTerminalBlock
        {
            var validType = typeof(T) == typeof(IMyUserControllableGun);
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);

            HashSet<string> visibleControls = new HashSet<string> {
                "OnOff",
                "Shoot",
                "ShowInTerminal",
                "ShowInInventory",
                "ShowInToolbarConfig",
                "Name",
                "ShowOnHUD",
                "CustomData",
                "Control",
                "Renamer_Separator",
                "Renamer_Label",
                "Renamer_Textbox",
                "Renamer_RenameButton",
                "Renamer_PrefixButton",
                "Renamer_SuffixButton",
                "Renamer_ResetButton",
            };
            for (int i = validType ? 12 : 0; i < controls.Count; i++) {

                var c = controls[i];
                if (!visibleControls.Contains(c.Id)) {
                    c.Visible = TerminalHelpers.NotWcBlock;
                    session.AlteredControls.Add(c);
                    continue;
                }

                switch (c.Id) {
                    case "Shoot":
                        c.Visible = TerminalHelpers.NotWcBlock;
                        session.AlteredControls.Add(c);
                        break;
                    case "Control":
                        c.Visible = TerminalHelpers.NotWcOrIsTurret;
                        session.AlteredControls.Add(c);
                        break;

                    case "OnOff":
                        ((IMyTerminalControlOnOffSwitch) c).Setter += OnOffSetter;
                        session.AlteredControls.Add(c);
                        break;
                }
            }
        }

        private static void OnOffSetter(IMyTerminalBlock block, bool on)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            OnOffAnimations(comp, on);
        }

        private static void OnOffAnimations(CoreComponent comp, bool on)
        {
            if (comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {

                var w = comp.Platform.Weapons[i];
                if (w == null) continue;

                if (!on) {

                    if (w.TurretMode) {
                        var azSteps = w.Azimuth / w.System.AzStep;
                        var elSteps = w.Elevation / w.System.ElStep;

                        if (azSteps < 0) azSteps *= -1;
                        if (azSteps < 0) azSteps *= -1;

                        w.OffDelay = (uint)(azSteps + elSteps > 0 ? azSteps > elSteps ? azSteps : elSteps : 0);

                        if (!w.Comp.Session.IsClient) w.Target.Reset(comp.Session.Tick, Target.States.AnimationOff);
                        w.ScheduleWeaponHome(true);
                    }

                    if (w.IsShooting) {
                        w.StopShooting();
                        Log.Line($"StopShooting OnOffAnimations");
                    }
                    if (w.DrawingPower) w.StopPowerDraw();

                    if (w.ActiveAmmoDef.ConsumableDef.Const.MustCharge)
                        w.Reloading = false;
                }
                else {

                    uint delay;
                    if (w.System.WeaponAnimationLengths.TryGetValue(EventTriggers.TurnOn, out delay))
                        w.WeaponReadyTick = comp.Session.Tick + delay;

                    if (w.LastEvent == EventTriggers.TurnOff && w.AnimationDelayTick > comp.Session.Tick)
                        w.WeaponReadyTick += w.AnimationDelayTick - comp.Session.Tick;
                }

                if (w.AnimationDelayTick < comp.Session.Tick || w.LastEvent == EventTriggers.TurnOn || w.LastEvent == EventTriggers.TurnOff) {
                    w.EventTriggerStateChanged(EventTriggers.TurnOn, on);
                    w.EventTriggerStateChanged(EventTriggers.TurnOff, !on);
                }
                else {

                    comp.Session.FutureEvents.Schedule(o => {
                        w.EventTriggerStateChanged(EventTriggers.TurnOn, on);
                        w.EventTriggerStateChanged(EventTriggers.TurnOff, !on);
                    },
                        null,
                        w.AnimationDelayTick - comp.Session.Tick
                    );
                }
            }
        }

        public static void PurgeTerminalSystem(Session session)
        {
            foreach (var a in session.CustomActions)
            {
                MyAPIGateway.TerminalControls.RemoveAction<IMyTerminalBlock>(a);

                a.Writer = EmptyWritter;
                a.Action = EmptyAction;
                a.Enabled = EmptyBool;
                a.Action = null;
                a.Enabled = null;
            }
            session.CustomActions.Clear();

            foreach (var a in session.AlteredActions)
            {
                MyAPIGateway.TerminalControls.RemoveAction<IMyTerminalBlock>(a);

                a.Writer = EmptyWritter;
                a.Action = EmptyAction;
                a.Enabled = EmptyBool;
                a.Action = null;
                a.Enabled = null;
            }
            session.AlteredActions.Clear();

            foreach (var c in session.CustomControls)
            {
                MyAPIGateway.TerminalControls.RemoveControl<IMyTerminalBlock>(c);
                c.Enabled = EmptyBool;
                c.Visible = EmptyBool;
                c.Enabled = null;
                c.Visible = null;
            }
            session.CustomControls.Clear();

            foreach (var c in session.AlteredControls)
            {
                MyAPIGateway.TerminalControls.RemoveControl<IMyTerminalBlock>(c);
                c.Enabled = EmptyBool;
                c.Visible = EmptyBool;
                c.Enabled = null;
                c.Visible = null;
            }
            session.AlteredControls.Clear();
        }

        private static void EmptyAction(IMyTerminalBlock obj)
        {
        }

        private static bool EmptyBool(IMyTerminalBlock obj)
        {
            return false;
        }

        public static void EmptyWritter(IMyTerminalBlock myTerminalBlock, StringBuilder stringBuilder)
        {
        }
        #endregion
    }
}