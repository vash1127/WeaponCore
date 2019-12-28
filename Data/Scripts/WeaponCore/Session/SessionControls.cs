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
                    TerminalHelpers.AddSlider<T>(-5, "Range", "Aiming Radius", "Range", 0, 100, 1, WepUi.GetRange, WepUi.SetRange, (b, i) => { var comp = b?.Components?.Get<WeaponComponent>(); return comp == null || comp.HasTurret; }, WepUi.GetMinRange, WepUi.GetMaxRange);
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

                            TerminalHelpers.AddWeaponOnOff<T>(wepID, wepName, $"Enable {wepName}", $"Enable {wepName}", "On ", "Off ", WeaponEnabled, EnableWeapon,
                                (block, i) =>
                                {
                                    var comp = block?.Components?.Get<WeaponComponent>();
                                    if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return false;
                                    int weaponId;
                                    if (!comp.Platform.Structure.HashToId.TryGetValue(i, out weaponId)) return false;
                                    return comp.Platform.Weapons[weaponId].System.WeaponId == i;
                                } );
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
                action.Enabled = (b) =>
                {
                    var comp = b?.Components?.Get<WeaponComponent>();
                    return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready;
                };
                action.ValidForGroups = true;

                MyAPIGateway.TerminalControls.AddAction<T>(action);

                TerminalHelpers.Separator<T>(0, "WC_sep1");

                TerminalHelpers.AddWeaponOnOff<T>(-1, "Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", WepUi.GetGuidance, WepUi.SetGuidance,
                    (block, i) =>
                    {
                        var comp = block?.Components?.Get<WeaponComponent>();
                        return comp != null && comp.HasGuidanceToggle;
                    });
                
                TerminalHelpers.AddSlider<T>(-2, "WC_Damage", "Change Damage Per Shot", "Change Damage Per Shot", 1, 100, 0.1f, WepUi.GetDps, WepUi.SetDps,
                    (block, i) =>
                    {
                        var comp = block?.Components?.Get<WeaponComponent>();
                        return comp != null && comp.HasDamageSlider;
                    });

                TerminalHelpers.AddSlider<T>(-3, "WC_ROF", "Change Rate of Fire", "Change Rate of Fire", 1, 100, 0.1f, WepUi.GetRof, WepUi.SetRof,
                    (block, i) =>
                    {
                        var comp = block?.Components?.Get<WeaponComponent>();
                        return comp != null && comp.HasRofSlider;
                    } );

                TerminalHelpers.AddCheckbox<T>(-4, "Overload", "Overload Damage", "Overload Damage", WepUi.GetOverload, WepUi.SetOverload, true,
                    (block, i) =>
                    {
                        var comp = block?.Components?.Get<WeaponComponent>();
                        return comp != null && comp.CanOverload;
                    });
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlerUi: {ex}"); }
        }

        internal bool WeaponEnabled(IMyTerminalBlock block, int weaponHash)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return false;
            
            int weaponId;
            if (!comp.Platform.Structure.HashToId.TryGetValue(weaponHash, out weaponId)) return false;
            return comp.Platform.Weapons[weaponId].System.WeaponId == weaponHash && comp.Set.Value.Weapons[weaponId].Enable;
        }

        internal void EnableWeapon(IMyTerminalBlock block, int weaponHash, bool enabled)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready)
            {
                int weaponId;
                if (comp.Platform.Structure.HashToId.TryGetValue(weaponHash, out weaponId))
                {
                    if (comp.Platform.Weapons[weaponId].System.WeaponId == weaponHash)
                    {
                        comp.Set.Value.Weapons[weaponId].Enable = enabled;
                        comp.SettingsUpdated = true;
                        comp.ClientUiUpdate = true;
                    }
                }
            }
        }

        internal static void CreateShootActionSet<T>(string name, int id, Session session) where T : IMyTerminalBlock
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_On_Off");
            action0.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action0.Name = new StringBuilder($"{name} Shoot On/Off");
            action0.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId) || comp.Platform.Weapons[weaponId].System.WeaponId != id) return;
                
                var w = comp.Platform.Weapons[weaponId];

                var wState = comp.State.Value.Weapons[w.WeaponId];
                if (wState.ManualShoot == ShootOn)
                {
                    wState.ManualShoot = ShootOff;
                    if (w.IsShooting)
                        w.StopShooting();
                    else if (w.DrawingPower && !w.System.MustCharge)
                        w.StopPowerDraw();

                    if (w.System.MustCharge)
                    {
                        if(w.Comp.State.Value.Weapons[w.WeaponId].CurrentAmmo != w.System.EnergyMagSize)
                            w.Comp.State.Value.Weapons[w.WeaponId].CurrentAmmo = 0;
                    }

                    comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
                    comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                }
                else if (wState.ManualShoot != ShootOff) wState.ManualShoot = ShootOn;
                else
                {
                    wState.ManualShoot = ShootOn;
                    comp.Ai.ManualComps++;
                    comp.Shooting++;
                }
            };
            action0.Writer = (b, t) => t.Append(session.CheckWeaponManualState(b, id) ? "On" : "Off");
            action0.Enabled = (b) =>
            {
                var comp = b?.Components?.Get<WeaponComponent>();
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId)) return false;

                return comp.Platform.Weapons[weaponId].System.WeaponId == id;
            };

            action0.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_On");
            action1.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action1.Name = new StringBuilder($"{name} Shoot On");
            action1.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId) || comp.Platform.Weapons[weaponId].System.WeaponId != id) return;
                
                var wState = comp.State.Value.Weapons[comp.Platform.Weapons[weaponId].WeaponId];

                if (wState.ManualShoot != ShootOff) wState.ManualShoot = ShootOn;
                else
                {
                    wState.ManualShoot = ShootOn;
                    comp.Ai.ManualComps++;
                    comp.Shooting++;
                }
            };
            action1.Writer = (b, t) => t.Append("On");
            action1.Enabled = (b) =>
            {
                var comp = b?.Components?.Get<WeaponComponent>();
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId)) return false;
                
                return comp.Platform.Weapons[weaponId].System.WeaponId == id;
            };
            action1.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);

            var action2 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_Off");
            action2.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action2.Name = new StringBuilder($"{name} Shoot Off");
            action2.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();

                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId) || comp.Platform.Weapons[weaponId].System.WeaponId != id) return;

                var w = comp.Platform.Weapons[weaponId];
                var wState = comp.State.Value.Weapons[w.WeaponId];

                if (wState.ManualShoot == ShootOff) return;

                wState.ManualShoot = ShootOff;
                if (w.IsShooting)
                    w.StopShooting();
                else if (w.DrawingPower && !w.System.MustCharge)
                    w.StopPowerDraw();
                else if (w.System.MustCharge)
                {
                    if (w.Comp.State.Value.Weapons[w.WeaponId].CurrentAmmo != w.System.EnergyMagSize)
                        w.Comp.State.Value.Weapons[w.WeaponId].CurrentAmmo = 0;
                }

                comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
                comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
            };
            action2.Writer = (b, t) => t.Append("Off");
            action2.Enabled = (b) =>
            {

                var comp = b?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return false;
                int weaponId;
                if (comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId))
                {
                    if (comp.Platform.Weapons[weaponId].System.WeaponId == id)
                        return true;
                }
                return false;
            };
            action2.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action2);

            var action3 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Shoot_Once");
            action3.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action3.Name = new StringBuilder($"{name} Shoot Once");
            action3.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

                int weaponId;
                if (comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId))
                {
                    if (comp.Platform.Weapons[weaponId].System.WeaponId == id)
                    {
                        comp.State.Value.Weapons[comp.Platform.Weapons[weaponId].WeaponId].ManualShoot = ShootOnce;
                        comp.Ai.ManualComps++;
                        comp.Shooting++;
                    }
                }
            };
            action3.Writer = (b, t) => t.Append("");
            action3.Enabled = (b) =>
            {

                var comp = b?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return false;
                int weaponId;
                if (comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId))
                {
                    if (comp.Platform.Weapons[weaponId].System.WeaponId == id)
                        return true;
                }
                return false;
            };
            action3.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action3);
        }

        internal bool CheckWeaponManualState(IMyTerminalBlock block, int weaponHash)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready)
            {
                int weaponId;
                if (comp.Platform.Structure.HashToId.TryGetValue(weaponHash, out weaponId))
                {
                    var w = comp.Platform.Weapons[weaponId];
                    if (weaponHash == w.System.WeaponId && comp.State.Value.Weapons[w.WeaponId].ManualShoot != ShootOff)
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
                    if (!comp.Ai.Session.DedicatedServer)
                        comp.TerminalRefresh();

                    gridAi.LastWeaponTerminal = block;
                    gridAi.WeaponTerminalAccess = true;
                }
            }
        }
        #endregion
    }
}