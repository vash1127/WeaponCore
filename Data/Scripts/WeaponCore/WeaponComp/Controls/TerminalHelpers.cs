using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using WeaponCore.Platform;
using static WeaponCore.Support.WeaponComponent.ShootActions;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef.EventTriggers;

namespace WeaponCore.Control
{
    public static class TerminalHelpers
    {

        #region Alter vanilla Actions/Control
        internal static void AlterActions<T>()
        {
            var isTurretType = typeof(T) == typeof(IMyLargeTurretBase);

            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);
            for (int i = isTurretType ? 12 : 0; i < actions.Count; i++)
            {
                var a = actions[i];

                if (!a.Id.Contains("OnOff") && !a.Id.Contains("Shoot") && !a.Id.Contains("WC_") && !a.Id.Contains("Control"))
                    a.Enabled = b => !b.Components.Has<WeaponComponent>();
                else if (a.Id.Equals("Control"))
                {
                    a.Enabled = (b) =>
                    {
                        WeaponComponent comp;
                        return !b.Components.TryGet(out comp) && comp.BaseType == WeaponComponent.BlockType.Turret;
                    };
                }
                else if (a.Id.Equals("ShootOnce"))
                {
                    var oldAction = a.Action;
                    a.Action = blk =>
                    {
                        var comp = blk?.Components?.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        {
                            if (comp == null)
                                oldAction(blk);

                            return;
                        }
                        comp.RequestShootUpdate(ShootOnce, comp.Session.DedicatedServer ? 0 : -1);
                    };
                }
                else if (a.Id.Equals("Shoot"))
                {
                    var oldAction = a.Action;
                    a.Action = blk =>
                    {
                        var comp = blk?.Components?.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        {
                            if (comp == null)
                                oldAction(blk);

                            return;
                        }

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
                        if (comp.Data.Repo.State.TerminalAction == ShootOn)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                }
                else if (a.Id.Equals("Shoot_On"))
                {
                    var oldAction = a.Action;
                    a.Action = blk =>
                    {
                        var comp = blk?.Components?.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        {
                            if (comp == null)
                                oldAction(blk);

                            return;
                        }

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
                        if (comp.Data.Repo.State.TerminalAction == ShootOn)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                }
                else if (a.Id.Equals("Shoot_Off"))
                {
                    var oldAction = a.Action;
                    a.Action = blk =>
                    {
                        var comp = blk?.Components?.Get<WeaponComponent>();
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        {
                            if (comp == null)
                                oldAction(blk);

                            return;
                        }
                        comp.RequestShootUpdate(ShootOff, comp.Session.DedicatedServer ? 0 : -1);
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
                        if (comp.Data.Repo.State.TerminalAction == ShootOn)
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
                "ShowInTerminal",
                "ShowInInventory",
                "ShowInToolbarConfig",
                "Name",
                "ShowOnHUD",
                "CustomData",
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

        internal static void AddUiControls<T>() where T : IMyTerminalBlock
        {
            Separator<T>(0, "WC_sep1", HasExtraUi);

            AddWeaponOnOff<T>(-1, "Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", WepUi.GetGuidance, WepUi.SetGuidance,
                (block, i) =>
                {
                    var comp = block?.Components?.Get<WeaponComponent>();
                    return comp != null && comp.HasGuidanceToggle;
                });

            AddSlider<T>(-2, "WC_Damage", "Change Damage Per Shot", "Change Damage Per Shot", WepUi.GetDps, WepUi.SetDpsFromTerminal,
                (block, i) =>
                {
                    var comp = block?.Components?.Get<WeaponComponent>();
                    return comp != null && comp.HasDamageSlider;
                });

            AddSlider<T>(-3, "WC_ROF", "Change Rate of Fire", "Change Rate of Fire", WepUi.GetRof, WepUi.SetRof,
                (block, i) =>
                {
                    var comp = block?.Components?.Get<WeaponComponent>();
                    return comp != null && comp.HasRofSlider;
                });

            AddCheckbox<T>(-4, "Overload", "Overload Damage", "Overload Damage", WepUi.GetOverload, WepUi.SetOverload, true,
                (block, i) =>
                {
                    var comp = block?.Components?.Get<WeaponComponent>();
                    return comp != null && comp.CanOverload;
                });
        }

        internal static bool HasExtraUi(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp != null && comp.CanOverload && comp.HasRofSlider && comp.HasDamageSlider && comp.HasGuidanceToggle;
        }

        private static void OnOffAnimations(WeaponComponent comp, bool on)
        {   
            if(comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {

                var w = comp.Platform.Weapons[i];
                if (w == null) continue;

                if (!on)
                {
                    if (w.TurretMode)
                    {
                        var azSteps = w.Azimuth / w.System.AzStep;
                        var elSteps = w.Elevation / w.System.ElStep;

                        if (azSteps < 0) azSteps *= -1;
                        if (azSteps < 0) azSteps *= -1;

                        w.OffDelay = (uint)(azSteps + elSteps > 0 ? azSteps > elSteps ? azSteps : elSteps : 0);

                        if (!w.Comp.Session.IsClient) w.Target.Reset(comp.Session.Tick, Target.States.Expired);
                        w.TurretHomePosition();

                    }

                    if(w.IsShooting) w.StopShooting();
                    if (w.DrawingPower) w.StopPowerDraw();

                    if (w.ActiveAmmoDef.AmmoDef.Const.MustCharge)
                        w.Reloading = false;
                }
                else
                {
                    if (!w.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || w.ActiveAmmoDef.AmmoDef.Const.MustCharge)
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            if (!w.Reload())
                                Session.ComputeStorage(w);
                        });
                    uint delay;
                    if (w.System.WeaponAnimationLengths.TryGetValue(TurnOn, out delay))
                        w.WeaponReadyTick = comp.Session.Tick + delay;

                    if (w.LastEvent == TurnOff && w.AnimationDelayTick > comp.Session.Tick)
                        w.WeaponReadyTick += w.AnimationDelayTick - comp.Session.Tick;
                }

                if (w.AnimationDelayTick < comp.Session.Tick || w.LastEvent == TurnOn || w.LastEvent == TurnOff)
                {
                    w.EventTriggerStateChanged(TurnOn, on);
                    w.EventTriggerStateChanged(TurnOff, !on);
                }
                else
                {
                    comp.Session.FutureEvents.Schedule(o => 
                        {
                            w.EventTriggerStateChanged(TurnOn, on);
                            w.EventTriggerStateChanged(TurnOff, !on);
                        }, 
                        null, 
                        w.AnimationDelayTick - comp.Session.Tick
                    );
                }
            }
        }
        #endregion

        #region Shoot Actions
        /*

        internal static void WcShootToggleAction(WeaponComponent comp, bool alreadySynced = false)
        {
            if (comp.State.Value.ShootOn)
                comp.RequestShootUpdate(ShootOff);
            else
                comp.RequestShootUpdate(ShootOn);
        }

        internal static void WcShootOnAction(WeaponComponent comp, bool alreadySynced = false)
        {
            comp.Session.TerminalMon.ClientUpdate(comp);
            var cState = comp.State.Value;

            for (int j = 0; j < comp.Platform.Weapons.Length; j++)
            {
                comp.Platform.Weapons[j].State.ManualShoot = ShootOn;
                comp.Platform.Weapons[j].State.SingleShotCounter = 0;
            }
            var update = comp.Set.Value.Overrides.ManualControl || comp.Set.Value.Overrides.TargetPainter;
            comp.Set.Value.Overrides.ManualControl = false;
            comp.Set.Value.Overrides.TargetPainter = false;


            if (!alreadySynced)
            {
                comp.State.Value.CurrentPlayerControl.PlayerId = comp.Session.PlayerId;
                comp.State.Value.CurrentPlayerControl.ControlType = ControlType.Toolbar;
            }

            cState.ShootOn = true;
            cState.ClickShoot = false;

            if (comp.Session.MpActive && !alreadySynced)
            {
                comp.Session.SendControlingPlayer(comp);
                comp.Session.SendActionShootUpdate(comp, ShootOn);
                if (update)
                    comp.Session.SendOverRidesUpdate(comp, comp.Set.Value.Overrides);
            }
        }

        internal static void WcShootOffAction(WeaponComponent comp, bool alreadySynced = false)
        {
            comp.Session.TerminalMon.ClientUpdate(comp);
            var cState = comp.State.Value;

            for (int j = 0; j < comp.Platform.Weapons.Length; j++)
            {
                comp.Platform.Weapons[j].State.ManualShoot = ShootOff;
                comp.Platform.Weapons[j].State.SingleShotCounter = 0;
            }
                
            var update = comp.Set.Value.Overrides.ManualControl || comp.Set.Value.Overrides.TargetPainter;
            comp.Set.Value.Overrides.ManualControl = false;
            comp.Set.Value.Overrides.TargetPainter = false;
            comp.State.Value.CurrentPlayerControl.PlayerId = -1;
            comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;

            cState.ShootOn = false;
            cState.ClickShoot = false;

            if (comp.Session.MpActive && !alreadySynced)
            {
                comp.Session.SendControlingPlayer(comp);
                comp.Session.SendActionShootUpdate(comp, ShootOff);
                if (update)
                    comp.Session.SendOverRidesUpdate(comp, comp.Set.Value.Overrides);
            }
        }

        internal static void WcShootOnceAction(WeaponComponent comp, bool alreadySynced = false)
        {
            comp.Session.TerminalMon.ClientUpdate(comp);
            var cState = comp.State.Value;

            for (int j = 0; j < comp.Platform.Weapons.Length; j++)
            {
                cState.Weapons[comp.Platform.Weapons[j].WeaponId].SingleShotCounter++;
                cState.Weapons[comp.Platform.Weapons[j].WeaponId].ManualShoot = ShootOnce;
            }

            var update = comp.Set.Value.Overrides.ManualControl || comp.Set.Value.Overrides.TargetPainter;
            comp.Set.Value.Overrides.ManualControl = false;
            comp.Set.Value.Overrides.TargetPainter = false;
            if (!alreadySynced)
            {
                comp.State.Value.CurrentPlayerControl.PlayerId = comp.Session.PlayerId;
                comp.State.Value.CurrentPlayerControl.ControlType = ControlType.Toolbar;
            }
            cState.ShootOn = false;
            cState.ClickShoot = false;

            if (comp.Session.MpActive && !alreadySynced)
            {
                comp.Session.SendControlingPlayer(comp);
                comp.Session.SendActionShootUpdate(comp, ShootOnce);
                if (update)
                    comp.Session.`(comp, comp.Set.Value.Overrides);
            }
        }

        internal static void WcShootClickAction(WeaponComponent comp, bool on, bool isTurret, bool alreadySynced = false)
        {
            comp.Session.TerminalMon.ClientUpdate(comp);
            var cState = comp.State.Value;

            if (!alreadySynced)
            {
                if (on)
                {
                    cState.CurrentPlayerControl.PlayerId = comp.Session.PlayerId;
                    cState.CurrentPlayerControl.ControlType = isTurret ? ControlType.Ui : ControlType.Toolbar;
                }
                else
                {
                    cState.CurrentPlayerControl.PlayerId = -1;
                    cState.CurrentPlayerControl.ControlType = ControlType.None;
                }
            }


            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];
                w.State.SingleShotCounter = 0;

                if (on)
                    w.State.ManualShoot = ShootClick;
                else
                    w.State.ManualShoot = ShootOff;
                
            }

            if (comp.Session.MpActive && !alreadySynced)
            {
                comp.Session.SendControlingPlayer(comp);
                comp.Session.SendActionShootUpdate(comp, (on ? ShootClick : ShootOff));
            }

            cState.ClickShoot = on;
            cState.ShootOn = !on && cState.ShootOn;
        }
        */
        #endregion

        #region Support methods

        internal static bool CompReady(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready;
        }

        internal static void ClickShootWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var on = blk.Components.Get<WeaponComponent>()?.Data.Repo?.State.TerminalAction == ShootClick;

            if (on)
                sb.Append("On");
            else
                sb.Append("Off");
        }

        internal static void TerminalActionShootClick(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            comp.RequestShootUpdate(ShootClick, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminActionToggleShoot(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            comp.RequestShootUpdate(ShootOn, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminalActionShootOn(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            comp.RequestShootUpdate(ShootOn, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminalActionShootOff(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            comp.RequestShootUpdate(ShootOff, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminalActionShootOnce(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            comp.RequestShootUpdate(ShootOnce, comp.Session.DedicatedServer ? 0 : -1);
        }

        internal static void TerminalActionCycleAmmo(IMyTerminalBlock blk, int id)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            int weaponId;
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId) || comp.Platform.Weapons[weaponId].System.WeaponIdHash != id)
                return;

            var w = comp.Platform.Weapons[weaponId];

            var availAmmo = w.System.AmmoTypes.Length;
            var currActive = w.System.AmmoTypes[w.State.AmmoTypeId];
            var next = (w.State.AmmoTypeId + 1) % availAmmo;
            var currDef = w.System.AmmoTypes[next];

            var change = false;

            while (!(currActive.Equals(currDef))) {
                if (currDef.AmmoDef.Const.IsTurretSelectable) {
                    change = true;
                    break;
                }

                next = (next + 1) % availAmmo;
                currDef = w.System.AmmoTypes[next];
            }

            if (change)
                w.ChangeAmmo(next);
        }

        internal static void ShootStateWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.State.TerminalAction == ShootOn)
                sb.Append("On");
            else
                sb.Append("Off");
        }
        #endregion

        #region terminal control methods

        internal static IMyTerminalControlOnOffSwitch AddWeaponOnOff<T>(int id, string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, int, bool> getter, Action<IMyTerminalBlock, int, bool> setter, Func<IMyTerminalBlock, int, bool> visibleGetter) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"WC_{id}_Enable");

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(onText);
            c.OffText = MyStringId.GetOrCompute(offText);
            c.Enabled = b => true;
            c.Visible = b => visibleGetter(b, id);
            c.Getter = b => getter(b, id);
            c.Setter = (b, enabled) => setter(b, id, enabled);
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            CreateOnOffActionSet<T>(c, name, id, visibleGetter, false);

            return c;
        }

        internal static IMyTerminalControlSeparator Separator<T>(int id, string name, Func<IMyTerminalBlock,bool> visibleGettter) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(name);

            c.Enabled = x => true;
            c.Visible = visibleGettter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddSlider<T>(int id, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, int, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null) where T : IMyTerminalBlock
        {
            var s = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            s.Title = MyStringId.GetOrCompute(title);
            s.Tooltip = MyStringId.GetOrCompute(tooltip);
            s.Enabled = b => true;
            s.Visible = b => visibleGetter(b, id);
            s.Getter = getter;
            s.Setter = setter;
            s.Writer = (b, v) => v.Append(getter(b).ToString("N2"));

            if (minGetter != null)
                s.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(s);

            CreateSliderActionSet<T>(s, name, id, 0, 1, .1f, visibleGetter);
            return s;
        }

        internal static IMyTerminalControlCheckbox AddCheckbox<T>(int id, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = b => visibleGetter != null && visibleGetter(b, id);
            c.Enabled = b => true;

            MyAPIGateway.TerminalControls.AddControl<T>(c);

            CreateOnOffActionSet<T>(c, name, id, visibleGetter, allowGroup);

            return c;
        }

        internal static void CreateOnOffActionSet<T>(IMyTerminalControlOnOffSwitch tc, string name, int id, Func<IMyTerminalBlock, int,bool> enabler, bool group = false) where T : IMyTerminalBlock
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle");
            action0.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action0.Name = new StringBuilder($"{name} Toggle On/Off");
            action0.Action = (b) => tc.Setter(b, !tc.Getter(b));
            action0.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action0.Enabled = (b) => enabler(b, id);
            action0.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_On");
            action1.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action1.Name = new StringBuilder($"{name} On");
            action1.Action = (b) => tc.Setter(b, true);
            action1.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action1.Enabled = (b) => enabler(b, id);
            action1.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);

            var action2 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_Off");
            action2.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action2.Name = new StringBuilder($"{name} Off");
            action2.Action = (b) => tc.Setter(b, true);
            action2.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action2.Enabled = (b) => enabler(b, id);
            action2.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action2);
        }

        internal static void CreateOnOffActionSet<T>(IMyTerminalControlCheckbox tc, string name, int id, Func<IMyTerminalBlock, int, bool> enabler, bool group = false) where T : IMyTerminalBlock
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle");
            action0.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action0.Name = new StringBuilder($"{name} Toggle On/Off");
            action0.Action = (b) => tc.Setter(b, !tc.Getter(b));
            action0.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action0.Enabled = (b) => enabler(b, id);
            action0.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_On");
            action1.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action1.Name = new StringBuilder($"{name} On");
            action1.Action = (b) => tc.Setter(b, true);
            action1.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action1.Enabled = (b) => enabler(b, id);
            action1.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);

