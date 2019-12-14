using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using WeaponCore.Support;
using VRage.Utils;
using WeaponCore.Control;
using WeaponCore.Platform;
using static WeaponCore.Platform.Weapon.TerminalActionState;
using static WeaponCore.Platform.Weapon;

namespace WeaponCore
{
    public partial class Session
    {
        #region UI Config

        public void CreateTerminalUI<T>(Session session) where T : IMyTerminalBlock
        {
            try
            {
                string currentType = "";
                

                if (typeof(T) == typeof(IMyLargeTurretBase))
                    currentType = "Missile";

                TerminalHelpers.AlterActions<T>();
                TerminalHelpers.AlterControls<T>();


                if (typeof(T) == typeof(IMyConveyorSorter))
                {
                    TerminalHelpers.AddSlider<T>(-5, "Range", "Aiming Radius", "Range", 0, 100, 1, WepUi.GetRange, WepUi.SetRange, WepUi.CoreWeaponEnableCheck, WepUi.GetMinRange, WepUi.GetMaxRange);
                    currentType = "Sorter";
                }

                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlHandler;

                TerminalHelpers.Separator<T>(0, "WC_sep0");

                var wepIDs = new HashSet<int>();
                foreach (KeyValuePair<MyStringHash, WeaponStructure> wp in WeaponPlatforms)
                {
                    foreach (KeyValuePair<MyStringHash, WeaponSystem> ws in WeaponPlatforms[wp.Key].WeaponSystems)
                    {

                        Type type = null;
                        foreach (var def in AllDefinitions) {
                            if (def.Id.SubtypeId == wp.Key)
                                type = def.Id.TypeId;
                        }


                        if (type != null && type.ToString().Contains(currentType))
                        {

                            var wepName = ws.Value.WeaponName;
                            var wepID = ws.Value.WeaponId;

                            if (!wepIDs.Contains(wepID))
                                wepIDs.Add(wepID);
                            else
                                continue;

                            TerminalHelpers.AddWeaponOnOff<T>(wepID, wepName, $"Enable {wepName}", $"Enable {wepName}", "On ", "Off ", WeaponEnabled, EnableWeapon, TerminalHelpers.WeaponFunctionEnabled);
                            CreateShootActionSet<T>(wepName, wepID, session);
                        }
                    }
                }

                var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_Shoot_Click");
                action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
                action.Name = new StringBuilder($"Toggle Mouse Shoot");
                action.Action = delegate (IMyTerminalBlock blk) {
                    var comp = blk?.Components?.Get<WeaponComponent>();
                    if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    {
                        var w = comp.Platform.Weapons[i];
                        var wState = comp.State.Value.Weapons[w.WeaponId];
                        if (wState.ManualShoot == ShootClick)
                        {
                            wState.ManualShoot = ShootOff;
                            comp.MouseShoot = false;
                            comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
                            comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                        }
                        else if (wState.ManualShoot != ShootOff)
                        {
                            wState.ManualShoot = ShootClick;
                            comp.MouseShoot = true;
                        }
                        else
                        {
                            wState.ManualShoot = ShootClick;
                            comp.MouseShoot = true;
                            comp.Ai.ManualComps++;
                            comp.Shooting++;
                        }
                    }
                };
                action.Writer = (blk, t) => 
                {
                    var comp = blk?.Components?.Get<WeaponComponent>();
                    if (comp != null && comp.MouseShoot)
                        t.Append("On");
                    else
                        t.Append("Off");
                };
                action.Enabled = (b) => WepUi.CoreWeaponEnableCheck(b, 0);
                action.ValidForGroups = true;

                MyAPIGateway.TerminalControls.AddAction<T>(action);

                TerminalHelpers.Separator<T>(0, "WC_sep1");

                TerminalHelpers.AddWeaponOnOff<T>(-1, "Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", WepUi.GetGuidance, WepUi.SetGuidance, WepUi.CoreWeaponEnableCheck);

                
                TerminalHelpers.AddSlider<T>(-2, "WC_Damage", "Change Damage Per Shot", "Change Damage Per Shot", 1, 100, 0.1f, WepUi.GetDps, WepUi.SetDps, WepUi.CoreWeaponEnableCheck);

                TerminalHelpers.AddSlider<T>(-3, "WC_ROF", "Change Rate of Fire", "Change Rate of Fire", 1, 100, 0.1f, WepUi.GetRof, WepUi.SetRof, WepUi.CoreWeaponEnableCheck);

                TerminalHelpers.AddCheckbox<T>(-4, "Overload", "Overload Damage", "Overload Damage", WepUi.GetOverload, WepUi.SetOverload, WepUi.CoreWeaponEnableCheck);
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlerUi: {ex}"); }
        }

        internal bool WeaponEnabled(IMyTerminalBlock block, int wepID)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return false;

            var enabled = false;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                if (comp.Platform.Weapons[i].System.WeaponId == wepID)
                    enabled = comp.Set.Value.Weapons[i].Enable;
            }
            return enabled;
        }

