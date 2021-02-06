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

        internal void WeaponDetectStateChanges()
        {
            if (Platform.State != CorePlatform.PlatformState.Ready)
                return;

            if (Session.Tick - Ai.LastDetectEvent > 59) {
                Ai.LastDetectEvent = Session.Tick;
                Ai.SleepingComps = 0;
                Ai.AwakeComps = 0;
                Ai.DetectOtherSignals = false;
            }

            UpdatedState = true;

            var overRides = Data.Repo.Base.Set.Overrides;
            var attackNeutrals = overRides.Neutrals;
            var attackNoOwner = overRides.Unowned;
            var attackFriends = overRides.Friendly;
            var targetNonThreats = (attackNoOwner || attackNeutrals || attackFriends);
            
            DetectOtherSignals = targetNonThreats;
            if (DetectOtherSignals)
                Ai.DetectOtherSignals = true;
            var wasAsleep = IsAsleep;
            IsAsleep = false;
            IsDisabled = Ai.TouchingWater && !ShootSubmerged && Ai.WaterVolume.Contains(CoreEntity.PositionComp.WorldAABB.Center) != ContainmentType.Disjoint;

            if (!Ai.Session.IsServer)
                return;

            var otherRangeSqr = Ai.DetectionInfo.OtherRangeSqr;
            var threatRangeSqr = Ai.DetectionInfo.PriorityRangeSqr;
            var targetInrange = DetectOtherSignals ? otherRangeSqr <= MaxDetectDistanceSqr && otherRangeSqr >=MinDetectDistanceSqr || threatRangeSqr <= MaxDetectDistanceSqr && threatRangeSqr >= MinDetectDistanceSqr
                : threatRangeSqr <= MaxDetectDistanceSqr && threatRangeSqr >=MinDetectDistanceSqr;

            if (Ai.Session.Settings.Enforcement.ServerSleepSupport && !targetInrange && PartTracking == 0 && Ai.Construct.RootAi.Data.Repo.ControllingPlayers.Count <= 0 && Session.TerminalMon.Comp != this && Data.Repo.Base.State.TerminalAction == TriggerActions.TriggerOff) {

                IsAsleep = true;
                Ai.SleepingComps++;
            }
            else if (wasAsleep) {

                Ai.AwakeComps++;
            }
            else 
                Ai.AwakeComps++;
        }

        internal void OtherDetectStateChanges()
        {
            if (Platform.State != CorePlatform.PlatformState.Ready)
                return;

            if (Session.Tick - Ai.LastDetectEvent > 59)
            {
                Ai.LastDetectEvent = Session.Tick;
                Ai.SleepingComps = 0;
                Ai.AwakeComps = 0;
                Ai.DetectOtherSignals = false;
            }

            UpdatedState = true;


            DetectOtherSignals = false;
            if (DetectOtherSignals)
                Ai.DetectOtherSignals = true;

            var wasAsleep = IsAsleep;
            IsAsleep = false;
            IsDisabled = false;

            if (!Ai.Session.IsServer)
                return;

            var otherRangeSqr = Ai.DetectionInfo.OtherRangeSqr;
            var priorityRangeSqr = Ai.DetectionInfo.PriorityRangeSqr;
            var somethingInRange = DetectOtherSignals ? otherRangeSqr <= MaxDetectDistanceSqr && otherRangeSqr >= MinDetectDistanceSqr || priorityRangeSqr <= MaxDetectDistanceSqr && priorityRangeSqr >= MinDetectDistanceSqr : priorityRangeSqr <= MaxDetectDistanceSqr && priorityRangeSqr >= MinDetectDistanceSqr;

            if (Ai.Session.Settings.Enforcement.ServerSleepSupport && !somethingInRange && PartTracking == 0 && Ai.Construct.RootAi.Data.Repo.ControllingPlayers.Count <= 0 && Session.TerminalMon.Comp != this && Data.Repo.Base.State.TerminalAction == TriggerActions.TriggerOff)
            {

                IsAsleep = true;
                Ai.SleepingComps++;
            }
            else if (wasAsleep)
            {

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
                case "ArmorShowArea":
                    o.ArmorShowArea = enabled;
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
