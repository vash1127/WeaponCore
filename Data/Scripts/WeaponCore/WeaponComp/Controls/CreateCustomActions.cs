using System.Text;
using Sandbox.ModAPI;
using WeaponCore.Support;
using WeaponCore.Platform;

namespace WeaponCore.Control
{
    public static class CreateCustomActions<T>
    {
        internal static void CreateShootClick()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_Shoot_Click");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Toggle Click To Fire");
            action.Action = CustomActions.TerminalActionShootClick;
            action.Writer = CustomActions.ClickShootWriter;
            action.Enabled = TerminalHelpers.IsReady;
            action.ValidForGroups = true;
            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateShootOnce()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"ShootOnce");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"Shoot Once");
            action.Action = CustomActions.TerminalActionShootOnce;
            action.Writer = (b, t) => t.Append("");
            action.Enabled = TerminalHelpers.IsReady;
            action.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateShoot()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Shoot");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Shoot On/Off");
            action.Action = CustomActions.TerminActionToggleShoot;
            action.Writer = CustomActions.ShootStateWriter;
            action.Enabled = TerminalHelpers.IsReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateShootOff()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Shoot_Off");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action.Name = new StringBuilder($"Shoot Off");
            action.Action = CustomActions.TerminalActionShootOff;
            action.Writer = CustomActions.ShootStateWriter;
            action.Enabled = TerminalHelpers.IsReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateShootOn()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Shoot_On");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"Shoot On");
            action.Action = CustomActions.TerminalActionShootOn;
            action.Writer = CustomActions.ShootStateWriter;
            action.Enabled = TerminalHelpers.IsReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateSubSystems()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"SubSystems");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Cycle SubSystems");
            action.Action = CustomActions.TerminActionCycleSubSystem;
            action.Writer = CustomActions.SubSystemWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateControlModes()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"ControlModes");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Control Mode");
            action.Action = CustomActions.TerminalActionControlMode;
            action.Writer = CustomActions.ControlStateWriter;
            action.Enabled = TerminalHelpers.TurretOrGuidedAmmo;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateNeutrals()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Neutrals");
            action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
            action.Name = new StringBuilder($"Neutrals On/Off");
            action.Action = CustomActions.TerminalActionToggleNeutrals;
            action.Writer = CustomActions.NeutralWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateProjectiles()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Projectiles");
            action.Icon = @"Textures\GUI\Icons\Actions\MissileToggle.dds";
            action.Name = new StringBuilder($"Projectiles On/Off");
            action.Action = CustomActions.TerminalActionToggleProjectiles;
            action.Writer = CustomActions.ProjectilesWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateBiologicals()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Biologicals");
            action.Icon = @"Textures\GUI\Icons\Actions\CharacterToggle.dds";
            action.Name = new StringBuilder($"Biologicals On/Off");
            action.Action = CustomActions.TerminalActionToggleBiologicals;
            action.Writer = CustomActions.BiologicalsWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateMeteors()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Meteors");
            action.Icon = @"Textures\GUI\Icons\Actions\MeteorToggle.dds";
            action.Name = new StringBuilder($"Meteors On/Off");
            action.Action = CustomActions.TerminalActionToggleMeteors;
            action.Writer = CustomActions.MeteorsWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateFriendly()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Friendly");
            action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
            action.Name = new StringBuilder($"Friendly On/Off");
            action.Action = CustomActions.TerminalActionToggleFriendly;
            action.Writer = CustomActions.FriendlyWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateUnowned()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Unowned");
            action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
            action.Name = new StringBuilder($"Unowned On/Off");
            action.Action = CustomActions.TerminalActionToggleUnowned;
            action.Writer = CustomActions.UnownedWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateFocusTargets()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"FocusTargets");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"FocusTargets On/Off");
            action.Action = CustomActions.TerminalActionToggleFocusTargets;
            action.Writer = CustomActions.FocusTargetsWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateFocusSubSystem()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"FocusSubSystem");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"FocusSubSystem On/Off");
            action.Action = CustomActions.TerminalActionToggleFocusSubSystem;
            action.Writer = CustomActions.FocusSubSystemWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateMaxSize()
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"MaxSize Increase");
            action0.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
            action0.Name = new StringBuilder($"MaxSize Increase");
            action0.Action = CustomActions.TerminalActionMaxSizeIncrease;
            action0.Writer = CustomActions.MaxSizeWriter;
            action0.Enabled = TerminalHelpers.HasTurret;
            action0.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"MaxSize Decrease");
            action1.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            action1.Name = new StringBuilder($"MaxSize Decrease");
            action1.Action = CustomActions.TerminalActionMaxSizeDecrease;
            action1.Writer = CustomActions.MaxSizeWriter;
            action1.Enabled = TerminalHelpers.HasTurret;
            action1.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);
        }

        public static void CreateMinSize()
        {
            var action0 = MyAPIGateway.TerminalControls.CreateAction<T>($"MinSize Increase");
            action0.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
            action0.Name = new StringBuilder($"MinSize Increase");
            action0.Action = CustomActions.TerminalActionMinSizeIncrease;
            action0.Writer = CustomActions.MinSizeWriter;
            action0.Enabled = TerminalHelpers.HasTurret;
            action0.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"MinSize Decrease");
            action1.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            action1.Name = new StringBuilder($"MinSize Decrease");
            action1.Action = CustomActions.TerminalActionMinSizeDecrease;
            action1.Writer = CustomActions.MinSizeWriter;
            action1.Enabled = TerminalHelpers.HasTurret;
            action1.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);
        }

        public static void CreateMovementState()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"TrackingMode");
            action.Icon = @"Textures\GUI\Icons\Actions\MovingObjectToggle.dds";
            action.Name = new StringBuilder($"Tracking Mode");
            action.Action = CustomActions.TerminalActionMovementMode;
            action.Writer = CustomActions.MovementModeWriter;
            action.Enabled = TerminalHelpers.HasTurret;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        internal static void CreateCycleAmmo(Session session)
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_CycleAmmo");
            action.Icon = session.ModPath() + @"\Textures\GUI\Icons\Actions\Cycle_Ammo.dds";
            action.Name = new StringBuilder("Cycle Ammo");
            action.Action = CustomActions.TerminalActionCycleAmmoNew;
            action.Writer = CustomActions.AmmoSelectionWriter;
            action.Enabled = TerminalHelpers.AmmoSelection;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }
    }
}