            var action2 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_Off");
            action2.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action2.Name = new StringBuilder($"{name} Off");
            action2.Action = (b) => tc.Setter(b, true);
            action2.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action2.Enabled = (b) => enabler(b, id);
            action2.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action2);
        }

        internal static void CreateSliderActionSet<T>(IMyTerminalControlSlider tc, string name, int id, int min, int max, float incAmt, Func<IMyTerminalBlock, int, bool> enabler) where T : IMyTerminalBlock
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Increase");
            action0.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
            action0.Name = new StringBuilder($"Increase {name}");
            action0.Action = (b) => tc.Setter(b, tc.Getter(b) + incAmt <= max ? tc.Getter(b) + incAmt : max);
            action0.Writer = (b, t) => t.Append("");
            action0.Enabled = (b) => enabler(b, id);
            action0.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Decrease");
            action1.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            action1.Name = new StringBuilder($"Decrease {name}");
            action1.Action = (b) => tc.Setter(b, tc.Getter(b) - incAmt >= min ? tc.Getter(b) - incAmt : min);
            action1.Writer = (b, t) => t.Append("");
            action1.Enabled = (b) => enabler(b, id);
            action1.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);
        }
        #endregion

        #region Saved Code

        internal static IMyTerminalControlCombobox AddCombobox<T>(T block, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter, Action<List<MyTerminalControlComboBoxItem>> fillAction, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null) where T : IMyTerminalBlock
        {
            var cmb = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, T>(name);

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

        internal static IMyTerminalControlColor AddColorEditor<T>(T block, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, Color> getter, Action<IMyTerminalBlock, Color> setter, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = x => true;
            c.Visible = x => true;
            c.Getter = getter;
            c.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            return c;
        }
        #endregion
    }
}
