using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        private bool EntityAlive()
        {
            if (MyGrid?.Physics == null) return false;
            if (!_firstSync && _readyToSync) SaveAndSendAll();
            if (!_isDedicated && _count == 29) TerminalRefresh();

            if (!_allInited && !PostInit()) return false;

            if (ClientUiUpdate || SettingsUpdated) UpdateSettings();
            return true;
        }

        private bool PostInit()
        {
            if (!_isServer && _clientNotReady) return false;
            Session.Instance.CreateLogicElements(Turret);
            WepUi.CreateUi(Turret);
            if (!Session.Instance.WepAction)
            {
                Session.Instance.WepAction = true;
                Session.AppendConditionToAction<IMyLargeTurretBase>((a) => Session.Instance.WepActions.Contains(a.Id), (a, b) => b.GameLogic.GetAs<WeaponComponent>() != null && Session.Instance.WepActions.Contains(a.Id));
            }
            if (_isServer && !IsFunctional) return false;

            if (_mpActive && _isServer) State.NetworkUpdate();

            _allInited = true;
            return true;
        }

        private void StorageSetup()
        {
            var isServer = MyAPIGateway.Multiplayer.IsServer;
            if (Set == null) Set = new LogicSettings(Turret);
            if (State == null) State = new LogicState(Turret);

            if (Turret.Storage == null) State.StorageInit();

            Set.LoadSettings();
            if (!State.LoadState() && !isServer) _clientNotReady = true;
            UpdateSettings(Set.Value);
            if (isServer)
            {
                State.Value.Overload = false;
                State.Value.Heat = 0;
            }
        }


        private void CreateUi()
        {
            if (Session.Instance.ControlInit) return;
            Session.Instance.ControlInit = true;
            foreach (var weapon in Platform.Weapons)
            {
                CreateWeaponEnables<IMyTerminalBlock>(weapon, MyCube.BlockDefinition.Id.SubtypeId.String);
            }
        }

        public void CreateWeaponEnables<T>(Weapon weapon, string subtypeName) where T : class, IMyTerminalBlock
        {
            IMyTerminalControlOnOffSwitch weaponControl = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>("TestControl");
            weaponControl.Title = MyStringId.GetOrCompute($"{weapon.System.WeaponName} Enable");
            weaponControl.Enabled = x => x.BlockDefinition.SubtypeId.Equals(subtypeName);
            weaponControl.Visible = x => x.BlockDefinition.SubtypeId.Equals(subtypeName);
            weaponControl.SupportsMultipleBlocks = true;
            weaponControl.OnText = MyStringId.GetOrCompute("On");
            weaponControl.OffText = MyStringId.GetOrCompute("Off");
            weaponControl.Setter = (x, v) => SetEnable(x, v, weapon.WeaponId);
            weaponControl.Getter = x => GetEnable(x, weapon.WeaponId);
            MyAPIGateway.TerminalControls.AddControl<T>(weaponControl);
            //toggle weapon action
            IMyTerminalAction enableOnOff = MyAPIGateway.TerminalControls.CreateAction<T>("Weapon_OnOff");
            enableOnOff.Action = (x) =>
            {
                var recharge = GetEnable(x, weapon.WeaponId);
                SetEnable(x, !recharge, weapon.WeaponId);
            };
            enableOnOff.ValidForGroups = true;
            enableOnOff.Writer = (x, s) => GetWriter(x, s, weapon.WeaponId);
            enableOnOff.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            enableOnOff.Enabled = x => x.BlockDefinition.SubtypeId.Equals(subtypeName);
            enableOnOff.Name = new StringBuilder("Weapon On/Off");
            MyAPIGateway.TerminalControls.AddAction<T>(enableOnOff);

            //weapon on action
            IMyTerminalAction rechargeOn = MyAPIGateway.TerminalControls.CreateAction<T>("Weapon_On");
            rechargeOn.Action = (x) => SetEnable(x, true, weapon.WeaponId);
            rechargeOn.ValidForGroups = true;
            rechargeOn.Writer = (x, s) => GetWriter(x, s, weapon.WeaponId);
            rechargeOn.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            rechargeOn.Enabled = x => x.BlockDefinition.SubtypeId.Equals(subtypeName);
            rechargeOn.Name = new StringBuilder("Weapon On");
            MyAPIGateway.TerminalControls.AddAction<T>(rechargeOn);

            //weapon off action
            IMyTerminalAction rechargeOff = MyAPIGateway.TerminalControls.CreateAction<T>("Weapon_Off");
            rechargeOff.Action = (x) => SetEnable(x, false, weapon.WeaponId);
            rechargeOff.ValidForGroups = true;
            rechargeOff.Writer = (x, s) => GetWriter(x, s, weapon.WeaponId);
            rechargeOff.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            rechargeOff.Enabled = x => x.BlockDefinition.SubtypeId.Equals(subtypeName);
            rechargeOff.Name = new StringBuilder("Weapon Off");
            MyAPIGateway.TerminalControls.AddAction<T>(rechargeOff);
        }

        public void GetWriter(IMyTerminalBlock b, StringBuilder s, int weaponId)
        {
            s.Clear();
            var comp = b.Components.Get<WeaponComponent>();
            var set = comp.Platform.Weapons[weaponId].Enabled;

            s.Append(set ? "On" : "Off");
        }

        public void SetEnable(IMyTerminalBlock b, bool v, int weaponId)
        {
            var comp = b.Components.Get<WeaponComponent>();
            comp.Platform.Weapons[weaponId].Enabled = v;
        }

        public bool GetEnable(IMyTerminalBlock b, int weaponId)
        {
            var comp = b.Components.Get<WeaponComponent>();
            return comp.Platform.Weapons[weaponId].Enabled;
        }

    }
}
