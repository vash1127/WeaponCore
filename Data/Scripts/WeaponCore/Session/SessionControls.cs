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
using Sandbox.Definitions;
using VRage.Game;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;

namespace WeaponCore
{
    public partial class Session
    {
        #region UI Config

        public void CreateTerminalUi<T>(Session session) where T : IMyTerminalBlock
        {
            try
            {
                object builderType = null;

                if (typeof(T) == typeof(IMyLargeTurretBase))
                {
                    if (!session.BaseControlsActions)
                    {
                        TerminalHelpers.AlterActions<IMyUserControllableGun>();
                        TerminalHelpers.AlterControls<IMyUserControllableGun>();
                        session.BaseControlsActions = true;
                    }

                    TerminalHelpers.AlterActions<T>();
                    TerminalHelpers.AlterControls<T>();
                    builderType = new MyObjectBuilder_LargeTurretBaseDefinition();
                }
                else if (typeof(T) == typeof(IMyUserControllableGun))
                {
                    if (!session.BaseControlsActions)
                    {
                        TerminalHelpers.AlterActions<T>();
                        TerminalHelpers.AlterControls<T>();
                        session.BaseControlsActions = true;
                    }
                    builderType = new MyObjectBuilder_WeaponBlockDefinition();
                }
                else if (typeof(T) == typeof(IMyConveyorSorter))
                {
                    builderType = new MyObjectBuilder_ConveyorSorterDefinition();
                    TerminalHelpers.AlterActions<T>();
                    TerminalHelpers.AlterControls<T>();
                    TerminalHelpers.AddSlider<T>(-5, "Range", "Aiming Radius", "Range", 0, 100, 1, WepUi.GetRange, WepUi.SetRange, (b, i) => { var comp = b?.Components?.Get<WeaponComponent>(); return comp == null || comp.HasTurret; }, WepUi.GetMinRange, WepUi.GetMaxRange);

                    
                }

                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlHandler;

                TerminalHelpers.Separator<T>(0, "WC_sep0");

                var wepIDs = new HashSet<int>();
                foreach (KeyValuePair<MyStringHash, WeaponStructure> wp in WeaponPlatforms)
                {
                    foreach (KeyValuePair<MyStringHash, WeaponSystem> ws in WeaponPlatforms[wp.Key].WeaponSystems)
                    {

                        MyDefinitionId defId;
                        MyDefinitionBase def = null;
                        Type type = null;

                        if (ReplaceVanilla && vanillaCoreIds.TryGetValue(wp.Key, out defId))
                        {
                            type = defId.TypeId;
                            def = MyDefinitionManager.Static.GetDefinition(defId);
                        }
                        else
                        {
                            foreach (var tmpdef in AllDefinitions)
                            {
                                if (tmpdef.Id.SubtypeId == wp.Key)
                                {
                                    type = tmpdef.Id.TypeId;
                                    def = tmpdef;
                                    break;
                                }
                            }
                        }

                        if (def == null || type == null) return;

                        MyObjectBuilder_DefinitionBase ob = null;

                        try
                        {
                            
                            ob = def.GetObjectBuilder();
                        }
                        catch (Exception e)
                        {
                            //Log.Line($"Type: {wp.Key}");
                            Log.Line($"Error In create controls : {e.StackTrace}");
                        }
                        if (builderType.GetType() == ob.GetType())
                        {
                            var wepName = ws.Value.WeaponName;
                            var wepID = ws.Value.WeaponId;

                            if (!wepIDs.Contains(wepID))
                                wepIDs.Add(wepID);
                            else
                                continue;
                            CreateShootActionSet<T>(wepName, wepID, session);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlUi: {ex}"); }
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

                if (w.State.ManualShoot == ShootOn)
                {
                    w.State.ManualShoot = ShootOff;
                    if (w.IsShooting)
                        w.StopShooting();
                    else if (w.DrawingPower && !w.System.MustCharge)
                        w.StopPowerDraw();

                    if (w.System.MustCharge)
                    {
                        if(w.State.CurrentAmmo != w.System.EnergyMagSize)
                            w.State.CurrentAmmo = 0;
                    }

                    //comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
                }
                else if (w.State.ManualShoot != ShootOff) w.State.ManualShoot = ShootOn;
                else
                {
                    w.State.ManualShoot = ShootOn;
                    //comp.Ai.ManualComps++;
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
                    //comp.Ai.ManualComps++;
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

                if (w.State.ManualShoot == ShootOff) return;

                w.State.ManualShoot = ShootOff;
                if (w.IsShooting)
                    w.StopShooting();
                else if (w.DrawingPower && !w.System.MustCharge)
                    w.StopPowerDraw();
                else if (w.System.MustCharge)
                {
                    if (w.State.CurrentAmmo != w.System.EnergyMagSize)
                        w.State.CurrentAmmo = 0;
                }

                //comp.Ai.ManualComps = comp.Ai.ManualComps - 1 > 0 ? comp.Ai.ManualComps - 1 : 0;
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
                        if (comp.State.Value.Weapons[comp.Platform.Weapons[weaponId].WeaponId].ManualShoot != ShootOff) return;
                        comp.State.Value.Weapons[comp.Platform.Weapons[weaponId].WeaponId].ManualShoot = ShootOnce;
                        //comp.Ai.ManualComps++;
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
                    if (weaponHash == w.System.WeaponId && w.State.ManualShoot != ShootOff)
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
                    if (!comp.Session.DedicatedServer)
                        comp.TerminalRefresh();

                    gridAi.LastWeaponTerminal = block;
                    gridAi.WeaponTerminalAccess = true;
                }
            }
        }
        #endregion
    }
}