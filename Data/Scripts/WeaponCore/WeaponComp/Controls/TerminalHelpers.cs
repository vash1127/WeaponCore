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
using static WeaponCore.Platform.Weapon.TerminalActionState;

namespace WeaponCore.Control
{
    public static class TerminalHelpers
    {
        internal static bool AlterActions<T>()
        {
            var isTurretType = typeof(T) == typeof(IMyLargeTurretBase);

            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);
            for (int i = isTurretType ? 13 : 0; i < actions.Count; i++)
            {
                var a = actions[i];
                //Log.Line($"Count: {i} ID:{a.Id}");

                if (!a.Id.Contains("OnOff") && !a.Id.Equals("Shoot") && !a.Id.Equals("ShootOnce"))
                    a.Enabled = b => !b.Components.Has<WeaponComponent>();

                else if(a.Id.Contains("Control"))
                    a.Enabled = b =>
                    {
                        var comp = b?.Components?.Get<WeaponComponent>();
                        return comp == null || comp.HasTurret;
                    };

                else if (a.Id.Equals("ShootOnce"))
                {
                    var oldAction = a.Action;
                    a.Action = blk =>
                    {
                        var comp = blk?.Components?.Get<WeaponComponent>();
                        oldAction(blk);
                        if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                        for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                        {
                            if (comp.State.Value.Weapons[comp.Platform.Weapons[j].WeaponId].ManualShoot != ShootOff) continue;
                            comp.State.Value.Weapons[comp.Platform.Weapons[j].WeaponId].ManualShoot = ShootOnce;
                            comp.Ai.ManualComps++;
                        }
                    };
                }
                else if (a.Id.Equals("OnOff"))
                {
                    a.Enabled = (IMyTerminalBlock b) => true;
                    a.Writer = (block, strBuild) => strBuild.Append("Code For on/of values");
                }
            }
            return false;
        }

        internal static bool AlterControls<T>()
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
                "Range",
                "Shoot"
            };

            for (int i = isTurretType ? 14 : 0; i < controls.Count; i++)
            {
                var c = controls[i];

                if(!visibleControls.Contains(c.Id))
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

                    case "ShootOnce":
                        ((IMyTerminalControlButton)c).Action += blk =>
                        {
                            var comp = blk?.Components?.Get<WeaponComponent>();
                            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                            
                            for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                            {
                                if (comp.State.Value.Weapons[comp.Platform.Weapons[j].WeaponId].ManualShoot != ShootOff) continue;
                                comp.State.Value.Weapons[comp.Platform.Weapons[j].WeaponId].ManualShoot = ShootOnce;
                                comp.Ai.ManualComps++;
                            }

                        };
                        break;

                    case "Shoot":
                        ((IMyTerminalControlOnOffSwitch)c).Setter += (blk, on) =>
                        {
                            var comp = blk?.Components?.Get<WeaponComponent>();
                            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                            for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                            {
                                var w = comp.Platform.Weapons[j];

                                if (!on && w.State.ManualShoot == ShootOn)
                                {
                                    w.State.ManualShoot = ShootOff;
                                    if (w.IsShooting)
                                        w.StopShooting();
                                    else if(w.DrawingPower && !w.System.MustCharge)
                                        w.StopPowerDraw();

                                    if (w.System.MustCharge)
                                    {
                                        if (w.State.CurrentAmmo != w.System.EnergyMagSize)
                                            w.State.CurrentAmmo = 0;
                                    }

                                    comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
                                }
                                else if (on && w.State.ManualShoot != ShootOff)
                                    w.State.ManualShoot = ShootOn;
                                else if (on)
                                {
                                    w.State.ManualShoot = ShootOn;
                                    comp.Ai.ManualComps++;
                                }
                            }
                        };
                        break;

                    case "OnOff":
                        ((IMyTerminalControlOnOffSwitch)c).Setter += OnOffAnimations;
                        break;

                    case "Range":
                        ((IMyTerminalControlSlider)c).Setter += (blk, Value) =>
                        {
                            var comp = blk?.Components?.Get<WeaponComponent>();
                            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

                            comp.Set.Value.Range = Value;
                        };                        
                        break;
                }  
            }
            return false;
        }

        private static void OnOffAnimations(IMyTerminalBlock blk, bool On)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            if (!On) comp.CurrentCharge = 0;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                if (!On)
                {
                    if (w.TurretMode)
                    {
                        var azSteps = w.Azimuth / w.System.AzStep;
                        var elSteps = w.Elevation / w.System.ElStep;

                        if (azSteps < 0) azSteps = azSteps * -1;
                        if (azSteps < 0) azSteps = azSteps * -1;

                        w.OffDelay = (uint)(azSteps + elSteps > 0 ? azSteps > elSteps ? azSteps : elSteps : 0);

                        w.Target.Reset();
                        w.TurretHomePosition();

                    }
                    w.StopShooting();

                    if (w.System.MustCharge && ((w.System.IsHybrid && w.State.CurrentAmmo != w.System.MagazineDef.Capacity) || (!w.System.IsHybrid && w.State.CurrentAmmo != w.System.EnergyMagSize)))
                    {
                        w.CurrentCharge = 0;
                        w.State.CurrentAmmo = 0;
                        w.Reloading = false;
                    }
                    comp.CurrentCharge += w.CurrentCharge;

                    uint delay;
                    if (w.System.WeaponAnimationLengths.TryGetValue(Weapon.EventTriggers.TurnOff, out delay))
                        w.AnimationDelayTick = w.ShootDelayTick = comp.Session.Tick + delay + w.OffDelay;
                }
                else
                {
                    w.OffDelay = 0;
                    uint delay;
                    if (w.System.WeaponAnimationLengths.TryGetValue(Weapon.EventTriggers.TurnOn, out delay))
                        w.AnimationDelayTick = w.ShootDelayTick = comp.Session.Tick + delay;

                    if (!w.System.EnergyAmmo || w.System.MustCharge)
                        Session.ComputeStorage(w);
                }

                
                if (w.AnimationDelayTick < comp.Session.Tick || w.LastEvent == Weapon.EventTriggers.TurnOn || w.LastEvent == Weapon.EventTriggers.TurnOff)
                {
                    w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOn, On);
                    w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, !On);
                }
                else
                {
                    comp.Session.FutureEvents.Schedule((object o) => 
                        {
                            w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOn, On);
                            w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, !On);
                        }, 
                        null, 
                        w.AnimationDelayTick - comp.Session.Tick
                    );
                }

                w.Set.Enable = On;
            }
        }

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

        internal static IMyTerminalControlSeparator Separator<T>(int id, string name) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(name);

            c.Enabled = x => true;
            c.Visible = x => true;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddSlider<T>(int id, string name, string title, string tooltip,int min, int max, float incAmt,Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, int, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null) where T : IMyTerminalBlock
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

            CreateSliderActionSet<T>(s, name, id, min/100, max/100, incAmt, visibleGetter);
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
