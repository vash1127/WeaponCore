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
                            if (ws.Value.WeaponAmmoTypes.Length > 1)
                                CreateCycleAmmoOptions<T>(wepName, wepId, session.ModPath());
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

                var cState = comp.State.Value;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];

                    if (cState.ClickShoot)
                    {
                        w.State.ManualShoot = ShootOff;
                        cState.CurrentPlayerControl.PlayerId = -1;
                        cState.CurrentPlayerControl.ControlType = ControlType.None;
                    }
                    else
                    {
                        w.State.ManualShoot = ShootClick;
                        cState.CurrentPlayerControl.PlayerId = comp.Session.PlayerId;
                        cState.CurrentPlayerControl.ControlType = ControlType.Toolbar;
                    }
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
                        Data = cState.ClickShoot ? ShootOff : ShootClick,
                    });
                }

                comp.SendControlingPlayer();

                cState.ClickShoot = !cState.ClickShoot;
                cState.ShootOn = !cState.ClickShoot && cState.ShootOn;

                

                //comp.UpdateStateMp();
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
                {
                    w.State.ManualShoot = ShootOn;

                    var update = comp.Set.Value.Overrides.ManualControl || comp.Set.Value.Overrides.TargetPainter;
                    comp.Set.Value.Overrides.ManualControl = false;
                    comp.Set.Value.Overrides.TargetPainter = false;

                    if (update && comp.Session.MpActive)
                    {
                        comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                        comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
                        comp.SendControlingPlayer();
                        comp.SendOverRides();
                    }
                }

                
                if(comp.Session.HandlesInput && comp.Session.MpActive)
                {
                    comp.State.Value.MId++;
                    comp.Session.PacketsToServer.Add(new WeaponShootStatePacket
                    {
                        EntityId = blk.EntityId,
                        SenderId = comp.Session.MultiplayerId,
                        MId = comp.State.Value.MId,
                        PType = PacketType.WeaponToolbarShootState,
                        Data = w.State.ManualShoot,
                        WeaponId = w.WeaponId,
                    });
                }

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

                var w = comp.Platform.Weapons[weaponId];

                comp.State.Value.Weapons[weaponId].ManualShoot = ShootOn;

                var update = comp.Set.Value.Overrides.ManualControl || comp.Set.Value.Overrides.TargetPainter;
                comp.Set.Value.Overrides.ManualControl = false;
                comp.Set.Value.Overrides.TargetPainter = false;

                if (update && comp.Session.MpActive)
                {
                    comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                    comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
                    comp.SendControlingPlayer();
                    comp.SendOverRides();
                }

                if (comp.Session.HandlesInput && comp.Session.MpActive)
                {
                    comp.State.Value.MId++;
                    comp.Session.PacketsToServer.Add(new WeaponShootStatePacket
                    {
                        EntityId = blk.EntityId,
                        SenderId = comp.Session.MultiplayerId,
                        MId = comp.State.Value.MId,
                        PType = PacketType.WeaponToolbarShootState,
                        Data = w.State.ManualShoot,
                        WeaponId = w.WeaponId,
                    });
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

                comp.Platform.Weapons[weaponId].State.ManualShoot = ShootOff;

                if (comp.Session.HandlesInput && comp.Session.MpActive)
                {
                    var w = comp.Platform.Weapons[weaponId];
                    comp.State.Value.MId++;
                    comp.Session.PacketsToServer.Add(new WeaponShootStatePacket
                    {
                        EntityId = blk.EntityId,
                        SenderId = comp.Session.MultiplayerId,
                        MId = comp.State.Value.MId,
                        PType = PacketType.WeaponToolbarShootState,
                        Data = w.State.ManualShoot,
                        WeaponId = w.WeaponId,
                    });
                }

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
                        
                        if (comp.Session.HandlesInput && comp.Session.MpActive)
                        {
                            comp.State.Value.MId++;
                            comp.Session.PacketsToServer.Add(new WeaponShootStatePacket
                            {
                                EntityId = blk.EntityId,
                                SenderId = comp.Session.MultiplayerId,
                                MId = comp.State.Value.MId,
                                PType = PacketType.WeaponToolbarShootState,
                                Data = comp.Platform.Weapons[weaponId].State.ManualShoot,
                                WeaponId = weaponId,
                            });
                        }
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

        internal static void CreateCycleAmmoOptions<T>(string name, int id, string path) where T : IMyTerminalBlock
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_CycleAmmo");
            action0.Icon = path + @"\Textures\GUI\Icons\Actions\Cycle_Ammo.dds";
            action0.Name = new StringBuilder($"{name} Cycle Ammo");
            action0.Action = delegate (IMyTerminalBlock blk)
            {
                var comp = blk?.Components?.Get<WeaponComponent>();
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId) || comp.Platform.Weapons[weaponId].System.WeaponId != id) return;
                try
                {
                    var w = comp.Platform.Weapons[weaponId];


                    var availAmmo = w.System.WeaponAmmoTypes.Length;
                    var next = (w.Set.AmmoTypeId + 1) % availAmmo;
                    var currDef = w.System.WeaponAmmoTypes[next];

                    while (!(currDef.Equals(w.ActiveAmmoDef)))
                    {

                        if (currDef.AmmoDef.Const.IsTurretSelectable)
                        {
                            //w.ActiveAmmoDef = currDef;
                            w.Set.AmmoTypeId = next;
                            break;
                        }

                        next = (next + 1) % availAmmo;
                        currDef = w.System.WeaponAmmoTypes[next];
                    }

                    WepUi.SetDps(comp, comp.Set.Value.DpsModifier);
                }
                catch (Exception e)
                {
                    Log.Line($"Broke the Unbreakable, its dead Jim: {e}");
                }
            };
            action0.Writer = (b, t) =>
            {
                var comp = b?.Components?.Get<WeaponComponent>();
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId))
                {
                    t.Append("0");
                    return;
                }

                t.Append(comp.Platform.Weapons[weaponId].ActiveAmmoDef.AmmoDef.AmmoRound);
            };
            action0.Enabled = (b) =>
            {
                var comp = b?.Components?.Get<WeaponComponent>();
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId)) return false;

                return comp.Platform.Weapons[weaponId].System.WeaponId == id;
            };

            action0.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);
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
                    gridAi.LastWeaponTerminal = block;
                    gridAi.WeaponTerminalAccess = true;
                }
            }
        }
        #endregion
    }
}