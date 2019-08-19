using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        private CompStatus WeaponState()
        {
            if (_isServer)
            {
                if (OverHeating()) return CompStatus.OverHeating;
                if (WarmingUp()) return CompStatus.WarmingUp;
                if (Offline()) return CompStatus.Offline;
            }
            else if (!State.Value.Online) return CompStatus.Offline;

            State.Value.Online = true;
            return CompStatus.Online;
        }

        internal void HealthCheck()
        {
            switch (Status)
            {
                case CompStatus.Startup:
                    Startup();
                    break;
                case CompStatus.ReInit:
                    ReInit();
                    break;
            }
        }

        private bool Startup()
        {
            IsWorking = MyCube.IsWorking;
            IsFunctional = MyCube.IsFunctional;
            State.Value.Online = IsWorking && IsFunctional;
            if (Turret.Enabled) Turret.Enabled = false; Turret.Enabled = true;
            Log.Line("test");
            Status = CompStatus.Online;
            return true;
        }

        private bool ReInit()
        {
            Platform.ResetParts(this);
            Status = CompStatus.Online;
            return true;
        }

        private bool OverHeating()
        {
            return false;
        }

        private bool WarmingUp()
        {
            return false;
        }

        internal bool Offline()
        {
            return false;
        }

        internal void FailWeapon(CompStatus state)
        {
            var on = state != CompStatus.Offline;
            var cool = state != CompStatus.OverHeating;
            var initing = state != CompStatus.WarmingUp;
            var keepCharge = on && !cool;
            var clear = !on && !initing;
            OfflineWeapon(clear, state, keepCharge);
        }

        internal void Online()
        {
        }

        internal void OfflineWeapon(bool clear, CompStatus reason, bool keepCharge = false)
        {
            DefaultWeaponState(clear, keepCharge);

            if (_isServer) UpdateNetworkState();
            else TerminalRefresh();
        }

        private void DefaultWeaponState(bool clear, bool keepHeat)
        {
            var state = State;
            NotFailed = false;
            if (clear)
            {
                Sink.Update();

                if (_isServer && !keepHeat)
                {
                    state.Value.Online = false;
                    state.Value.Heat = 0;
                }
            }
            else if (_isServer)
                state.Value.Online = false;

            TerminalRefresh(false);
        }

        private void ComingOnline()
        {
            _firstLoop = false;
            NotFailed = true;
            WarmedUp = true;

            if (_isServer)
            {
                UpdateNetworkState();
            }
            else
            {
                TerminalRefresh();
            }
        }

        private bool ClientOfflineStates()
        {
            var shieldUp = State.Value.Online && !State.Value.Overload;

            if (!shieldUp)
            {
                if (_clientOn)
                {
                    //ClientDown();
                    _clientOn = false;
                    TerminalRefresh();
                }
                return true;
            }

            if (!_clientOn)
            {
                ComingOnline();
                _clientOn = true;
            }
            return false;
        }

        internal void UpdateSettings(LogicSettingsValues newSettings)
        {
            if (newSettings.MId > Set.Value.MId)
            {
                Set.Value = newSettings;
                SettingsUpdated = true;

            }
        }

        internal void UpdateState(LogicStateValues newState)
        {
            if (newState.MId > State.Value.MId)
            {
                if (!_isServer)
                {

                    //if (State.Value.Message) BroadcastMessage();
                }
                State.Value = newState;
                _clientNotReady = false;
            }
        }

        private void UpdateSettings()
        {
            if (Session.Instance.Tick % 33 == 0)
            {
                if (SettingsUpdated)
                {

                    SettingsUpdated = false;
                    Set.SaveSettings();
                    if (_isServer)
                    {
                        UpdateNetworkState();
                    }
                }
            }
            else if (Session.Instance.Tick % 34 == 0)
            {
                if (ClientUiUpdate)
                {
                    ClientUiUpdate = false;
                    if (!_isServer) Set.NetworkUpdate();
                }
            }
        }

        internal void UpdateNetworkState()
        {
            if (Session.Instance.MpActive)
            {
                State.NetworkUpdate();
                if (_isServer) TerminalRefresh(false);
            }

            //if (!_isDedicated && State.Value.Message) BroadcastMessage();

            State.Value.Message = false;
            State.SaveState();
        }
    }
}
