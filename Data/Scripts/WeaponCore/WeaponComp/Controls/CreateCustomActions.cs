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
            action.Enabled = CustomActions.CompReady;
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
            action.Enabled = CustomActions.CompReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateControl()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"ControlMode");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Control Mode");
            action.Action = CustomActions.TerminActionControlMode;
            action.Writer = CustomActions.ControlStateWriter;
            action.Enabled = CustomActions.CompReady;
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
            action.Enabled = CustomActions.CompReady;
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
            action.Enabled = CustomActions.CompReady;
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
            action.Enabled = CustomActions.CompReady;
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
            action.Enabled = CustomActions.CompReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateNeutrals()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Neutrals");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Neutrals On/Off");
            action.Action = CustomActions.TerminalActionToggleNeutrals;
            action.Writer = CustomActions.NeutralWriter;
            action.Enabled = CustomActions.CompReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateProjectiles()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Projectiles");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Projectiles On/Off");
            action.Action = CustomActions.TerminalActionToggleProjectiles;
            action.Writer = CustomActions.ProjectilesWriter;
            action.Enabled = CustomActions.CompReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateBiologicals()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Biologicals");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Biologicals On/Off");
            action.Action = CustomActions.TerminalActionToggleBiologicals;
            action.Writer = CustomActions.BiologicalsWriter;
            action.Enabled = CustomActions.CompReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateMeteors()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Meteors");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Meteors On/Off");
            action.Action = CustomActions.TerminalActionToggleMeteors;
            action.Writer = CustomActions.MeteorsWriter;
            action.Enabled = CustomActions.CompReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateFriendly()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Friendly");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Friendly On/Off");
            action.Action = CustomActions.TerminalActionToggleFriendly;
            action.Writer = CustomActions.FriendlyWriter;
            action.Enabled = CustomActions.CompReady;
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        public static void CreateUnowned()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"Unowned");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"Unowned On/Off");
            action.Action = CustomActions.TerminalActionToggleUnowned;
            action.Writer = CustomActions.UnownedWriter;
            action.Enabled = CustomActions.CompReady;
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
            action.Enabled = CustomActions.CompReady;
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
            action.Enabled = CustomActions.CompReady;
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
            action0.Enabled = CustomActions.CompReady;
            action0.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"MaxSize Decrease");
            action1.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            action1.Name = new StringBuilder($"MaxSize Decrease");
            action1.Action = CustomActions.TerminalActionMaxSizeDecrease;
            action1.Writer = CustomActions.MaxSizeWriter;
            action1.Enabled = CustomActions.CompReady;
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
            action0.Enabled = CustomActions.CompReady;
            action0.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action0);

            var action1 = MyAPIGateway.TerminalControls.CreateAction<T>($"MinSize Decrease");
            action1.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            action1.Name = new StringBuilder($"MinSize Decrease");
            action1.Action = CustomActions.TerminalActionMinSizeDecrease;
            action1.Writer = CustomActions.MinSizeWriter;
            action1.Enabled = CustomActions.CompReady;
            action1.ValidForGroups = false;

            MyAPIGateway.TerminalControls.AddAction<T>(action1);
        }

        internal static void CreateCycleAmmoOptions(string name, int id, string path) 
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_CycleAmmo");
            action.Icon = path + @"\Textures\GUI\Icons\Actions\Cycle_Ammo.dds";
            action.Name = new StringBuilder($"{name} Cycle Ammo");
            action.Action = (b) => CustomActions.TerminalActionCycleAmmo(b, id);
            action.Writer = (b, t) =>
            {
                //cant create method call as it would require 2, this is checked every tick
                var comp = b?.Components?.Get<WeaponComponent>();
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId))
                {
                    t.Append("0");
                    return;
                }

                t.Append(comp.Platform.Weapons[weaponId].ActiveAmmoDef.AmmoDef.AmmoRound);
            };
            action.Enabled = (b) =>
            {
                //cant create method call as it would require 2, this is checked every tick
                var comp = b?.Components?.Get<WeaponComponent>();
                int weaponId;
                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !comp.Platform.Structure.HashToId.TryGetValue(id, out weaponId)) return false;

                return comp.Platform.Weapons[weaponId].System.WeaponIdHash == id;
            };

            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }
    }
}
