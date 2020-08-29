using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using WeaponCore.Support;
using VRage.Utils;
using WeaponCore.Control;
using WeaponCore.Platform;
using Sandbox.Definitions;
using VRage.Game;
using Sandbox.Common.ObjectBuilders;

namespace WeaponCore
{
    public partial class Session
    {
        #region UI Config
        internal static void CreateShootActionSet<T>() where T : IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateShoot();
            CreateCustomActions<T>.CreateShootOn();
            CreateCustomActions<T>.CreateShootOff();
            CreateCustomActions<T>.CreateShootOnce();
            CreateCustomActions<T>.CreateControl();
            CreateCustomActions<T>.CreateSubSystems();
            CreateCustomActions<T>.CreateNeutrals();
            CreateCustomActions<T>.CreateProjectiles();
            CreateCustomActions<T>.CreateBiologicals();
            CreateCustomActions<T>.CreateMeteors();
            CreateCustomActions<T>.CreateFriendly();
            CreateCustomActions<T>.CreateUnowned();
            CreateCustomActions<T>.CreateFocusTargets();
            CreateCustomActions<T>.CreateFocusSubSystem();
            CreateCustomActions<T>.CreateMaxSize();
            CreateCustomActions<T>.CreateMinSize();
        }

        private void CustomControlHandler(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            LastTerminal = block;

            var cube = (MyCubeBlock)block;
            GridAi gridAi;
            if (GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
            {
                gridAi.LastTerminal = block;
                WeaponComponent comp;
                if (gridAi.WeaponBase.TryGetValue(cube, out comp) && comp.Platform.State == MyWeaponPlatform.PlatformState.Ready)
                {
                    TerminalMon.HandleInputUpdate(comp);
                    int rangeControl = -1;
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
                }
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

                    CreateCustomActions<T>.CreateShootClick();
                    TerminalHelpers.AlterActions<T>();
                    TerminalHelpers.AlterControls<T>();

                    obs.Add(new MyObjectBuilder_LargeMissileTurret().GetType());
                    obs.Add(new MyObjectBuilder_InteriorTurret().GetType());
                    obs.Add(new MyObjectBuilder_LargeGatlingTurret().GetType());
                }
                else if (typeof(T) == typeof(IMySmallMissileLauncher) || typeof(T) == typeof(IMySmallGatlingGun) || typeof(T) == typeof(IMySmallMissileLauncherReload))
                {

                    if (!session.BaseControlsActions)
                    {
                        TerminalHelpers.AlterActions<IMyUserControllableGun>();
                        TerminalHelpers.AlterControls<IMyUserControllableGun>();
                        TerminalHelpers.AddUiControls<T>();
                        session.BaseControlsActions = true;
                    }

                    CreateCustomActions<T>.CreateShootClick();

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
                    CreateCustomActions<T>.CreateShootClick();
                }

                TerminalHelpers.AddSlider<T>(-5, "WC_Range", "Aiming Radius", "Range", WepUi.GetRange, WepUi.RequestSetRange, WepUi.ShowRange, WepUi.GetMinRange, WepUi.GetMaxRange);

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

                            if (type == null)
                                return;
                        }

                        try
                        {

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
                                    if (ws.Value.AmmoTypes.Length > 1)
                                    {

                                        var c = 0;
                                        for (int i = 0; i < ws.Value.AmmoTypes.Length; i++)
                                        {
                                            if (ws.Value.AmmoTypes[i].AmmoDef.Const.IsTurretSelectable)
                                                c++;
                                        }

                                        if (c > 1)
                                            CreateCustomActions<T>.CreateCycleAmmoOptions(wepName, wepIdHash, session.ModPath());
                                    }
                                }
                            }
                        }
                        catch (Exception e) { Log.Line($"Keen Broke it: {e}"); }
                    }
                }

            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlUi: {ex}"); }
        }

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
                a.Writer = (block, builder) => { };
                a.Action = block => { };
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
        #endregion
    }
}