        internal void EnableWeapon(IMyTerminalBlock block, int wepID, bool enabled)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready)
            {
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (comp.Platform.Weapons[i].System.WeaponId == wepID)
                    {
                        comp.Set.Value.Weapons[i].Enable = enabled;
                        comp.SettingsUpdated = true;
                        comp.ClientUiUpdate = true;
                    }

                }
            }
        }

        internal static void CreateShootActionSet<T>(string name, int id, Session session) where T : IMyTerminalBlock
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_On_Off");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"{name} Shoot On/Off");
            action.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (comp.Platform.Weapons[i].System.WeaponId == id)
                    {
                        var w = comp.Platform.Weapons[i];

                        var wState = comp.State.Value.Weapons[w.WeaponId];
                        if (wState.ManualShoot == ShootOn)
                        {
                            wState.ManualShoot = ShootOff;
                            w.StopShooting();
                            comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
                            comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                        }
                        else if (wState.ManualShoot != ShootOff)
                            wState.ManualShoot = ShootOn;
                        else
                        {
                            wState.ManualShoot = ShootOn;
                            comp.Ai.ManualComps++;
                            comp.Shooting++;
                        }
                    }
                }
            };
            action.Writer = (b, t) => t.Append(session.CheckWeaponManualState(b, id) ? "On" : "Off");
            action.Enabled = (b) => TerminalHelpers.WeaponFunctionEnabled(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_On");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"{name} Shoot On");
            action.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (comp.Platform.Weapons[i].System.WeaponId == id)
                    {
                        var wState = comp.State.Value.Weapons[comp.Platform.Weapons[i].WeaponId];

                        if (wState.ManualShoot != ShootOff)
                            wState.ManualShoot = ShootOn;
                        else
                        {
                            wState.ManualShoot = ShootOn;
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
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (comp.Platform.Weapons[i].System.WeaponId == id)
                    {
                        var w = comp.Platform.Weapons[i];
                        var wState = comp.State.Value.Weapons[w.WeaponId];

                        if (wState.ManualShoot != ShootOff)
                        {
                            wState.ManualShoot = ShootOff;
                            w.StopShooting();
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
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (comp.Platform.Weapons[i].System.WeaponId == id)
                    {
                        comp.State.Value.Weapons[comp.Platform.Weapons[i].WeaponId].ManualShoot = ShootOnce;
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

        internal bool CheckWeaponManualState(IMyTerminalBlock block, int weaponId)
        {
            var cube = (MyCubeBlock)block;
            var grid = cube.CubeGrid;
            GridAi gridAi;
            if (GridTargetingAIs.TryGetValue(grid, out gridAi))
            {
                WeaponComponent comp;
                if (gridAi.WeaponBase.TryGetValue(cube, out comp) && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready)
                {
                    var w = comp.Platform.Weapons[weaponId];
                    if (comp.State.Value.Weapons[w.WeaponId].ManualShoot != ShootOff)
                        return true;
                }
            }

            return false;
        }

        private void CustomControlHandler(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            var cube = (MyCubeBlock)block;

            GridAi gridAi;
            if (GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
            {
                gridAi.LastTerminal = block;

                WeaponComponent comp;
                if (gridAi.WeaponBase.TryGetValue(cube, out comp) && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready)
                {
                    gridAi.LastWeaponTerminal = block;
                    gridAi.WeaponTerminalAccess = true;
                }
            }
        }
        #endregion
    }
}