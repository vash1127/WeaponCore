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
using Sandbox.Definitions;
using VRage.Game;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI.Interfaces;
using static WeaponCore.Platform.Weapon.TerminalActionState;

namespace WeaponCore
{
    public partial class Session
    {
        #region UI Config
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
                a.Writer = (block, builder) => {};
                a.Action = block => {};
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

        public static void CreateTerminalUi<T>(Session session) where T : IMyTerminalBlock
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

                    CreateShootClick<T>();

                    TerminalHelpers.AlterActions<T>();
                    TerminalHelpers.AlterControls<T>();

                    builderType = new MyObjectBuilder_LargeTurretBaseDefinition();
                }
                else if (typeof(T) == typeof(IMySmallMissileLauncher) || typeof(T) == typeof(IMySmallGatlingGun))
                {
                    if (!session.BaseControlsActions)
                    {
                        TerminalHelpers.AlterActions<IMyUserControllableGun>();
                        TerminalHelpers.AlterControls<IMyUserControllableGun>();
                        
                        session.BaseControlsActions = true;
                    }

                    CreateShootClick<T>();

                    builderType = new MyObjectBuilder_WeaponBlockDefinition();
                }
                else if (typeof(T) == typeof(IMyConveyorSorter))
                {
                    builderType = new MyObjectBuilder_ConveyorSorterDefinition();
                    TerminalHelpers.AlterActions<T>();
                    TerminalHelpers.AlterControls<T>();
                    TerminalHelpers.AddSlider<T>(-5, "Range", "Aiming Radius", "Range", 0, 100, 1, WepUi.GetRange, WepUi.SetRange, (b, i) => { var comp = b?.Components?.Get<WeaponComponent>(); return comp == null || comp.HasTurret; }, WepUi.GetMinRange, WepUi.GetMaxRange);

                    CreateShootClick<T>();

                }
                if (builderType == null) return;

                var wepIDs = new HashSet<int>();
                foreach (KeyValuePair<MyStringHash, WeaponStructure> wp in session.WeaponPlatforms)
                {
                    foreach (KeyValuePair<MyStringHash, WeaponSystem> ws in session.WeaponPlatforms[wp.Key].WeaponSystems)
                    {
                        MyDefinitionId defId;
                        MyDefinitionBase def = null;

                        if (session.ReplaceVanilla && session.VanillaCoreIds.TryGetValue(wp.Key, out defId))
                        {
                            if (!MyDefinitionManager.Static.TryGetDefinition(defId, out def)) return;
                        }
                        else
                        {
                            Type type = null;
                            foreach (var tmpdef in session.AllDefinitions)
                            {
                                if (tmpdef.Id.SubtypeId == wp.Key)
                                {
                                    type = tmpdef.Id.TypeId;
                                    def = tmpdef;
                                    break;
                                }
                            }
                            if (type == null) return;
                        }

                        var ob = def.GetObjectBuilder();
                        if (ob != null && builderType.GetType() == ob.GetType())
                        {
                            var wepName = ws.Value.WeaponName;
                            var wepId = ws.Value.WeaponId;

                            if (!wepIDs.Contains(wepId))
                                wepIDs.Add(wepId);
                            else
                                continue;
                            CreateShootActionSet<T>(wepName, wepId);
                        }
                    }
                }

            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlUi: {ex}"); }
        }

        internal static void CreateShootClick<T>()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_Shoot_Click");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Toggle Mouse Shoot");
            action.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

                /*if (comp.ClickShootAction == null || comp.shootAction == null)
                {
                    comp.ClickShootAction = action;
                    comp.shootAction = (IMyTerminalAction)MyAPIGateway.TerminalActionsHelper.GetActionWithName("Shoot", typeof(T));
                }*/
                var cState = comp.State.Value;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];

                    if (cState.ClickShoot)
                    {
                        w.State.ManualShoot = ShootOff;
                        cState.CurrentPlayerControl.PlayerId = -1;
                        cState.CurrentPlayerControl.CurrentControlType = ControlType.None;
                    }
                    else
                    {
                        w.State.ManualShoot = ShootClick;
                        cState.CurrentPlayerControl.PlayerId = comp.Session.Session.Player.IdentityId;
                        cState.CurrentPlayerControl.CurrentControlType = ControlType.Toolbar;
                    }
                }

                cState.ClickShoot = !cState.ClickShoot;
                cState.ShootOn = cState.ClickShoot ? false : cState.ShootOn;
                comp.UpdateStateMP();
            };

            action.Writer = (blk, sb) =>
            {
                var on = blk.Components.Get<WeaponComponent>()?.State.Value.ClickShoot ?? false;

                if (on)
                    sb.Append("On");
                else
                    sb.Append("Off");
            };

            action.Enabled = (b) =>
            {
                var comp = b?.Components?.Get<WeaponComponent>();
                return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready;
            };
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        internal static void CreateShootActionSet<T>(string name, int id) where T : IMyTerminalBlock
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
                    w.State.ManualShoot = ShootOff;
                else
                    w.State.ManualShoot = ShootOn;

                comp.UpdateStateMP();
            };
            action0.Writer = (b, t) => t.Append(CheckWeaponManualState(b, id) ? "On" : "Off");
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
                
                comp.State.Value.Weapons[comp.Platform.Weapons[weaponId].WeaponId].ManualShoot = ShootOn;
                comp.UpdateStateMP();

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

                var w = comp.Platform.Weapons[weaponId].State.ManualShoot = ShootOff;
                comp.UpdateStateMP();

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
                        var cState = comp.State.Value;
                        cState.Weapons[comp.Platform.Weapons[weaponId].WeaponId].ManualShoot = ShootOnce;
                        cState.Weapons[comp.Platform.Weapons[weaponId].WeaponId].SingleShotCounter++;
                        comp.UpdateStateMP();
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

        internal static bool CheckWeaponManualState(IMyTerminalBlock block, int weaponHash)
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