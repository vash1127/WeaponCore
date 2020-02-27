using System;
using VRage.Game.Entity;
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
                    Platform.ResetParts(this);
                    Status = Start.Started;
                    break;
            }
            UpdateNetworkState();
        }

        private void Startup()
        {
            IsWorking = MyCube.IsWorking;
            IsFunctional = MyCube.IsFunctional;
            State.Value.Online = IsWorking && IsFunctional;

            if(MyCube != null)
                if (FunctionalBlock.Enabled) { FunctionalBlock.Enabled = false; FunctionalBlock.Enabled = true; }
            
            Status = Start.Started;
        }

        internal void SubpartClosed(MyEntity ent)
        {
            try
            {
                if (ent == null || MyCube == null) return;

                using (MyCube.Pin())
                {
                    ent.OnClose -= SubpartClosed;
                    if (!MyCube.MarkedForClose && Platform.State == MyWeaponPlatform.PlatformState.Ready)
                    {
                        Platform.ResetParts(this);
                        Status = Start.Started;

                        foreach (var w in Platform.Weapons)
                        {
                            w.Azimuth = 0;
                            w.Elevation = 0;
                            if (!FunctionalBlock.Enabled)
                                w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);

                            if (w.State.CurrentAmmo == 0)
                                w.EventTriggerStateChanged(Weapon.EventTriggers.EmptyOnGameLoad, true);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SubpartClosed: {ex}"); }
        }

        internal void UpdateSettings(CompSettingsValues newSettings)
        {
            if (newSettings.MId > Set.Value.MId)
            {
                Set.Value = newSettings;
                SettingsUpdated = true;
            }
        }

        internal void UpdateNetworkState()
        {
            if (Session.MpActive)
                State.NetworkUpdate();

            State.Value.Message = false;
            State.SaveState();
        }
    }
}
