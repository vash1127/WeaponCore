using WepaonCore.Control;
using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using WeaponCore.Support;
using VRage.Utils;
using static WeaponCore.Platform.Weapon.TerminalActionState;
using Sandbox.Game.Entities;

namespace WeaponCore
{
    public partial class Session
    {
        #region UI Config
        public static void AppendConditionToAction<T>(Func<IMyTerminalAction, bool> actionFindCondition, Func<IMyTerminalAction, IMyTerminalBlock, bool> actionEnabledAppend)
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);

            foreach (var a in actions)
            {
                if (actionFindCondition(a))
                {
                    var existingAction = a.Enabled;

                    a.Enabled = (b) => (existingAction == null ? true : existingAction.Invoke(b)) && actionEnabledAppend(a, b);
                }
            }
        }

        public void CreateLogicElements()
        {
            try
            {
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlHandler;

                TerminalHelpers.AlterActions<IMyLargeTurretBase>();
                TerminalHelpers.AlterControls<IMyLargeTurretBase>();

                if (WepControl) return;
                TerminalHelpers.Separator<IMyLargeTurretBase>(0, "WC_sep0");

                foreach(KeyValuePair<MyStringHash, WeaponStructure> wp in WeaponPlatforms)
                {
                    foreach (KeyValuePair<MyStringHash, WeaponSystem> ws in WeaponPlatforms[wp.Key].WeaponSystems)
                    {
                        var wepName = ws.Value.WeaponName;
                        var wepID = ws.Value.WeaponID;

                        TerminalHelpers.AddWeaponOnOff<IMyLargeTurretBase>(wepID, wepName, $"Enable {wepName}", $"Enable {wepName}", "On ", "Off ",
                            delegate (IMyTerminalBlock block)
                            {
                                var tmpComp = block?.Components?.Get<WeaponComponent>();
                                if (tmpComp == null) return false;

                                var enabled = false;
                                for(int i =0; i < tmpComp.Platform.Weapons.Length; i++)
                                {
                                    if (tmpComp.Platform.Weapons[i].System.WeaponID == wepID)
                                        enabled = tmpComp.Set.Value.Weapons[i].Enable;
                                }
                                return enabled;
                            },
                            delegate (IMyTerminalBlock block, bool enabled)
                            {
                                var tmpComp = block?.Components?.Get<WeaponComponent>();
                                if (tmpComp != null)
                                {
                                    for (int i = 0; i < tmpComp.Platform.Weapons.Length; i++)
                                    {
                                        if (tmpComp.Platform.Weapons[i].System.WeaponID == wepID)
                                        {
                                            tmpComp.Set.Value.Weapons[i].Enable = enabled;
                                            tmpComp.SettingsUpdated = true;
                                            tmpComp.ClientUiUpdate = true;
                                        }

                                    }
                                }
                            },
                            TerminalHelpers.WeaponFunctionEnabled);

                        CreateShootActionSet<IMyLargeTurretBase>(wepName, wepID);
                    }
                }

                var action = MyAPIGateway.TerminalControls.CreateAction<IMyLargeTurretBase>($"WC_Shoot_Click");
                action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
                action.Name = new StringBuilder($"Activate Mouse Shoot");
                action.Action = delegate (IMyTerminalBlock blk) {
                    var comp = blk?.Components?.Get<WeaponComponent>();
                    if (comp == null) return;
                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    {
                        comp.Platform.Weapons[i].ManualShoot = comp.Platform.Weapons[i].ManualShoot != ShootClick ? ShootClick : ShootOff;
                    }
                };
                action.Writer = (b, t) => t.Append("");
                action.Enabled = (b) => WepUi.CoreWeaponEnableCheck(b, 0);
                action.ValidForGroups = true;

                MyAPIGateway.TerminalControls.AddAction<IMyLargeTurretBase>(action);

                TerminalHelpers.Separator<IMyLargeTurretBase>(0, "WC_sep1");

                TerminalHelpers.AddWeaponOnOff<IMyLargeTurretBase>(-1, "Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", WepUi.GetGuidance, WepUi.SetGuidance, WepUi.CoreWeaponEnableCheck);

                
                TerminalHelpers.AddSlider<IMyLargeTurretBase>( -2, "Damage", "Change Damage Per Shot", "Change Damage Per Shot", 1, 100, 0.1f, WepUi.GetDPS, WepUi.SetDPS, WepUi.CoreWeaponEnableCheck);

                TerminalHelpers.AddSlider<IMyLargeTurretBase>(-3, "ROF", "Change Rate of Fire", "Change Rate of Fire", 1, 100, 0.1f, WepUi.GetROF, WepUi.SetROF, WepUi.CoreWeaponEnableCheck);

                TerminalHelpers.AddCheckbox<IMyLargeTurretBase>(-4, "Overload", "Overload Damage", "Overload Damage", WepUi.GetOverload, WepUi.SetOverload, WepUi.CoreWeaponEnableCheck);

                /*
                DoubleRate = TerminalHelpers.AddCheckbox(comp, -1, "WC-L_DoubleRate", "DoubleRate", "DoubleRate", WepUi.GetDoubleRate, WepUi.SetDoubleRate, WepUi.IsCoreWeapon, WepUi.IsCoreWeapon);
                CreateAction<IMyLargeTurretBase>(Guidance);
                */

                //CreateActionChargeRate<IMyLargeTurretBase>(PowerSlider);

                WepControl = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlerUi: {ex}"); }
        }

        internal static void CreateShootActionSet<T>(string name, int id) where T : IMyTerminalBlock
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_On_Off");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"{name} Shoot On/Off");
            action.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (comp.Platform.Weapons[i].System.WeaponID == id)
                    {
                        if (comp.Platform.Weapons[i].ManualShoot != ShootOn)
                            comp.Platform.Weapons[i].ManualShoot = ShootOn;
                        else
                            comp.Platform.Weapons[i].ManualShoot = ShootOff;
                    }
                }
            };
            action.Writer = (b, t) => t.Append(CheckWeaponManualState(b, id) ? "On" : "Off");
            action.Enabled = (b) => TerminalHelpers.WeaponFunctionEnabled(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_On");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"{name} Shoot On");
            action.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (comp.Platform.Weapons[i].System.WeaponID == id)
                        comp.Platform.Weapons[i].ManualShoot = ShootOn;
                }
            };
            action.Writer = (b, t) => t.Append("On");
            action.Enabled = (b) => TerminalHelpers.WeaponFunctionEnabled(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_Off");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action.Name = new StringBuilder($"{name} Shoot Off");
            action.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (comp.Platform.Weapons[i].System.WeaponID == id)
                        comp.Platform.Weapons[i].ManualShoot = ShootOff;
                }
            };
            action.Writer = (b, t) => t.Append("Off");
            action.Enabled = (b) => TerminalHelpers.WeaponFunctionEnabled(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_Once");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action.Name = new StringBuilder($"{name} Shoot Once");
            action.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (comp.Platform.Weapons[i].System.WeaponID == id)
                        comp.Platform.Weapons[i].ManualShoot = ShootOnce;
                }
            };
            action.Writer = (b, t) => t.Append("");
            action.Enabled = (b) => TerminalHelpers.WeaponFunctionEnabled(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        internal static bool CheckWeaponManualState(IMyTerminalBlock blk, int id)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null) return false;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                if (comp.Platform.Weapons[i].System.WeaponID == id)
                    if (comp.Platform.Weapons[i].ManualShoot != ShootOff)
                        return true;
            }

            return false;
        }

        private void CustomControlHandler(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            var cockpit = block as MyCockpit;
            var turret = block as IMyLargeTurretBase;

            if (controls.Count == 0 || cockpit == null && turret == null) return;

            if (ControlledEntity == cockpit && UpdateLocalAiAndCockpit())
            {
                var gridAi = GridTargetingAIs[cockpit.CubeGrid];
                gridAi.turnWeaponShootOff = true;
            }
            else if (turret != null) {
                var comp = turret?.Components?.Get<WeaponComponent>();
                if (comp != null)
                {
                    comp.TerminalRefresh();
                }
            }
            
        }
        #endregion
    }
}