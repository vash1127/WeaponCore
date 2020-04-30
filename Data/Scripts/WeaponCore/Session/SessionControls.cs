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
using static WeaponCore.Platform.Weapon.ManualShootActionState;
using Sandbox.Common.ObjectBuilders;
using VRage.ObjectBuilders;

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
                var obs = new HashSet<Type>();

                if (typeof(T) == typeof(IMyLargeTurretBase))
                {
                    if (!session.BaseControlsActions)
                    {
                        TerminalHelpers.AlterActions<IMyUserControllableGun>();
                        TerminalHelpers.AlterControls<IMyUserControllableGun>();
                        TerminalHelpers.AddUiControls<T>();
                        session.BaseControlsActions = true;
                    }

                    CreateShootClick<T>();

                    TerminalHelpers.AlterActions<T>();
                    TerminalHelpers.AlterControls<T>();

                    obs.Add(new MyObjectBuilder_LargeMissileTurret().GetType());
                    obs.Add(new MyObjectBuilder_InteriorTurret().GetType());
                    obs.Add(new MyObjectBuilder_LargeGatlingTurret().GetType());
                }
                else if (typeof(T) == typeof(IMySmallMissileLauncher) || typeof(T) == typeof(IMySmallGatlingGun))
                {
                    if (!session.BaseControlsActions)
                    {
                        TerminalHelpers.AlterActions<IMyUserControllableGun>();
                        TerminalHelpers.AlterControls<IMyUserControllableGun>();
                        TerminalHelpers.AddUiControls<T>();
                        session.BaseControlsActions = true;
                    }

                    CreateShootClick<T>();


                    obs.Add(new MyObjectBuilder_SmallMissileLauncher().GetType());
                    obs.Add(new MyObjectBuilder_SmallMissileLauncherReload().GetType());
                    obs.Add(new MyObjectBuilder_SmallGatlingGun().GetType());
                }
                else if (typeof(T) == typeof(IMyConveyorSorter))
                {
                    obs.Add(new MyObjectBuilder_ConveyorSorter().GetType());
                    TerminalHelpers.AlterActions<T>();
                    TerminalHelpers.AlterControls<T>();
                    TerminalHelpers.AddUiControls<T>();
                    CreateShootActionSet<T>();
                    CreateShootClick<T>();
                }

                TerminalHelpers.AddSlider<T>(-5, "WC_Range", "Aiming Radius", "Range", WepUi.GetRange, WepUi.SetRange, WepUi.ShowRange, WepUi.GetMinRange, WepUi.GetMaxRange);

                if (obs.Count == 0) return;

                var wepIDs = new HashSet<int>();
                foreach (KeyValuePair<MyStringHash, WeaponStructure> wp in session.WeaponPlatforms)
                {
                    foreach (KeyValuePair<MyStringHash, WeaponSystem> ws in session.WeaponPlatforms[wp.Key].WeaponSystems)
                    {
                        MyDefinitionId defId;
                        MyDefinitionBase def = null;

                        Type type = null;
                        if (session.ReplaceVanilla && session.VanillaCoreIds.TryGetValue(wp.Key, out defId))
                        {
                            if (!MyDefinitionManager.Static.TryGetDefinition(defId, out def)) return;
                            type = defId.TypeId;
                        }
                        else
                        {                            
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

                        try
                        {
                            //var ob = def.GetObjectBuilder();
                            if (obs.Contains(type))
                            {
                                var wepName = ws.Value.WeaponName;
                                var wepIdHash = ws.Value.WeaponIdHash;

                                if (!wepIDs.Contains(wepIdHash))
                                    wepIDs.Add(wepIdHash);
                                else
                                    continue;
                                if (!ws.Value.DesignatorWeapon)
                                {
                                    if (ws.Value.WeaponAmmoTypes.Length > 1)
                                    {
                                        var c = 0;
                                        for(int i = 0; i < ws.Value.WeaponAmmoTypes.Length; i++)
                                        {
                                            if (ws.Value.WeaponAmmoTypes[i].AmmoDef.HardPointUsable)
                                                c++;
                                        }
                                        if(c > 1)
                                            CreateCycleAmmoOptions<T>(wepName, wepIdHash, session.ModPath());

                                        if (ws.Value.TurretMovement != WeaponSystem.TurretType.Fixed && obs.Contains(new MyObjectBuilder_ConveyorSorter().GetType()))
                                        {
                                            Log.Line("create");
                                            CreateControlButton<T>(ws.Value.WeaponName, wepIdHash);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Line($"Keen Broke it: {e}");
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
            action.Name = new StringBuilder($"Toggle Click To Fire");
            action.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                
                TerminalHelpers.WCShootClickAction(comp, !(comp.State?.Value.ClickShoot ?? false), comp.HasTurret);
            };
            action.Writer = (blk, sb) =>
            {
                var on = blk.Components.Get<WeaponComponent>()?.State?.Value.ClickShoot ?? false;

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

        internal static void CreateShootActionSet<T>() where T : IMyTerminalBlock
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"Shoot");
            action0.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action0.Name = new StringBuilder($"Shoot On/Off");
            action0.Action = delegate (IMyTerminalBlock blk) 
            {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                    return;

                TerminalHelpers.WCShootToggleAction(comp);
            };
            action0.Writer = (blk, sb) => 
            {
                var comp = blk.Components.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                if (comp.State.Value.ShootOn)
                    sb.Append("On");
                else
                    sb.Append("Off");
            };
            action0.Enabled = (b) =>
            {
                return b.Components.Has<WeaponComponent>();
            };
            action0.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"Shoot_On");
            action1.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action1.Name = new StringBuilder($"Shoot On");
            action1.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                    return;

                TerminalHelpers.WCShootOnAction(comp);

            };
            action1.Writer = (blk, sb) =>
            {
                var comp = blk.Components.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                if (comp.State.Value.ShootOn)
                    sb.Append("On");
                else
                    sb.Append("Off");
            };
            action1.Enabled = (b) =>
            {
                return b.Components.Has<WeaponComponent>();
            };
            action1.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);

            var action2 = MyAPIGateway.TerminalControls.CreateAction<T>($"Shoot_Off");
            action2.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action2.Name = new StringBuilder($"Shoot Off");
            action2.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                    return;

                TerminalHelpers.WCShootOffAction(comp);

            };
            action2.Writer = (blk, sb) =>
            {
                var comp = blk.Components.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                if (comp.State.Value.ShootOn)
                    sb.Append("On");
                else
                    sb.Append("Off");
            };
            action2.Enabled = (b) =>
            {
                return b.Components.Has<WeaponComponent>();
            };
            action2.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action2);

            var action3 = MyAPIGateway.TerminalControls.CreateAction<T>($"ShootOnce");
            action3.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action3.Name = new StringBuilder($"Shoot Once");
            action3.Action = delegate (IMyTerminalBlock blk) {
                var comp = blk?.Components?.Get<WeaponComponent>();
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

                TerminalHelpers.WCShootOnceAction(comp);
            };
            action3.Writer = (b, t) => t.Append("");
            action3.Enabled = (b) =>
            {
                return b.Components.Has<WeaponComponent>();
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
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId) || comp.Platform.Weapons[weaponId].System.WeaponIdHash != id) return;
                try
                {
                    var w = comp.Platform.Weapons[weaponId];

                    var availAmmo = w.System.WeaponAmmoTypes.Length;
                    // cant use w.ActiveAmmoDef as it may not have reloaded yet
                    var currActive = w.System.WeaponAmmoTypes[w.Set.AmmoTypeId]; 
                    var next = (w.Set.AmmoTypeId + 1) % availAmmo;
                    var currDef = w.System.WeaponAmmoTypes[next];

                    var change = false;

                    while (!(currActive.Equals(currDef)))
                    {
                        if (currDef.AmmoDef.Const.IsTurretSelectable)
                        { 
                            w.Set.AmmoTypeId = next;

                            if (comp.Session.MpActive)
                                comp.Session.SendCycleAmmoNetworkUpdate(w, next);

                            change = true;

                            break;
                        }

                        next = (next + 1) % availAmmo;
                        currDef = w.System.WeaponAmmoTypes[next];
                    }

                    if (change)
                        comp.Session.FutureEvents.Schedule(w.CycleAmmo, null, 1);

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

                return comp.Platform.Weapons[weaponId].System.WeaponIdHash == id;
            };

            action0.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);
        }

        internal static void CreateControlButton<T>(string name, int id)
        {
            var button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("WC_Control_" + id);

            button.Title = MyStringId.GetOrCompute("Control");
            button.Visible = (b) => 
            {
                var comp = b?.Components?.Get<WeaponComponent>();
                int weaponId;
                return comp != null && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready && comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId) && comp.Platform.Weapons[weaponId].System.WeaponIdHash == id;
            };

            button.Action = (b) =>
            {
                var comp = b?.Components?.Get<WeaponComponent>();
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId) || comp.Platform.Weapons[weaponId].System.WeaponIdHash != id) return;

                var w = comp.Platform.Weapons[weaponId];

                TerminalHelpers.WCCameraShootAction(w);

            };

            MyAPIGateway.TerminalControls.AddControl<T>(button);

            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_Control_{id}");
            action0.Name = new StringBuilder($"{name} Control");
            action0.Action = button.Action;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);
            /*if (WeaponCamActive)
            {
                if (WeaponCamera == null)
                    CameraGrid = Spawn.SpawnCamera("SpyCam", out WeaponCamera);

                if (WeaponCamera != null)
                    WeaponCamera.SetView();
            }
            else
            {
                MyAPIGateway.Session.SetCameraController(VRage.Game.MyCameraControllerEnum.Entity, ActiveControlBlock, null);
            }*/
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
                    List<IMyTerminalControl> controlsToReAdd = new List<IMyTerminalControl>();

                    HashSet<string> idCheck = new HashSet<string>();
                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                        idCheck.Add($"WC_Control_{comp.Platform.Weapons[i].System.WeaponIdHash}");

                    int rangeControl = -1;
                    int cDataControl = -1;
                    IMyTerminalControl wcRangeControl = null;
                    for (int i = controls.Count - 1; i >= 0; i--)
                    {
                        if (controls[i].Id.Equals("Range"))
                        {
                            rangeControl = i;
                            controls.RemoveAt(i);
                        }
                        else if (controls[i].Id.Equals("WC_Range"))
                        {
                            wcRangeControl = controls[i];
                            controls.RemoveAt(i);
                        }
                        else if (controls[i].Id.Contains("WC_Control_"))
                        {
                            if (idCheck.Contains(controls[i].Id))
                                controlsToReAdd.Add(controls[i]);

                            controls.RemoveAtFast(i);
                        }
                        else if (controls[i].Id.Equals("CustomData"))
                            cDataControl = i + 1;
                        
                    }

                    if (rangeControl != -1)
                        controls.RemoveAt(rangeControl);

                    if (wcRangeControl != null)
                    {
                        if (rangeControl != -1)
                            controls.Insert(rangeControl, wcRangeControl);

                        else
                            controls.Add(wcRangeControl);
                    }

                    for (int i = 0; i < controlsToReAdd.Count; i++)
                    {
                        controls.Insert(cDataControl, controlsToReAdd[i]);
                        cDataControl++;
                    }
                }
            }
        }
        #endregion
    }
}