using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon : Part
    {
        public class WeaponComponent : CoreComponent
        {
            internal readonly IMyAutomaticRifleGun Rifle;
            internal readonly IMyHandheldGunObject<MyGunBase> GunBase;
            internal readonly IMyLargeTurretBase VanillaTurretBase;
            internal Weapon TrackingWeapon;
            internal readonly WeaponCompData Data;
            internal uint LastRayCastTick;
            internal float EffectiveDps;
            internal float PeakDps;
            internal float ShotsPerSec;
            internal float BaseDps;
            internal float AreaDps;
            internal float DetDps;
            internal float CurrentDps;
            internal bool HasEnergyWeapon;
            internal bool HasGuidanceToggle;
            internal bool HasRofSlider;
            internal bool HasChargeWeapon;
            internal bool ShootSubmerged;
            internal bool HasTracking;

            internal WeaponComponent(Session session, MyEntity coreEntity, MyDefinitionId id)
            {
                MyEntity topEntity;

                var cube = coreEntity as MyCubeBlock;
                if (cube != null) {

                    topEntity = cube.CubeGrid;

                    var turret = coreEntity as IMyLargeTurretBase;
                    if (turret != null) {
                        VanillaTurretBase = turret;
                        VanillaTurretBase.EnableIdleRotation = false;
                    }
                }
                else {

                    var gun = coreEntity as IMyAutomaticRifleGun;

                    if (gun != null) {
                        Rifle = gun;
                        GunBase = gun;
                        topEntity = Rifle.Owner;
                    }
                    else
                        topEntity = coreEntity;
                }
                Data = new WeaponCompData(this);
                base.Init(session, coreEntity, cube != null, topEntity, id);
            }

            internal void WeaponInit()
            {

                for (int i = 0; i < Platform.Weapons.Count; i++)
                {
                    var w = Platform.Weapons[i];
                    w.UpdatePivotPos();

                    if (Session.IsClient)
                        w.Target.ClientDirty = true;

                    if (w.Ammo.CurrentAmmo == 0 && !w.Reloading)
                        w.EventTriggerStateChanged(PartDefinition.AnimationDef.PartAnimationSetDef.EventTriggers.EmptyOnGameLoad, true);

                    if (TypeSpecific == CompTypeSpecific.Rifle)
                        Ai.AiOwner = GunBase.OwnerId;
                }
            }

            internal void OnAddedToSceneWeaponTasks(bool firstRun)
            {
                Data.Load();

                if (Session.IsServer)
                    Data.Repo.ResetToFreshLoadState();

                var maxTrajectory1 = 0f;
                for (int i = 0; i < Platform.Weapons.Count; i++)
                {

                    var w = Platform.Weapons[i];

                    if (Session.IsServer)
                        w.ChangeActiveAmmoServer();
                    else w.ChangeActiveAmmoClient();

                    if (w.ActiveAmmoDef.AmmoDef == null || !w.ActiveAmmoDef.AmmoDef.Const.IsTurretSelectable && w.System.AmmoTypes.Length > 1)
                    {
                        Platform.PlatformCrash(this, false, true, $"[{w.System.PartName}] Your first ammoType is broken (isNull:{w.ActiveAmmoDef.AmmoDef == null}), I am crashing now Dave.");
                        return;
                    }

                    w.UpdateWeaponRange();
                    if (maxTrajectory1 < w.MaxTargetDistance)
                        maxTrajectory1 = (float)w.MaxTargetDistance;

                }

                if (Data.Repo.Base.Set.Range <= 0)
                    Data.Repo.Base.Set.Range = maxTrajectory1;

                var maxTrajectory2 = 0d;

                for (int i = 0; i < Platform.Weapons.Count; i++)
                {

                    var weapon = Platform.Weapons[i];
                    weapon.InitTracking();

                    double weaponMaxRange;
                    DpsAndHeatInit(weapon, out weaponMaxRange);

                    if (maxTrajectory2 < weaponMaxRange)
                        maxTrajectory2 = weaponMaxRange;

                    if (weapon.Ammo.CurrentAmmo > weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize)
                        weapon.Ammo.CurrentAmmo = weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize;

                    if (Session.IsServer && weapon.TrackTarget)
                        Session.AcqManager.Monitor(weapon.Acquire);
                }

                if (maxTrajectory2 + Ai.TopEntity.PositionComp.LocalVolume.Radius > Ai.MaxTargetingRange)
                {

                    Ai.MaxTargetingRange = maxTrajectory2 + Ai.TopEntity.PositionComp.LocalVolume.Radius;
                    Ai.MaxTargetingRangeSqr = Ai.MaxTargetingRange * Ai.MaxTargetingRange;
                }

                Ai.OptimalDps += PeakDps;
                Ai.EffectiveDps += EffectiveDps;

                VanillaTurretBase?.SetTarget(Vector3D.MaxValue);

                if (firstRun)
                    WeaponInit();
            }

            private void DpsAndHeatInit(Weapon weapon, out double maxTrajectory)
            {
                MaxHeat += weapon.System.MaxHeat;

                weapon.RateOfFire = (int)(weapon.System.RateOfFire * Data.Repo.Base.Set.RofModifier);
                weapon.BarrelSpinRate = (int)(weapon.System.BarrelSpinRate * Data.Repo.Base.Set.RofModifier);
                HeatSinkRate += weapon.HsRate;

                if (weapon.System.HasBarrelRotation) weapon.UpdateBarrelRotation();

                if (weapon.RateOfFire < 1)
                    weapon.RateOfFire = 1;

                weapon.SetWeaponDps();

                if (!weapon.System.DesignatorWeapon)
                {
                    var patternSize = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoPattern.Length;
                    foreach (var ammo in weapon.ActiveAmmoDef.AmmoDef.Const.AmmoPattern)
                    {
                        weapon.Comp.PeakDps += ammo.Const.PeakDps / (float)patternSize;
                        weapon.Comp.EffectiveDps += ammo.Const.EffectiveDps / (float)patternSize;
                        weapon.Comp.ShotsPerSec += ammo.Const.ShotsPerSec / (float)patternSize;
                        weapon.Comp.BaseDps += ammo.Const.BaseDps / (float)patternSize;
                        weapon.Comp.AreaDps += ammo.Const.AreaDps / (float)patternSize;
                        weapon.Comp.DetDps += ammo.Const.DetDps / (float)patternSize;
                    }
                }

                maxTrajectory = 0;
                if (weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory > maxTrajectory)
                    maxTrajectory = weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;

                if (weapon.System.TrackProjectile)
                    Ai.PointDefense = true;
            }


            internal static void SetRange(CoreComponent comp)
            {
                foreach (var w in comp.Platform.Weapons)
                    w.UpdateWeaponRange();
            }

            internal static void SetRof(CoreComponent comp)
            {
                for (int i = 0; i < comp.Platform.Weapons.Count; i++)
                {
                    var w = comp.Platform.Weapons[i];

                    if (w.ActiveAmmoDef.AmmoDef.Const.MustCharge) continue;

                    w.UpdateRof();
                }

                SetDps(comp);
            }

            internal static void SetDps(CoreComponent comp, bool change = false)
            {
                if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

                for (int i = 0; i < comp.Platform.Weapons.Count; i++)
                {
                    var w = comp.Platform.Weapons[i];
                    if (!change && (!w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon || w.ActiveAmmoDef.AmmoDef.Const.MustCharge)) continue;
                    comp.Session.FutureEvents.Schedule(w.SetWeaponDps, null, 1);
                }
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
                else if (action == TriggerActions.TriggerClick || action == TriggerActions.TriggerOnce || action == TriggerActions.TriggerOn)
                    Data.Repo.Base.State.Control = CompStateValues.ControlMode.Toolbar;
                else
                    Data.Repo.Base.State.Control = CompStateValues.ControlMode.None;

                playerId = Session.HandlesInput && playerId == -1 ? Session.PlayerId : playerId;
                var newId = action == TriggerActions.TriggerOff && !Data.Repo.Base.State.TrackingReticle ? -1 : playerId;
                Data.Repo.Base.State.PlayerId = newId;
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

                    if (Session.MpActive)
                    {
                        Session.SendCompBaseData(this);
                        if (action == TriggerActions.TriggerClick || action == TriggerActions.TriggerOn)
                        {
                            foreach (var w in Platform.Weapons)
                                Session.SendWeaponAmmoData(w);
                        }
                    }

                }
                else if (action == TriggerActions.TriggerOnce)
                    ShootOnceCheck();
                else Session.SendActionShootUpdate(this, action);
            }

            internal void DetectStateChanges()
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
                var targetInrange = DetectOtherSignals ? otherRangeSqr <= MaxDetectDistanceSqr && otherRangeSqr >= MinDetectDistanceSqr || threatRangeSqr <= MaxDetectDistanceSqr && threatRangeSqr >= MinDetectDistanceSqr
                    : threatRangeSqr <= MaxDetectDistanceSqr && threatRangeSqr >= MinDetectDistanceSqr;

                if (Ai.Session.Settings.Enforcement.ServerSleepSupport && !targetInrange && PartTracking == 0 && Ai.Construct.RootAi.Data.Repo.ControllingPlayers.Count <= 0 && Session.TerminalMon.Comp != this && Data.Repo.Base.State.TerminalAction == TriggerActions.TriggerOff)
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


            internal static void RequestSetValue(WeaponComponent comp, string setting, int value, long playerId)
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

            internal static void SetValue(WeaponComponent comp, string setting, int v, long playerId)
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


            internal static void ResetCompState(WeaponComponent comp, long playerId, bool resetTarget, Dictionary<string, int> settings = null)
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

            internal void NotFunctional()
            {
                for (int i = 0; i < Platform.Weapons.Count; i++)
                {

                    var w = Platform.Weapons[i];
                    PartAnimation[] partArray;
                    if (w.AnimationsSet.TryGetValue(PartDefinition.AnimationDef.PartAnimationSetDef.EventTriggers.TurnOff, out partArray))
                    {
                        for (int j = 0; j < partArray.Length; j++)
                            w.PlayEmissives(partArray[j]);
                    }
                    if (!Session.IsClient && !IsWorking)
                        w.Target.Reset(Session.Tick, Target.States.Offline);
                }
            }

            internal void PowerLoss()
            {
                Session.SendCompBaseData(this);
                if (IsWorking)
                {
                    foreach (var w in Platform.Weapons)
                        Session.SendWeaponAmmoData(w);
                }
            }
        }

        internal class ParallelRayCallBack
        {
            internal Weapon Weapon;

            internal ParallelRayCallBack(Weapon weapon)
            {
                Weapon = weapon;
            }

            public void NormalShootRayCallBack(IHitInfo hitInfo)
            {
                Weapon.Casting = false;
                var masterWeapon = Weapon.TrackTarget ? Weapon : Weapon.Comp.TrackingWeapon;
                var ignoreTargets = Weapon.Target.IsProjectile || Weapon.Target.TargetEntity is IMyCharacter;
                var scope = Weapon.GetScope;
                var trackingCheckPosition = scope.CachedPos;
                double rayDist = 0;


                if (Weapon.System.Session.DebugLos)
                {
                    var hitPos = hitInfo.Position;
                    if (rayDist <= 0) Vector3D.Distance(ref trackingCheckPosition, ref hitPos, out rayDist);

                    Weapon.System.Session.AddLosCheck(new Session.LosDebug { Part = Weapon, HitTick = Weapon.System.Session.Tick, Line = new LineD(trackingCheckPosition, hitPos) });
                }

                
                if (Weapon.Comp.Ai.ShieldNear)
                {
                    var targetPos = Weapon.Target.Projectile?.Position ?? Weapon.Target.TargetEntity.PositionComp.WorldMatrixRef.Translation;
                    var targetDir = targetPos - trackingCheckPosition;
                    if (Weapon.HitFriendlyShield(trackingCheckPosition, targetPos, targetDir))
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        return;
                    }
                }

                var hitTopEnt = (MyEntity)hitInfo?.HitEntity?.GetTopMostParent();
                if (hitTopEnt == null)
                {
                    if (ignoreTargets)
                        return;
                    masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckMiss);
                    if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckMiss);
                    return;
                }

                var targetTopEnt = Weapon.Target.TargetEntity?.GetTopMostParent();
                if (targetTopEnt == null)
                    return;

                var unexpectedHit = ignoreTargets || targetTopEnt != hitTopEnt;
                var topAsGrid = hitTopEnt as MyCubeGrid;

                if (unexpectedHit)
                {
                    if (hitTopEnt is MyVoxelBase)
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckVoxel);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckVoxel);
                        return;
                    }

                    if (topAsGrid == null)
                        return;
                    if (Weapon.Target.TargetEntity != null && (Weapon.Comp.IsBlock && topAsGrid.IsSameConstructAs(Weapon.Comp.Ai.GridEntity)))
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckSelfHit);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckSelfHit);
                        return;
                    }
                    if (!topAsGrid.DestructibleBlocks || topAsGrid.Immune || topAsGrid.GridGeneralDamageModifier <= 0 || !Session.GridEnemy(Weapon.Comp.Ai.AiOwner, topAsGrid))
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        return;
                    }
                    return;
                }
                if (Weapon.System.ClosestFirst && topAsGrid != null && topAsGrid == targetTopEnt)
                {
                    var halfExtMin = topAsGrid.PositionComp.LocalAABB.HalfExtents.Min();
                    var minSize = topAsGrid.GridSizeR * 8;
                    var maxChange = halfExtMin > minSize ? halfExtMin : minSize;
                    var targetPos = Weapon.Target.TargetEntity.PositionComp.WorldAABB.Center;
                    var weaponPos = trackingCheckPosition;

                    if (rayDist <= 0) Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var newHitShortDist = rayDist * (1 - hitInfo.Fraction);
                    var distanceToTarget = rayDist * hitInfo.Fraction;

                    var shortDistExceed = newHitShortDist - Weapon.Target.HitShortDist > maxChange;
                    var escapeDistExceed = distanceToTarget - Weapon.Target.OrigDistance > Weapon.Target.OrigDistance;
                    if (shortDistExceed || escapeDistExceed)
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckDistOffset);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckDistOffset);
                    }
                }
            }
        }

        internal class Muzzle
        {
            internal Muzzle(Weapon weapon, int id, Session session)
            {
                MuzzleId = id;
                UniqueId = session.NewVoxelCache.Id;
                Weapon = weapon;
            }

            internal Weapon Weapon;
            internal Vector3D Position;
            internal Vector3D Direction;
            internal Vector3D DeviatedDir;
            internal uint LastUpdateTick;
            internal uint LastAv1Tick;
            internal uint LastAv2Tick;
            internal int MuzzleId;
            internal ulong UniqueId;
            internal bool Av1Looping;
            internal bool Av2Looping;

        }

    }
}
