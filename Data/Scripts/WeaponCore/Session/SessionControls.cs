using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using WeaponCore.Support;
using VRage.Utils;
using WeaponCore.Control;
using WeaponCore.Platform;
using Sandbox.Definitions;
using VRage.Game;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.Entity;
using static WeaponCore.Support.WeaponComponent.ShootActions;
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
                AlterActions<T>();
                AlterControls<T>();

                TerminalHelpers.AddUiControls<T>();

                if (typeof(T) == typeof(IMyLargeTurretBase) || typeof(T) == typeof(IMySmallMissileLauncher) || typeof(T) == typeof(IMySmallGatlingGun) || typeof(T) == typeof(IMySmallMissileLauncherReload))
                {

                    session.BaseControlsActions = true;
                    CreateCustomActionSet<T>(session);
                }
                else if (typeof(T) == typeof(IMyConveyorSorter))
                {

                    CreateDefaultActions<T>(session);
                    TerminalHelpers.CreateSorterControls<T>();
                }

                TerminalHelpers.AddTurretControls<T>();
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlUi: {ex}"); }
        }

        internal static void CreateDefaultActions<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateShoot();
            CreateCustomActions<T>.CreateShootOn();
            CreateCustomActions<T>.CreateShootOff();
            CreateCustomActions<T>.CreateShootOnce();
            CreateCustomActionSet<T>(session);
        }

        internal static void CreateCustomActionSet<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateCycleAmmo(session);
            CreateCustomActions<T>.CreateShootClick();
            CreateCustomActions<T>.CreateNeutrals();
            CreateCustomActions<T>.CreateFriendly();
            CreateCustomActions<T>.CreateUnowned();
            CreateCustomActions<T>.CreateMaxSize();
            CreateCustomActions<T>.CreateMinSize();
            CreateCustomActions<T>.CreateMovementState();
            CreateCustomActions<T>.CreateControlModes();
            CreateCustomActions<T>.CreateSubSystems();
            CreateCustomActions<T>.CreateProjectiles();
            CreateCustomActions<T>.CreateBiologicals();
            CreateCustomActions<T>.CreateMeteors();
            CreateCustomActions<T>.CreateFocusTargets();
            CreateCustomActions<T>.CreateFocusSubSystem();
        }

        private void CustomControlHandler(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            LastTerminal = block;

            var cube = (MyCubeBlock)block;
            GridAi gridAi;
            if (GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
            {
                gridAi.LastTerminal = block;
                WeaponComponent comp;
                if (gridAi.WeaponBase.TryGetValue(cube, out comp) && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready)
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

        internal static void AlterActions<T>()
        {
            var isTurretType = typeof(T) == typeof(IMyLargeTurretBase);

            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);
            for (int i = isTurretType ? 11 : 0; i < actions.Count; i++) {

                var a = actions[i];

                if (!a.Id.Contains("OnOff") && !a.Id.Contains("Shoot") && !a.Id.Contains("WC_") && !a.Id.Contains("Control"))
                    a.Enabled = b => !b.Components.Has<WeaponComponent>();
                else if (a.Id.Equals("Control")) {

                    a.Enabled = (b) => {
                        WeaponComponent comp;
                        return !b.Components.TryGet(out comp) && comp.BaseType == WeaponComponent.BlockType.Turret;
                    };
                }
                else if (a.Id.Equals("ShootOnce")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                            if (comp == null)
                                oldAction(blk);
                            return;
                        }
                        comp.RequestShootUpdate(ShootOnce, comp.Session.DedicatedServer ? 0 : -1);
                    };
                }
                else if (a.Id.Equals("Shoot")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                            if (comp == null)
                                oldAction(blk);
                            return;
                        }
                        comp.RequestShootUpdate(ShootOn, comp.Session.DedicatedServer ? 0 : -1);
                    };

                    var oldWriter = a.Writer;
                    a.Writer = (blk, sb) => {

                        var comp = blk.Components.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                            oldWriter(blk, sb);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction == ShootOn)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                }
                else if (a.Id.Equals("Shoot_On")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                            if (comp == null) oldAction(blk);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction != ShootOn)
                            comp.RequestShootUpdate(ShootOn, comp.Session.DedicatedServer ? 0 : -1);
                    };

                    var oldWriter = a.Writer;
                    a.Writer = (blk, sb) =>
                    {
                        var comp = blk.Components.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        {
                            oldWriter(blk, sb);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction == ShootOn)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                }
                else if (a.Id.Equals("Shoot_Off")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                            if (comp == null)  oldAction(blk);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction != ShootOff)
                            comp.RequestShootUpdate(ShootOff, comp.Session.DedicatedServer ? 0 : -1);
                    };

                    var oldWriter = a.Writer;
                    a.Writer = (blk, sb) => {

                        var comp = blk.Components.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                            oldWriter(blk, sb);
                            return;
                        }
                        if (comp.Data.Repo.Base.State.TerminalAction == ShootOn)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                }
            }
        }

        internal static void AlterControls<T>() where T : IMyTerminalBlock
        {
            var isTurretType = typeof(T) == typeof(IMyLargeTurretBase);

            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);

            HashSet<string> visibleControls = new HashSet<string>
            {
                "OnOff",
                "Shoot",
                "ShowInTerminal",
                "ShowInInventory",
                "ShowInToolbarConfig",
                "Name",
                "Control",
            };

            for (int i = isTurretType ? 12 : 0; i < controls.Count; i++)
            {
                var c = controls[i];
                if (!visibleControls.Contains(c.Id))
                    c.Visible = b => !b.Components.Has<WeaponComponent>();

                switch (c.Id)
                {
                    case "Control":
                        c.Visible = b =>
                        {
                            var comp = b?.Components?.Get<WeaponComponent>();
                            return comp == null || comp.HasTurret;
                        };
                        break;

                    case "OnOff":
                        {
                            ((IMyTerminalControlOnOffSwitch)c).Setter += (blk, on) =>
                            {
                                var comp = blk?.Components?.Get<WeaponComponent>();
                                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

                                OnOffAnimations(comp, on);
                            };
                            break;
                        }
                }
            }
        }

        private static void OnOffAnimations(WeaponComponent comp, bool on)
        {
            if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

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

                    if (w.ActiveAmmoDef.AmmoDef.Const.MustCharge)
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

        public static void PurgeTerminalSystem()
        {
            var actions = new List<IMyTerminalAction>();
            var controls = new List<IMyTerminalControl>();
            var sControls = new List<IMyTerminalControl>();
            var sActions = new List<IMyTerminalAction>();

            MyAPIGateway.TerminalControls.GetActions<IMyUserControllableGun>(out actions);
            MyAPIGateway.TerminalControls.GetControls<IMyUserControllableGun>(out controls);

            foreach (var a in actions)
            {
                a.Writer = (block, builder) => { };
                a.Action = block => { };
                a.Enabled = block => false;
                MyAPIGateway.TerminalControls.RemoveAction<IMyUserControllableGun>(a);
            }
            foreach (var a in controls)
            {
                a.Enabled = block => false;
                a.Visible = block => false;
                MyAPIGateway.TerminalControls.RemoveControl<IMyUserControllableGun>(a);
            }

            foreach (var a in sActions)
            {
                a.Writer = (block, builder) => { };
                a.Action = block => { };
                a.Enabled = block => false;
                MyAPIGateway.TerminalControls.RemoveAction<MyConveyorSorter>(a);
            }
            foreach (var c in sControls)
            {
                c.Enabled = block => false;
                c.Visible = block => false;
                MyAPIGateway.TerminalControls.RemoveControl<MyConveyorSorter>(c);
            }
        }
        #endregion
    }
}