using System;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using WeaponCore.Support;
using WeaponCore.Platform;

namespace WeaponCore.Control
{
    public static class CreateCustomActions<T>
    {
        internal static void CreateShootClick(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_Shoot_Click");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Toggle Click To Fire");
            action.Action = CustomActions.TerminalActionShootClick;
            action.Writer = CustomActions.ClickShootWriter;
            action.Enabled = TerminalHelpers.IsReady;
            action.ValidForGroups = true;
            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateShootOnce(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"ShootOnce");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"Shoot Once");
            action.Action = CustomActions.TerminalActionShootOnce;
            action.Writer = TerminalHelpers.EmptyStringBuilder;
            action.Enabled = TerminalHelpers.ShootOnceWeapon;
            action.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateShoot(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Shoot");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Shoot On/Off");
            action.Action = CustomActions.TerminActionToggleShoot;
            action.Writer = CustomActions.ShootStateWriter;
            action.Enabled = TerminalHelpers.IsReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateDecoy(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Mask");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Select Mask Type");
            action.Action = CustomActions.TerminActionCycleDecoy;
            action.Writer = CustomActions.DecoyWriter;
            action.Enabled = TerminalHelpers.Istrue;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateShootOff(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Shoot_Off");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action.Name = new StringBuilder($"Shoot Off");
            action.Action = CustomActions.TerminalActionShootOff;
            action.Writer = CustomActions.ShootStateWriter;
            action.Enabled = TerminalHelpers.IsReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateShootOn(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Shoot_On");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"Shoot On");
            action.Action = CustomActions.TerminalActionShootOn;
            action.Writer = CustomActions.ShootStateWriter;
            action.Enabled = TerminalHelpers.IsReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateSubSystems(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"SubSystems");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Cycle SubSystems");
            action.Action = CustomActions.TerminActionCycleSubSystem;
            action.Writer = CustomActions.SubSystemWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateControlModes(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"ControlModes");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Control Mode");
            action.Action = CustomActions.TerminalActionControlMode;
            action.Writer = CustomActions.ControlStateWriter;
            action.Enabled = TerminalHelpers.TurretOrGuidedAmmo;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateNeutrals(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Neutrals");
            action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
            action.Name = new StringBuilder($"Neutrals On/Off");
            action.Action = CustomActions.TerminalActionToggleNeutrals;
            action.Writer = CustomActions.NeutralWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateProjectiles(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Projectiles");
            action.Icon = @"Textures\GUI\Icons\Actions\MissileToggle.dds";
            action.Name = new StringBuilder($"Projectiles On/Off");
            action.Action = CustomActions.TerminalActionToggleProjectiles;
            action.Writer = CustomActions.ProjectilesWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateBiologicals(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Biologicals");
            action.Icon = @"Textures\GUI\Icons\Actions\CharacterToggle.dds";
            action.Name = new StringBuilder($"Biologicals On/Off");
            action.Action = CustomActions.TerminalActionToggleBiologicals;
            action.Writer = CustomActions.BiologicalsWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateMeteors(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Meteors");
            action.Icon = @"Textures\GUI\Icons\Actions\MeteorToggle.dds";
            action.Name = new StringBuilder($"Meteors On/Off");
            action.Action = CustomActions.TerminalActionToggleMeteors;
            action.Writer = CustomActions.MeteorsWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateGrids(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Grids");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Grids On/Off");
            action.Action = CustomActions.TerminalActionToggleGrids;
            action.Writer = CustomActions.GridsWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateFriendly(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Friendly");
            action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
            action.Name = new StringBuilder($"Friendly On/Off");
            action.Action = CustomActions.TerminalActionToggleFriendly;
            action.Writer = CustomActions.FriendlyWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateUnowned(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Unowned");
            action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
            action.Name = new StringBuilder($"Unowned On/Off");
            action.Action = CustomActions.TerminalActionToggleUnowned;
            action.Writer = CustomActions.UnownedWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateFocusTargets(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"FocusTargets");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"FocusTargets On/Off");
            action.Action = CustomActions.TerminalActionToggleFocusTargets;
            action.Writer = CustomActions.FocusTargetsWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateFocusSubSystem(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"FocusSubSystem");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"FocusSubSystem On/Off");
            action.Action = CustomActions.TerminalActionToggleFocusSubSystem;
            action.Writer = CustomActions.FocusSubSystemWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        public static void CreateMaxSize(Session session)
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"MaxSize Increase");
            action0.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
            action0.Name = new StringBuilder($"MaxSize Increase");
            action0.Action = CustomActions.TerminalActionMaxSizeIncrease;
            action0.Writer = CustomActions.MaxSizeWriter;
            action0.Enabled = TerminalHelpers.HasTracking;
            action0.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);
            session.CustomActions.Add(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"MaxSize Decrease");
            action1.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            action1.Name = new StringBuilder($"MaxSize Decrease");
            action1.Action = CustomActions.TerminalActionMaxSizeDecrease;
            action1.Writer = CustomActions.MaxSizeWriter;
            action1.Enabled = TerminalHelpers.HasTracking;
            action1.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);
            session.CustomActions.Add(action1);
        }

        public static void CreateMinSize(Session session)
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"MinSize Increase");
            action0.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
            action0.Name = new StringBuilder($"MinSize Increase");
            action0.Action = CustomActions.TerminalActionMinSizeIncrease;
            action0.Writer = CustomActions.MinSizeWriter;
            action0.Enabled = TerminalHelpers.HasTracking;
            action0.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);
            session.CustomActions.Add(action0);
            
            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"MinSize Decrease");
            action1.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            action1.Name = new StringBuilder($"MinSize Decrease");
            action1.Action = CustomActions.TerminalActionMinSizeDecrease;
            action1.Writer = CustomActions.MinSizeWriter;
            action1.Enabled = TerminalHelpers.HasTracking;
            action1.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);
            session.CustomActions.Add(action1);
        }

        public static void CreateMovementState(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"TrackingMode");
            action.Icon = @"Textures\GUI\Icons\Actions\MovingObjectToggle.dds";
            action.Name = new StringBuilder($"Tracking Mode");
            action.Action = CustomActions.TerminalActionMovementMode;
            action.Writer = CustomActions.MovementModeWriter;
            action.Enabled = TerminalHelpers.HasTracking;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        internal static void CreateCycleAmmo(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_CycleAmmo");
            action.Icon = session.ModPath() + @"\Textures\GUI\Icons\Actions\Cycle_Ammo.dds";
            action.Name = new StringBuilder("Cycle Ammo");
            action.Action = CustomActions.TerminalActionCycleAmmo;
            action.Writer = CustomActions.AmmoSelectionWriter;
            action.Enabled = TerminalHelpers.AmmoSelection;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
            session.CustomActions.Add(action);
        }

        internal static void CreateOnOffActionSet(Session session, IMyTerminalControlOnOffSwitch tc, string name, int id, Func<IMyTerminalBlock, bool> enabler, bool group = false)
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle");
            action0.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action0.Name = new StringBuilder($"{name} Toggle On/Off");
            action0.Action = (b) => tc.Setter(b, !tc.Getter(b));
            action0.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action0.Enabled = enabler;
            action0.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);
            session.CustomActions.Add(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_On");
            action1.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action1.Name = new StringBuilder($"{name} On");
            action1.Action = (b) => tc.Setter(b, true);
            action1.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action1.Enabled = enabler;
            action1.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);
            session.CustomActions.Add(action1);

            var action2 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_Off");
            action2.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action2.Name = new StringBuilder($"{name} Off");
            action2.Action = (b) => tc.Setter(b, true);
            action2.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action2.Enabled = enabler;
            action2.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action2);
            session.CustomActions.Add(action2);

        }

        internal static void CreateOnOffActionSet(Session session, IMyTerminalControlCheckbox tc, string name, int id, Func<IMyTerminalBlock, bool> enabler, bool group = false)
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle");
            action0.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action0.Name = new StringBuilder($"{name} Toggle On/Off");
            action0.Action = (b) => tc.Setter(b, !tc.Getter(b));
            action0.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action0.Enabled = enabler;
            action0.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);
            session.CustomActions.Add(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_On");
            action1.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action1.Name = new StringBuilder($"{name} On");
            action1.Action = (b) => tc.Setter(b, true);
            action1.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action1.Enabled = enabler;
            action1.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);
            session.CustomActions.Add(action1);

            var action2 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_Off");
            action2.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action2.Name = new StringBuilder($"{name} Off");
            action2.Action = (b) => tc.Setter(b, true);
            action2.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action2.Enabled = enabler;
            action2.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action2);
            session.CustomActions.Add(action2);

        }

        internal static void CreateSliderActionSet(Session session, IMyTerminalControlSlider tc, string name, int id, int min, int max, float incAmt, Func<IMyTerminalBlock, bool> enabler, bool group)
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Increase");
            action0.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
            action0.Name = new StringBuilder($"Increase {name}");
            action0.Action = (b) => tc.Setter(b, tc.Getter(b) + incAmt <= max ? tc.Getter(b) + incAmt : max);
            action0.Writer = TerminalHelpers.EmptyStringBuilder;
            action0.Enabled = enabler;
            action0.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);
            session.CustomActions.Add(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Decrease");
            action1.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            action1.Name = new StringBuilder($"Decrease {name}");
            action1.Action = (b) => tc.Setter(b, tc.Getter(b) - incAmt >= min ? tc.Getter(b) - incAmt : min);
            action1.Writer = TerminalHelpers.EmptyStringBuilder;
            action1.Enabled = enabler;
            action1.ValidForGroups = group;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);
            session.CustomActions.Add(action1);
        }
    }
}
