using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        internal void HealthCheck()
        {
            switch (Status)
            {
                case Start.Starting:
                    Startup();
                    break;
                case Start.ReInit:
                    ReInit();
                    break;
            }

            UpdateNetworkState();
        }

        private bool Startup()
        {
            IsWorking = MyCube.IsWorking;
            IsFunctional = MyCube.IsFunctional;
            State.Value.Online = IsWorking && IsFunctional;
            

            if(IsSorterTurret && SorterBase != null)
                if (SorterBase.Enabled) { SorterBase.Enabled = false; SorterBase.Enabled = true; }
            else if(MissileBase != null)
                if (MissileBase.Enabled) { MissileBase.Enabled = false; MissileBase.Enabled = true; }

            
            Status = Start.Started;
            return true;
        }

        internal void SubpartClosed(MyEntity ent)
        {
            if (ent != null && MyCube != null && !MyCube.MarkedForClose && Platform != null && Platform.State == MyWeaponPlatform.PlatformState.Ready)
            {
                ent.OnClose -= SubpartClosed;
                ReInit();

                foreach (var w in Platform.Weapons)
                {
                    if (IsSorterTurret && !SorterBase.Enabled)
                        w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
                    else if(!MissileBase.Enabled)
                        w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);

                    if (State.Value.Weapons[w.WeaponId].CurrentAmmo == 0)
                        w.EventTriggerStateChanged(Weapon.EventTriggers.EmptyOnGameLoad, true);
                }
            }
        }

        private bool ReInit()
        {
            //Log.Line($"ReInit");
            Platform.ResetParts(this);
            Status = Start.Started;
            return true;
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
            if (Ai.Session.Tick % 33 == 0)
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
            else if (Ai.Session.Tick % 34 == 0)
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
            if (Ai.Session.MpActive)
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
