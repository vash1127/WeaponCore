using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        private void PowerInit()
        {
            var resourceInfo = new MyResourceSinkInfo()
            {
                ResourceTypeId = GId,
                MaxRequiredInput = 0f,
                RequiredInputFunc = () => SinkPower,
            };
            MyCube.Components.TryGet(out Sink);
            var gId = GId;
            Sink.RemoveType(ref gId);
            Sink.Init(MyStringHash.GetOrCompute("Charging"), resourceInfo);
            Sink.AddType(ref resourceInfo);
            Sink.Update();
        }
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
            //Session.Instance.CreateLogicElements(Turret);
            //WepUi.CreateUi(Turret);
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

            if (State == null)
            {
                Log.Line($"State null");
                State = new LogicState(this);
            }

            if (Turret.Storage == null)
            {
                Log.Line("Storage null");
                State.StorageInit();
            }

            if (Set == null)
            {
                Log.Line($"Settings null");
                Set = new LogicSettings(this);
            }

            State.LoadState();

            Set.LoadSettings();
            if (!State.LoadState() && !isServer) _clientNotReady = true;
            UpdateSettings(Set.Value);
            Log.Line($"StateWeapon:{State.Value.Weapons != null} - SetWeapon:{Set.Value.Weapons != null}");
            if (isServer)
            {

                foreach (var w in State.Value.Weapons)
                    w.Heat = 0;
            }
        }

        private void CreateUi()
        {
            if (Session.Instance.ControlInit) return;
            Session.Instance.ControlInit = true;
            Session.Instance.CreateLogicElements(Turret);
        }
    }
}
