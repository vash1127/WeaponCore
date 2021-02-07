using System;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Support.PartDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore.Support
{
    public partial class CoreComponent
    {
        internal void HealthCheck()
        {
            if (Platform.State != CorePlatform.PlatformState.Ready || CoreEntity.MarkedForClose)
                return;

            switch (Status)
            {
                case Start.Starting:
                    Startup();
                    break;
                case Start.ReInit:
                    if (Type == CompType.Weapon) 
                        Platform.ResetParts(this);
                    Status = Start.Started;
                    break;
            }
        }

        private void Startup()
        {
            IsWorking = !IsBlock || Cube.IsWorking;

            if (!IsWorking)
                Ai.PowerDistributor?.MarkForUpdate();

            if (FunctionalBlock.Enabled) {
                FunctionalBlock.Enabled = false;
                FunctionalBlock.Enabled = true;
            }

            Status = Start.Started;
        }

        internal void WakeupComp()
        {
            if (IsAsleep) {
                IsAsleep = false;
                Ai.AwakeComps += 1;
                Ai.SleepingComps -= 1;
            }
        }


        internal void SubpartClosed(MyEntity ent)
        {
            try
            {
                if (ent == null)
                    return;

                using (CoreEntity.Pin())
                {
                    ent.OnClose -= SubpartClosed;
                    if (!CoreEntity.MarkedForClose && Platform.State == CorePlatform.PlatformState.Ready)
                    {
                        if (Type == CompType.Weapon)
                            Platform.ResetParts(this);
                        Status = Start.Started;

                        foreach (var w in Platform.Weapons)
                        {
                            w.Azimuth = 0;
                            w.Elevation = 0;
                            w.Elevation = 0;

                            if (w.ActiveAmmoDef.AmmoDef.Const.MustCharge)
                                w.Reloading = false;

                            if (!FunctionalBlock.Enabled)
                                w.EventTriggerStateChanged(EventTriggers.TurnOff, true);
                            else if (w.AnimationsSet.ContainsKey(EventTriggers.TurnOn))
                                Session.FutureEvents.Schedule(w.TurnOnAV, null, 100);

                            if (w.ProtoWeaponAmmo.CurrentAmmo == 0)
                                w.EventTriggerStateChanged(EventTriggers.EmptyOnGameLoad, true);                            
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SubpartClosed: {ex}");
            }
        }
    }
}
