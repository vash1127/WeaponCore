using WepaonCore.Control;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using VRage.Utils;
using static WeaponCore.Platform.Weapon.TerminalActionState;

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
        internal IMyTerminalControlOnOffSwitch Guidance { get; set; }
        internal IMyTerminalControlCombobox WeaponMode { get; set; }
        internal IMyTerminalControlSlider PowerSlider { get; set; }
        internal IMyTerminalControlCheckbox DoubleRate { get; set; }
        public void CreateLogicElements()
        {
            try
            {
                if (WepControl) return;
                TerminalHelpers.Separator<IMyLargeTurretBase>(-1, "WC-L_sep0", WepUi.IsCoreWeapon, WepUi.IsCoreWeapon);

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
                                            tmpComp.Set.Value.Weapons[i].Enable = enabled;
                                    }
                                }
                            },
                            WepUi.EnableWeapon, WepUi.EnableWeapon);

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
                action.Enabled = (b) => true;
                action.ValidForGroups = true;

                MyAPIGateway.TerminalControls.AddAction<IMyLargeTurretBase>(action);
                /*TerminalHelpers.Separator(comp, -1, "WC-L_sep1", WepUi.GuidanceEnabled, WepUi.GuidanceEnabled);
                Guidance = TerminalHelpers.AddOnOff(comp, -1, "WC-L_Guidance", "Enable Guidance", "Enable Guidance", "On", "Off", WepUi.GetGuidance, WepUi.SetGuidance, WepUi.GuidanceEnabled, WepUi.GuidanceEnabled);

                
                PowerSlider = TerminalHelpers.AddSlider(comp, -1, "WC-L_PowerLevel", "Change Power Level", "Change Power Level", WepUi.GetPowerLevel, WepUi.SetPowerLevel, WepUi.IsCoreWeapon, WepUi.IsCoreWeapon);
                PowerSlider.SetLimits(0, 100);



                /*
                DoubleRate = TerminalHelpers.AddCheckbox(comp, -1, "WC-L_DoubleRate", "DoubleRate", "DoubleRate", WepUi.GetDoubleRate, WepUi.SetDoubleRate, WepUi.IsCoreWeapon, WepUi.IsCoreWeapon);
                CreateAction<IMyLargeTurretBase>(Guidance);
                */

                //CreateActionChargeRate<IMyLargeTurretBase>(PowerSlider);

                WepControl = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlerUi: {ex}"); }
        }


        private void WarheadSetter(IMyTerminalBlock tBlock, bool isSet)
        {
            var customData = tBlock.CustomData;
            var iOf = tBlock.CustomData.IndexOf("@EMP", StringComparison.Ordinal);
            if (isSet && iOf == -1)
            {
                if (customData.Length == 0) tBlock.CustomData = "@EMP";
                else if (!customData.Contains("@EMP")) tBlock.CustomData = customData + "\n@EMP";
                return;
            }

            if (iOf != -1)
            {
                if (iOf != 0)
                {
                    tBlock.CustomData = customData.Remove(iOf - 1, 5);
                }
                else
                {
                    if (customData.Length > 4 && customData.IndexOf("\n", StringComparison.Ordinal) == iOf + 4) tBlock.CustomData = customData.Remove(iOf, 5);
                    else tBlock.CustomData = customData.Remove(iOf, iOf + 4);
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
            action.Enabled = (b) => TerminalHelpers.ActionEnabled(id,b);
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
            action.Enabled = (b) => TerminalHelpers.ActionEnabled(id, b);
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
            action.Enabled = (b) => TerminalHelpers.ActionEnabled(id, b);
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
            action.Enabled = (b) => TerminalHelpers.ActionEnabled(id, b);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        internal static bool CheckWeaponManualState(IMyTerminalBlock blk, int id)
        {
            var comp = blk?.Components?.Get<WeaponComponent>();
            if (comp == null) return false;

            var isShootOn = false;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                if (comp.Platform.Weapons[i].System.WeaponID == id)
                    if (comp.Platform.Weapons[i].ManualShoot != ShootOff)
                        return true;
            }

            return isShootOn;
        }

        private void GetWeaponControls(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            var turret = block as IMyLargeTurretBase;

            if (controls.Count == 0 || turret == null) return;

            var comp = block?.Components?.Get<WeaponComponent>();

            for (int i = 0; i < controls.Count; i++)
            {
                if (controls[i].Id.Contains($"WC_"))
                    controls[i].Visible = (b) => false;
            }

            if (comp == null) return;


            HashSet<int> IDs = new HashSet<int>();

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                IDs.Add(comp.Platform.Weapons[i].System.WeaponID);
                Log.Line($"WepID: {comp.Platform.Weapons[i].WeaponId} WepName: {comp.Platform.Weapons[i].System.WeaponID}");
            }

            for (int i = 0; i < controls.Count; i++)
            {

                if ((i > 6 && i < 10) || (i > 12 && i < 22))
                    controls[i].Visible = tb => false;



                foreach (int id in IDs)
                {
                    if (controls[i].Id.Contains($"WC_{id}"))
                    {
                        controls[i].Visible = (b) => true;
                    }
                }
            }
        }

        private void GetWeaponActions(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            var turret = block as IMyLargeTurretBase;

            if (actions.Count == 0 || turret == null) return;

            var iterActions = new List<IMyTerminalAction>(actions);

            for (int i = 0; i < iterActions.Count; i++)
            {
                if (iterActions[i].Id.Contains($"WC_"))
                    actions.Remove(iterActions[i]);
            }

            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return;


            HashSet<int> IDs = new HashSet<int>();

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                IDs.Add(comp.Platform.Weapons[i].System.WeaponID);
            }

            for (int i = 0; i < iterActions.Count; i++)
            {

                if (!iterActions[i].Id.Contains($"OnOff") && !iterActions[i].Id.Contains($"WC_"))
                {
                    iterActions[i].Enabled = (b) =>false;
                    actions.Remove(iterActions[i]);
                }

                if (iterActions[i].Id == "WC_Shoot_Click")
                    actions.Add(iterActions[i]);


                foreach (int id in IDs)
                {
                    if (iterActions[i].Id.Contains($"WC_{id}"))
                    {
                        actions.Add(iterActions[i]);
                    }
                }
            }
        }
        #endregion
    }
}