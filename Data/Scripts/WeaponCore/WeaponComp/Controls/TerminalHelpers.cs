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
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef.EventTriggers;
namespace WeaponCore.Control
{
    public static class TerminalHelpers
    {
        internal static void AlterActions<T>()
        {
            var isTurretType = typeof(T) == typeof(IMyLargeTurretBase);

            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);
            for (int i = isTurretType ? 13 : 0; i < actions.Count; i++)
            {
                var a = actions[i];

                if (!a.Id.Contains("OnOff") && !a.Id.Equals("Shoot") && !a.Id.Equals("ShootOnce") && !a.Id.Contains("WC_") && !a.Id.Contains("Control"))
                    a.Enabled = b => !b.Components.Has<WeaponComponent>();

                /*else if(a.Id.Contains("Control"))
                    a.Enabled = b =>
                    {
                        var comp = b?.Components?.Get<WeaponComponent>();
                        return comp == null || comp.HasTurret;
                    };*/

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
                        var cState = comp.State.Value;

                        for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                        {
                            cState.Weapons[comp.Platform.Weapons[j].WeaponId].SingleShotCounter++;
                            cState.Weapons[comp.Platform.Weapons[j].WeaponId].ManualShoot = ShootOnce;
                        }

                        cState.ClickShoot = false;
                        cState.ShootOn = false;

                        if (comp.Session.HandlesInput && comp.Session.MpActive)
                        {
                            comp.State.Value.MId++;
                            comp.Session.PacketsToServer.Add(new ShootStatePacket
                            {
                                EntityId = blk.EntityId,
                                SenderId = comp.Session.MultiplayerId,
                                PType = PacketType.CompToolbarShootState,
                                MId = comp.State.Value.MId,
                                Data = ShootOnce,
                            });
                        }
                        //comp.UpdateStateMp();
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

                        var cState = comp.State.Value;

                        for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                        {
                            var w = comp.Platform.Weapons[j];

                            if (cState.ShootOn)
                                w.State.ManualShoot = ShootOff;
                            else
                                w.State.ManualShoot = ShootOn;
                        }

                        if (comp.Session.HandlesInput && comp.Session.MpActive)
                        {
                            comp.State.Value.MId++;
                            comp.Session.PacketsToServer.Add(new ShootStatePacket
                            {
                                EntityId = blk.EntityId,
                                SenderId = comp.Session.MultiplayerId,
                                PType = PacketType.CompToolbarShootState,
                                MId = comp.State.Value.MId,
                                Data = cState.ShootOn ? ShootOff : ShootOn,
                            });
                        }

                        cState.ShootOn = !cState.ShootOn;
                        cState.ClickShoot = cState.ShootOn ? false : cState.ClickShoot;

                        //comp.UpdateStateMp();
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
                        if (comp.State.Value.ShootOn)
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
                "Range",
            };

            for (int i = isTurretType ? 12 : 0; i < controls.Count; i++)
            {
                var c = controls[i];

                Log.Line($"typeof(T): {typeof(T)} id: {c.Id}");

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

                    case "OnOff":
                        {
                            ((IMyTerminalControlOnOffSwitch)c).Setter += (blk, On) =>
                            {
                                var comp = blk?.Components?.Get<WeaponComponent>();
                                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                                OnOffAnimations(comp, On);
                            };
                            break;
                        }

                    case "Range":
                        {
                            ((IMyTerminalControlSlider)c).Setter += (blk, Value) =>
                            {
                                var comp = blk?.Components?.Get<WeaponComponent>();
                                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

                                comp.Set.Value.Range = Value;                                

                                if (comp.Session.HandlesInput && comp.Session.MpActive)
                                {
                                    comp.Set.Value.MId++;
                                    comp.Session.PacketsToServer.Add(new RangePacket
                                    {
                                        EntityId = blk.EntityId,
                                        SenderId = comp.Session.MultiplayerId,
                                        PType = PacketType.RangeUpdate,
                                        MId = comp.Set.Value.MId,
                                        Data = Value,
                                    });
                                }

                            };
                            break;
                        }
                }  
            }

