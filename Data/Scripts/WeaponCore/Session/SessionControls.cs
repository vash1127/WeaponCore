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
using Sandbox.Game;
using VRage.Input;

namespace WeaponCore
{
    public partial class Session
    {
        #region UI Config

        public void AlterControlsActions()
        {
            
        }

        public void CreateLogicElements()
        {
            try
            {
                if (Controls) return;
                Controls = true;
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlHandler;

                TerminalHelpers.AlterActions<IMyUpgradeModule>();
                TerminalHelpers.AlterControls<IMyUpgradeModule>();

                if (WepControl) return;

                TerminalHelpers.Separator<IMyUpgradeModule>(0, "WC_sep0");

                var wepIDs = new HashSet<int>();
                List<IMyTerminalControlButton> controlButtons = new List<IMyTerminalControlButton>();
                List<IMyTerminalControlOnOffSwitch> enableSwitches = new List<IMyTerminalControlOnOffSwitch>();

                foreach (KeyValuePair<MyStringHash, WeaponStructure> wp in WeaponPlatforms)
                {
                    foreach (KeyValuePair<MyStringHash, WeaponSystem> ws in WeaponPlatforms[wp.Key].WeaponSystems)
                    {
                        var wepName = ws.Value.WeaponName;
                        var wepID = ws.Value.WeaponId;

                        if (!wepIDs.Contains(wepID))
                            wepIDs.Add(wepID);
                        else
                            continue;

                        controlButtons.Add(TerminalHelpers.AddButton<IMyUpgradeModule>(wepID, $"Control {wepName}", "Control", "Control", TerminalHelpers.WeaponFunctionEnabled, EnableManualControl));

                        enableSwitches.Add(TerminalHelpers.AddWeaponOnOff<IMyUpgradeModule>(wepID, wepName, $"Enable {wepName}", $"Enable {wepName}", "On ", "Off ", WeaponEnabled, EnableWeapon, TerminalHelpers.WeaponFunctionEnabled));
                        CreateShootActionSet<IMyUpgradeModule>(wepName, wepID);
                    }
                }

                for (int i = 0; i < controlButtons.Count; i++)
                    MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(controlButtons[i]);

                TerminalHelpers.Separator<IMyUpgradeModule>(0, "WC_sep0");

                for (int i = 0; i < enableSwitches.Count; i++)
                    MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(enableSwitches[i]);

                var action = MyAPIGateway.TerminalControls.CreateAction<IMyUpgradeModule>($"WC_Shoot_Click");
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

                MyAPIGateway.TerminalControls.AddAction<IMyUpgradeModule>(action);

                TerminalHelpers.Separator<IMyUpgradeModule>(0, "WC_sep1");

                TerminalHelpers.AddWeaponOnOff<IMyUpgradeModule>(-1, "Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", WepUi.GetGuidance, WepUi.SetGuidance, WepUi.CoreWeaponEnableCheck);

                
                TerminalHelpers.AddSlider<IMyUpgradeModule>(-2, "Damage", "Change Damage Per Shot", "Change Damage Per Shot", 1, 100, 0.1f, WepUi.GetDps, WepUi.SetDps, WepUi.CoreWeaponEnableCheck);

                TerminalHelpers.AddSlider<IMyUpgradeModule>(-3, "ROF", "Change Rate of Fire", "Change Rate of Fire", 1, 100, 0.1f, WepUi.GetRof, WepUi.SetRof, WepUi.CoreWeaponEnableCheck);

                TerminalHelpers.AddCheckbox<IMyUpgradeModule>(-4, "Overload", "Overload Damage", "Overload Damage", WepUi.GetOverload, WepUi.SetOverload, WepUi.CoreWeaponEnableCheck);

                WepControl = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlerUi: {ex}"); }
        }

        private static void EnableManualControl(IMyTerminalBlock blk, int id)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                if (w.System.WeaponId == id)
                {
                    Instance.ControlingWeaponCam = true;
                    comp.Controlling = true;

                    Instance.ControlledWeapon = w;

                    var weaponCamera = Instance.WeaponCamera;
                    var weaponCameraGrid = Instance.WeaponCameraGrid;

                    var cameraMatrix = w.ElevationPart.Item1.PositionComp.WorldMatrix;
                    cameraMatrix.Translation += (cameraMatrix.Up * 1.2);
                    cameraMatrix.Translation += (-cameraMatrix.Forward * 1.2);
                    weaponCamera.DisplayNameText = w.System.WeaponName;

                    weaponCameraGrid.PositionComp.WorldMatrix = cameraMatrix;
                    weaponCamera.PositionComp.WorldMatrix = cameraMatrix;

                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(MyControlsSpace.ROTATION_DOWN.String, MyAPIGateway.Session.Player.IdentityId, false);

                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(MyControlsSpace.ROTATION_LEFT.String, MyAPIGateway.Session.Player.IdentityId, false);

                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(MyControlsSpace.ROTATION_RIGHT.String, MyAPIGateway.Session.Player.IdentityId, false);

                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(MyControlsSpace.ROTATION_UP.String, MyAPIGateway.Session.Player.IdentityId, false);

                    comp.Ai.GridMatrix = comp.MyGrid.PositionComp.WorldMatrix;

                }
            }
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
            var turret = block as IMyUpgradeModule;

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