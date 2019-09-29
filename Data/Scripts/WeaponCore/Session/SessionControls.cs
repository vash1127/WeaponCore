using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using WeaponCore.Support;
using VRage.Utils;
using static WeaponCore.Platform.Weapon.TerminalActionState;
using Sandbox.Game.Entities;
using WeaponCore.Control;

namespace WeaponCore
{
    public partial class Session
    {
        #region UI Config

        public void AlterControlsActions()
        {
            
        }

        public void CreateLogicElements(object o)
        {
            try
            {
                Log.Line("init");
                WasInited = true;
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlHandler;

                TerminalHelpers.AlterActions<IMyLargeTurretBase>();
                TerminalHelpers.AlterControls<IMyLargeTurretBase>();

                if (WepControl) return;
                TerminalHelpers.Separator<IMyLargeTurretBase>(0, "WC_sep0");

                var wepIDs = new HashSet<int>();

                foreach(KeyValuePair<MyStringHash, WeaponStructure> wp in WeaponPlatforms)
                {
                    foreach (KeyValuePair<MyStringHash, WeaponSystem> ws in WeaponPlatforms[wp.Key].WeaponSystems)
                    {
                        var wepName = ws.Value.WeaponName;
                        var wepID = ws.Value.WeaponId;

                        if (!wepIDs.Contains(wepID))
                            wepIDs.Add(wepID);
                        else
                            continue;

                        TerminalHelpers.AddWeaponOnOff<IMyLargeTurretBase>(wepID, wepName, $"Enable {wepName}", $"Enable {wepName}", "On ", "Off ", WeaponEnabled, EnableWeapon, TerminalHelpers.WeaponFunctionEnabled);

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
                        var w = comp.Platform.Weapons[i];
                        if (w.ManualShoot == ShootClick)
                        {
                            w.ManualShoot = ShootOff;
                            comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
                            comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                        }
                        else if(w.ManualShoot != ShootOff)
                            w.ManualShoot = ShootClick;
                        else
                        {
                            w.ManualShoot = ShootClick;
                            comp.Ai.ManualComps++;
                            comp.Shooting++;
                        }
                    }
                };
                action.Writer = (b, t) => t.Append("");
                action.Enabled = (b) => WepUi.CoreWeaponEnableCheck(b, 0);
                action.ValidForGroups = true;

                MyAPIGateway.TerminalControls.AddAction<IMyLargeTurretBase>(action);

                TerminalHelpers.Separator<IMyLargeTurretBase>(0, "WC_sep1");

                TerminalHelpers.AddWeaponOnOff<IMyLargeTurretBase>(-1, "Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", WepUi.GetGuidance, WepUi.SetGuidance, WepUi.CoreWeaponEnableCheck);

                
                TerminalHelpers.AddSlider<IMyLargeTurretBase>(-2, "Damage", "Change Damage Per Shot", "Change Damage Per Shot", 1, 100, 0.1f, WepUi.GetDps, WepUi.SetDps, WepUi.CoreWeaponEnableCheck);

                TerminalHelpers.AddSlider<IMyLargeTurretBase>(-3, "ROF", "Change Rate of Fire", "Change Rate of Fire", 1, 100, 0.1f, WepUi.GetRof, WepUi.SetRof, WepUi.CoreWeaponEnableCheck);

                TerminalHelpers.AddCheckbox<IMyLargeTurretBase>(-4, "Overload", "Overload Damage", "Overload Damage", WepUi.GetOverload, WepUi.SetOverload, WepUi.CoreWeaponEnableCheck);

                WepControl = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlerUi: {ex}"); }
        }

        internal bool WeaponEnabled(IMyTerminalBlock block, int wepID)
        {
            var tmpComp = block?.Components?.Get<WeaponComponent>();
            if (tmpComp == null || !tmpComp.Platform.Inited) return false;

            var enabled = false;
            for (int i = 0; i < tmpComp.Platform.Weapons.Length; i++)
            {
                if (tmpComp.Platform.Weapons[i].System.WeaponId == wepID)
                    enabled = tmpComp.Set.Value.Weapons[i].Enable;
            }
            return enabled;
        }

        internal void EnableWeapon(IMyTerminalBlock block, int wepID, bool enabled)
        {
            var tmpComp = block?.Components?.Get<WeaponComponent>();
            if (tmpComp != null && tmpComp.Platform.Inited)
            {
                for (int i = 0; i < tmpComp.Platform.Weapons.Length; i++)
                {
                    if (tmpComp.Platform.Weapons[i].System.WeaponId == wepID)
                    {
                        tmpComp.Set.Value.Weapons[i].Enable = enabled;
                        tmpComp.SettingsUpdated = true;
                        tmpComp.ClientUiUpdate = true;
                    }

                }
            }
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
                    if (comp.Platform.Weapons[i].System.WeaponId == id)
                    {
                        var w = comp.Platform.Weapons[i];
                        if (w.ManualShoot == ShootOn)
                        {
                            w.ManualShoot = ShootOff;
                            comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
                            comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                        }
                        else if (w.ManualShoot != ShootOff)
                            w.ManualShoot = ShootOn;
                        else
                        {
                            w.ManualShoot = ShootOn;
                            comp.Ai.ManualComps++;
                            comp.Shooting++;
                        }
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
                    if (comp.Platform.Weapons[i].System.WeaponId == id)
                    {
                        var w = comp.Platform.Weapons[i];
                        if(w.ManualShoot != ShootOff)
                            w.ManualShoot = ShootOn;
                        else
                        {
                            w.ManualShoot = ShootOn;
                            comp.Ai.ManualComps++;
                            comp.Shooting++;
                        }
                    }
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
                    if (comp.Platform.Weapons[i].System.WeaponId == id)
                    {
                        var w = comp.Platform.Weapons[i];
                        if (w.ManualShoot != ShootOff)
                        {
                            w.ManualShoot = ShootOff;
                            comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
                            comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                        }
                    }
                        
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
                    if (comp.Platform.Weapons[i].System.WeaponId == id)
                    {
                        comp.Platform.Weapons[i].ManualShoot = ShootOnce;
                        comp.Ai.ManualComps++;
                        comp.Shooting++;
                    }
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
                if (comp.Platform.Weapons[i].System.WeaponId == id)
                    if (comp.Platform.Weapons[i].ManualShoot != ShootOff)
                        return true;
            }

            return false;
        }

        private void CustomControlHandler(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            var turret = block as IMyLargeTurretBase;

            if (controls.Count == 0 || turret == null) return;

            if (turret != null) {
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