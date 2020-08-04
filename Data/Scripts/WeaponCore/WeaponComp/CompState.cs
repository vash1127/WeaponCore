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

        internal void RequestShootUpdate(ShootActions action, long playerId)
        {
            if (Session.HandlesInput)
                Session.TerminalMon.HandleInputUpdate(this);
            
            if (Session.IsServer)
            {
                bool singleShot;
                ResetShootState(action, playerId, out singleShot);
                if (Session.MpActive)
                {
                    Session.SendCompData(this);
                    if ((Session.HandlesInput || playerId == 0) && singleShot)
                        ShootOnceCheck(false);
                }
            }
            else if (action == ShootActions.ShootOnce)
                ShootOnceCheck(true);
            else Session.SendActionShootUpdate(this, action);
        }

        internal bool ShootOnceCheck(bool addToCounter, int weaponToCheck = -1)
        {
            var checkAllWeapons = weaponToCheck == -1;
            var numOfWeapons = checkAllWeapons ? Platform.Weapons.Length : 1;
            var loadedWeapons = 0;

            for (int i = 0; i < Platform.Weapons.Length; i++) {
                var w = Platform.Weapons[i];

                if (w.Reload.CurrentAmmo > 0 && (checkAllWeapons || weaponToCheck == i))
                    ++loadedWeapons;
            }
            if (numOfWeapons == loadedWeapons) {

                for (int i = 0; i < Platform.Weapons.Length; i++)  {

                    var w = Platform.Weapons[i];

                    if (!checkAllWeapons && i != weaponToCheck)
                        continue;
                    
                    if (addToCounter) w.ShootOnce = true;
                    if (!Session.IsServer && w.System.TurretMovement == WeaponSystem.TurretType.Fixed && w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance == WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType.None && !w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon)
                        w.ClientStaticShot = true;
                }
                
                Session.SendSingleShot(this);
                
                if (Session.IsClient) 
                    Session.SendActionShootUpdate(this, ShootActions.ShootOnce);
                return true;
            }

            return false;
        }

        internal void ResetShootState(ShootActions action, long playerId, out bool addShot)
        {
            var cycleShootClick = Data.Repo.State.TerminalAction == ShootActions.ShootClick && action == ShootActions.ShootClick;
            var cycleShootOn = Data.Repo.State.TerminalAction == ShootActions.ShootOn && action == ShootActions.ShootOn;
            var cycleSomething = cycleShootOn || cycleShootClick;
            addShot = !cycleShootClick && action == ShootActions.ShootOnce;
            if (Data.Repo.Set.Overrides.ManualControl || Data.Repo.Set.Overrides.TargetPainter) {
                Data.Repo.Set.Overrides.ManualControl = false;
                Data.Repo.Set.Overrides.TargetPainter = false;
            }
            Data.Repo.State.TerminalActionSetter(this, cycleSomething ? ShootActions.ShootOff : action);

            if (addShot && (Session.HandlesInput || playerId == 0))
                for (int i = 0; i < Platform.Weapons.Length; i++)
                    Platform.Weapons[i].ShootOnce = true;

            if (action == ShootActions.ShootClick && HasTurret) 
                Data.Repo.State.Control = CompStateValues.ControlMode.Ui;
            else if (action == ShootActions.ShootClick || action == ShootActions.ShootOnce ||  action == ShootActions.ShootOn)
                Data.Repo.State.Control = CompStateValues.ControlMode.Toolbar;
            else
                Data.Repo.State.Control = CompStateValues.ControlMode.None;

            playerId = Session.HandlesInput && playerId == -1 ? Session.PlayerId : playerId;
            var newId = action == ShootActions.ShootOff && !Data.Repo.State.TrackingReticle ? -1 : playerId;
            Data.Repo.State.PlayerId = newId;
        }

        internal void ResetPlayerControl()
        {
            Data.Repo.State.PlayerId = -1;
            Data.Repo.State.Control = CompStateValues.ControlMode.None;
            Data.Repo.Set.Overrides.ManualControl = false;
            Data.Repo.Set.Overrides.TargetPainter = false;

            var tAction = Data.Repo.State.TerminalAction;
            if (tAction == ShootActions.ShootOnce || tAction == ShootActions.ShootClick) 
                Data.Repo.State.TerminalActionSetter(this, ShootActions.ShootOff);

            Session.SendCompData(this);
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

            if (false && !targetInrange && WeaponsTracking == 0 && Ai.Construct.RootAi.Data.Repo.ControllingPlayers.Count <= 0 && Session.TerminalMon.Comp != this && Data.Repo.State.TerminalAction == ShootActions.ShootOff) {

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

                            if (w.Reload.CurrentAmmo == 0)
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
