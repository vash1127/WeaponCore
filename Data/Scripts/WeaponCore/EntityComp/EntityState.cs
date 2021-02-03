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
                    if (IsWeapon) 
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

        internal void RequestShootUpdate(TriggerActions action, long playerId)
        {
            if (IsDisabled) return;

            if (Session.HandlesInput)
                Session.TerminalMon.HandleInputUpdate(this);
            
            if (Session.IsServer)
            {
                ResetShootState(action, playerId);
                
                if (action == TriggerActions.TriggerOnce)
                    ShootOnceCheck();

                if (Session.MpActive) {
                    Session.SendCompBaseData(this);
                    if (action == TriggerActions.TriggerClick || action == TriggerActions.TriggerOn) {
                        foreach (var w in Platform.Weapons)
                            Session.SendWeaponAmmoData(w);
                    }
                }

            }
            else if (action == TriggerActions.TriggerOnce)
                ShootOnceCheck();
            else Session.SendActionShootUpdate(this, action);
        }

        internal bool ShootOnceCheck(int weaponToCheck = -1)
        {
            var checkAllWeapons = weaponToCheck == -1;
            var numOfWeapons = checkAllWeapons ? Platform.Weapons.Count : 1;
            var loadedWeapons = 0;

            for (int i = 0; i < Platform.Weapons.Count; i++) {
                var w = Platform.Weapons[i];

                if (w.State.Overheated)
                    return false;

                if ((w.Ammo.CurrentAmmo > 0 || w.System.DesignatorWeapon) && (checkAllWeapons || weaponToCheck == i))
                    ++loadedWeapons;
            }
            if (numOfWeapons == loadedWeapons) {

                for (int i = 0; i < Platform.Weapons.Count; i++)  {

                    var w = Platform.Weapons[i];

                    if (!checkAllWeapons && i != weaponToCheck)
                        continue;

                    if (Session.IsServer)
                        w.ShootOnce = true;
                }
                
                if (Session.IsClient) 
                    Session.SendActionShootUpdate(this, TriggerActions.TriggerOnce);
                return true;
            }

            return false;
        }

        internal void ResetShootState(TriggerActions action, long playerId)
        {
            var cycleShootClick = Data.Repo.Base.State.TerminalAction == TriggerActions.TriggerClick && action == TriggerActions.TriggerClick;
            var cycleShootOn = Data.Repo.Base.State.TerminalAction == TriggerActions.TriggerOn && action == TriggerActions.TriggerOn;
            var cycleSomething = cycleShootOn || cycleShootClick;

            Data.Repo.Base.Set.Overrides.Control = GroupOverrides.ControlModes.Auto;

            Data.Repo.Base.State.TerminalActionSetter(this, cycleSomething ? TriggerActions.TriggerOff : action);

            if (action == TriggerActions.TriggerClick && HasTurret) 
                Data.Repo.Base.State.Control = CompStateValues.ControlMode.Ui;
            else if (action == TriggerActions.TriggerClick || action == TriggerActions.TriggerOnce ||  action == TriggerActions.TriggerOn)
                Data.Repo.Base.State.Control = CompStateValues.ControlMode.Toolbar;
            else
                Data.Repo.Base.State.Control = CompStateValues.ControlMode.None;

            playerId = Session.HandlesInput && playerId == -1 ? Session.PlayerId : playerId;
            var newId = action == TriggerActions.TriggerOff && !Data.Repo.Base.State.TrackingReticle ? -1 : playerId;
            Data.Repo.Base.State.PlayerId = newId;
        }

        internal void ResetPlayerControl()
        {
            Data.Repo.Base.State.PlayerId = -1;
            Data.Repo.Base.State.Control = CompStateValues.ControlMode.None;
            Data.Repo.Base.Set.Overrides.Control = GroupOverrides.ControlModes.Auto;

            var tAction = Data.Repo.Base.State.TerminalAction;
            if (tAction == TriggerActions.TriggerOnce || tAction == TriggerActions.TriggerClick) 
                Data.Repo.Base.State.TerminalActionSetter(this, TriggerActions.TriggerOff, Session.MpActive);
            if (Session.MpActive) 
                Session.SendCompBaseData(this);
        }

        internal void DetectStateChanges()
        {
            if (Platform.State != CorePlatform.PlatformState.Ready)
                return;

            if (Session.Tick - Ai.LastDetectEvent > 59) {
                Ai.LastDetectEvent = Session.Tick;
                Ai.SleepingComps = 0;
                Ai.AwakeComps = 0;
                Ai.TargetNonThreats = false;
            }

            UpdatedState = true;

            var overRides = Data.Repo.Base.Set.Overrides;
            var attackNeutrals = overRides.Neutrals;
            var attackNoOwner = overRides.Unowned;
            var attackFriends = overRides.Friendly;
            var targetNonThreats = (attackNoOwner || attackNeutrals || attackFriends);
            
            TargetNonThreats = targetNonThreats;
            if (TargetNonThreats)
                Ai.TargetNonThreats = true;
            var wasAsleep = IsAsleep;
            IsAsleep = false;
            IsDisabled = Ai.TouchingWater && !ShootSubmerged && Ai.WaterVolume.Contains(CoreEntity.PositionComp.WorldAABB.Center) != ContainmentType.Disjoint;

            if (!Ai.Session.IsServer)
                return;

            var otherRangeSqr = Ai.TargetingInfo.OtherRangeSqr;
            var threatRangeSqr = Ai.TargetingInfo.ThreatRangeSqr;
            var targetInrange = TargetNonThreats ? otherRangeSqr <= MaxTargetDistanceSqr && otherRangeSqr >=MinTargetDistanceSqr || threatRangeSqr <= MaxTargetDistanceSqr && threatRangeSqr >= MinTargetDistanceSqr
                : threatRangeSqr <= MaxTargetDistanceSqr && threatRangeSqr >=MinTargetDistanceSqr;

            if (Ai.Session.Settings.Enforcement.ServerSleepSupport && !targetInrange && WeaponsTracking == 0 && Ai.Construct.RootAi.Data.Repo.ControllingPlayers.Count <= 0 && Session.TerminalMon.Comp != this && Data.Repo.Base.State.TerminalAction == TriggerActions.TriggerOff) {

                IsAsleep = true;
                Ai.SleepingComps++;
            }
            else if (wasAsleep) {

                Ai.AwakeComps++;
            }
            else 
                Ai.AwakeComps++;
        }

        internal static void RequestSetValue(CoreComponent comp, string setting, int value, long playerId)
        {
            if (comp.Session.IsServer)
            {
                SetValue(comp, setting, value, playerId);
            }
            else if (comp.Session.IsClient)
            {
                comp.Session.SendOverRidesClientComp(comp, setting, value);
            }
        }

        internal static void SetValue(CoreComponent comp, string setting, int v, long playerId)
        {
            var o = comp.Data.Repo.Base.Set.Overrides;
            var enabled = v > 0;
            var clearTargets = false;

            switch (setting)
            {
                case "MaxSize":
                    o.MaxSize = v;
                    break;
                case "MinSize":
                    o.MinSize = v;
                    break;
                case "SubSystems":
                    o.SubSystem = (PartDefinition.TargetingDef.BlockTypes)v;
                    break;
                case "MovementModes":
                    o.MoveMode = (GroupOverrides.MoveModes)v;
                    clearTargets = true;
                    break;
                case "ControlModes":
                    o.Control = (GroupOverrides.ControlModes)v;
                    clearTargets = true;
                    break;
                case "FocusSubSystem":
                    o.FocusSubSystem = enabled;
                    break;
                case "FocusTargets":
                    o.FocusTargets = enabled;
                    clearTargets = true;
                    break;
                case "Unowned":
                    o.Unowned = enabled;
                    break;
                case "Friendly":
                    o.Friendly = enabled;
                    clearTargets = true;
                    break;
                case "Meteors":
                    o.Meteors = enabled;
                    break;
                case "Grids":
                    o.Grids = enabled;
                    break;
                case "Biologicals":
                    o.Biologicals = enabled;
                    break;
                case "Projectiles":
                    o.Projectiles = enabled;
                    clearTargets = true;
                    break;
                case "Neutrals":
                    o.Neutrals = enabled;
                    clearTargets = true;
                    break;
            }

            ResetCompState(comp, playerId, clearTargets);

            if (comp.Session.MpActive)
                comp.Session.SendCompBaseData(comp);
        }


        internal static void ResetCompState(CoreComponent comp, long playerId, bool resetTarget, Dictionary<string, int> settings = null)
        {
            var o = comp.Data.Repo.Base.Set.Overrides;
            var userControl = o.Control != GroupOverrides.ControlModes.Auto;

            if (userControl)
            {
                comp.Data.Repo.Base.State.PlayerId = playerId;
                comp.Data.Repo.Base.State.Control = CompStateValues.ControlMode.Ui;
                if (settings != null) settings["ControlModes"] = (int)o.Control;
                comp.Data.Repo.Base.State.TerminalActionSetter(comp, TriggerActions.TriggerOff);
            }
            else
            {
                comp.Data.Repo.Base.State.PlayerId = -1;
                comp.Data.Repo.Base.State.Control = CompStateValues.ControlMode.None;
            }

            if (resetTarget)
                ClearTargets(comp);
        }

        private static void ClearTargets(CoreComponent comp)
        {
            for (int i = 0; i < comp.Platform.Weapons.Count; i++)
            {
                var weapon = comp.Platform.Weapons[i];
                if (weapon.Target.HasTarget)
                    comp.Platform.Weapons[i].Target.Reset(comp.Session.Tick, Target.States.ControlReset);
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
                        if (IsWeapon)
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

                            if (w.Ammo.CurrentAmmo == 0)
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
