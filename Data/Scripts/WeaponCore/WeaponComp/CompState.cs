using System;
using VRage.Game.Entity;
using WeaponCore.Platform;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        internal void HealthCheck()
        {
            if (Platform.State != MyWeaponPlatform.PlatformState.Ready || MyCube.MarkedForClose)
                return;

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
        }

        private void Startup()
        {
            IsWorking = MyCube.IsWorking;
            IsFunctional = MyCube.IsFunctional;
            Data.Repo.State.Online = IsWorking && IsFunctional;

            if (MyCube != null)
                if (FunctionalBlock.Enabled)
                {
                    FunctionalBlock.Enabled = false;
                    FunctionalBlock.Enabled = true;
                }

            Status = Start.Started;
        }

        internal void WakeupComp()
        {
            if (IsAsleep) {
                Log.Line($"force comp awake");
                IsAsleep = false;
                Ai.AwakeComps += 1;
                Ai.SleepingComps -= 1;
            }
        }

        internal void RequestShootUpdate(ShootActions action, long playerId = -1)
        {
            Session.TerminalMon.ClientUpdate(this);
            if (Session.IsServer) {
                
                ResetShootState(action, playerId);
                if (Session.MpActive)
                {
                    Session.SendCompStateUpdate(this);
                    Session.SendCompSettingUpdate(this);
                }
            }
            else
                Session.SendActionShootUpdate(this, action);
        }

        internal void ResetShootState(ShootActions action, long playerId)
        {
            var cycleShootClick = Data.Repo.Set.TerminalAction == ShootActions.ShootClick && action == ShootActions.ShootClick;
            var cycleShootOn = Data.Repo.Set.TerminalAction == ShootActions.ShootOn && action == ShootActions.ShootOn;
            var cycleSomething = cycleShootOn || cycleShootClick;

            var addShot = !cycleShootClick && action == ShootActions.ShootClick;

            if (Data.Repo.Set.Overrides.ManualControl || Data.Repo.Set.Overrides.TargetPainter) {
                Data.Repo.Set.Overrides.ManualControl = false;
                Data.Repo.Set.Overrides.TargetPainter = false;
            }

            Data.Repo.Set.TerminalActionSetter(this, cycleSomething ? ShootActions.ShootOff : action);

            for (int i = 0; i < Platform.Weapons.Length; i++) {
                var w = Platform.Weapons[i];
                w.State.SingleShotCounter = addShot ? w.State.SingleShotCounter++ : 0;
            }

            if (action == ShootActions.ShootClick && HasTurret) {
                Data.Repo.State.CurrentPlayerControl.ControlType = ControlType.Ui;
            }
            else if (action == ShootActions.ShootClick || action == ShootActions.ShootOnce || action == ShootActions.ShootOn) {
                Data.Repo.State.CurrentPlayerControl.ControlType = ControlType.Toolbar;
            }
            else
                Data.Repo.State.CurrentPlayerControl.ControlType = ControlType.None;

            playerId = playerId == -1 ? Session.PlayerId : playerId;
            Data.Repo.State.CurrentPlayerControl.PlayerId = action == ShootActions.ShootOff ? -1 : playerId;
        }

        internal void DetectStateChanges()
        {
            if (Platform.State != MyWeaponPlatform.PlatformState.Ready)
                return;

            if (Session.Tick - Ai.LastDetectEvent > 59) {
                Ai.LastDetectEvent = Session.Tick;
                Ai.SleepingComps = 0;
                Ai.AwakeComps = 0;
                Ai.TargetNonThreats = false;
            }

            UpdatedState = true;

            var overRides = Data.Repo.Set.Overrides;
            var overActive = overRides.Activate;
            var attackNeutrals = overActive && overRides.Neutrals;
            var attackNoOwner = overActive && overRides.Unowned;
            var attackFriends = overActive && overRides.Friendly;
            var targetNonThreats = (attackNoOwner || attackNeutrals || attackFriends);
            
            TargetNonThreats = targetNonThreats;
            if (TargetNonThreats)
                Ai.TargetNonThreats = true;
            var wasAsleep = IsAsleep;
            IsAsleep = false;

            if (!Ai.Session.IsServer)
                return;

            var otherRangeSqr = Ai.TargetingInfo.OtherRangeSqr;
            var threatRangeSqr = Ai.TargetingInfo.ThreatRangeSqr;
            var targetInrange = TargetNonThreats ? otherRangeSqr <= MaxTargetDistanceSqr && otherRangeSqr >=MinTargetDistanceSqr
                : threatRangeSqr <= MaxTargetDistanceSqr && threatRangeSqr >=MinTargetDistanceSqr;

            if (false && !targetInrange && WeaponsTracking == 0 && Ai.Construct.RootAi.ControllingPlayers.Count <= 0 && Session.TerminalMon.Comp != this && Data.Repo.Set.TerminalAction == ShootActions.ShootOff) {

                IsAsleep = true;
                Ai.SleepingComps++;
            }
            else if (wasAsleep) {

                Log.Line("waking up");
                Ai.AwakeComps++;
            }
            else 
                Ai.AwakeComps++;
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
                            w.Elevation = 0;

                            if (w.ActiveAmmoDef.AmmoDef.Const.MustCharge)
                                w.Reloading = false;

                            if (!FunctionalBlock.Enabled)
                                w.EventTriggerStateChanged(EventTriggers.TurnOff, true);
                            else if (w.AnimationsSet.ContainsKey(EventTriggers.TurnOn))
                                Session.FutureEvents.Schedule(w.TurnOnAV, null, 100);

                            if (w.State.Sync.CurrentAmmo == 0)
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