            if (!isTurretType)
            {
                Separator<T>(0, "WC_sep1");

                AddWeaponOnOff<T>(-1, "Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", WepUi.GetGuidance, WepUi.SetGuidance,
                    (block, i) =>
                    {
                        var comp = block?.Components?.Get<WeaponComponent>();
                        return comp != null && comp.HasGuidanceToggle;
                    });
                
                AddSlider<T>(-2, "WC_Damage", "Change Damage Per Shot", "Change Damage Per Shot", 1, 100, 0.1f, WepUi.GetDps, WepUi.SetDpsFromTerminal,
                    (block, i) =>
                    {
                        var comp = block?.Components?.Get<WeaponComponent>();
                        return comp != null && comp.HasDamageSlider;
                    });

                AddSlider<T>(-3, "WC_ROF", "Change Rate of Fire", "Change Rate of Fire", 1, 100, 0.1f, WepUi.GetRof, WepUi.SetRof,
                    (block, i) =>
                    {
                        var comp = block?.Components?.Get<WeaponComponent>();
                        return comp != null && comp.HasRofSlider;
                    } );

                AddCheckbox<T>(-4, "Overload", "Overload Damage", "Overload Damage", WepUi.GetOverload, WepUi.SetOverload, true,
                    (block, i) =>
                    {
                        var comp = block?.Components?.Get<WeaponComponent>();
                        return comp != null && comp.CanOverload;
                    });
            }
        }

        private static void OnOffAnimations(WeaponComponent comp, bool On)
        {            
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                if (!On)
                {
                    if (w.TurretMode)
                    {
                        var azSteps = w.Azimuth / w.System.AzStep;
                        var elSteps = w.Elevation / w.System.ElStep;

                        if (azSteps < 0) azSteps *= -1;
                        if (azSteps < 0) azSteps *= -1;

                        w.Timings.OffDelay = (uint)(azSteps + elSteps > 0 ? azSteps > elSteps ? azSteps : elSteps : 0);

                        if (!w.Comp.Session.IsClient) w.Target.Reset(comp.Session.Tick, Target.States.Expired);
                        w.TurretHomePosition();

                    }
                    w.StopShooting();

                    if (w.ActiveAmmoDef.Const.MustCharge && ((w.ActiveAmmoDef.Const.IsHybrid && w.State.Sync.CurrentAmmo != w.ActiveAmmoDef.Const.MagazineDef.Capacity) || (!w.ActiveAmmoDef.Const.IsHybrid && w.State.Sync.CurrentAmmo != w.ActiveAmmoDef.Const.EnergyMagSize)))
                    {
                        w.State.Sync.CurrentCharge = 0;
                        w.State.Sync.CurrentAmmo = 0;
                        w.State.Sync.Reloading = false;
                    }
                    //comp.State.Value.CurrentCharge += w.State.Sync.CurrentCharge;

                    uint delay;
                    if (w.System.WeaponAnimationLengths.TryGetValue(TurnOff, out delay))
                        w.Timings.AnimationDelayTick = w.Timings.ShootDelayTick = comp.Session.Tick + delay + w.Timings.OffDelay;
                }
                else
                {
                    w.Timings.OffDelay = 0;
                    uint delay;
                    if (w.System.WeaponAnimationLengths.TryGetValue(TurnOn, out delay))
                        w.Timings.AnimationDelayTick = w.Timings.ShootDelayTick = w.Timings.WeaponReadyTick = comp.Session.Tick + delay;

                    if (!w.ActiveAmmoDef.Const.EnergyAmmo || w.ActiveAmmoDef.Const.MustCharge)
                        Session.ComputeStorage(w);
                }

                
                if (w.Timings.AnimationDelayTick < comp.Session.Tick || w.LastEvent == TurnOn || w.LastEvent == TurnOff)
                {
                    w.EventTriggerStateChanged(TurnOn, On);
                    w.EventTriggerStateChanged(TurnOff, !On);
                }
                else
                {
                    comp.Session.FutureEvents.Schedule(o => 
                        {
                            w.EventTriggerStateChanged(TurnOn, On);
                            w.EventTriggerStateChanged(TurnOff, !On);
                        }, 
                        null, 
                        w.Timings.AnimationDelayTick - comp.Session.Tick
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
