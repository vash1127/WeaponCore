using System;
using System.Collections.Generic;
using WeaponCore.Platform;
using static WeaponCore.Support.Ai;
namespace WeaponCore.Support
{
    public partial class CoreComponent 
    {
        internal static void SetRange(CoreComponent comp)
        {
            foreach (var w in comp.Platform.Weapons)
                w.UpdateWeaponRange();
        }

        internal static void SetRof(CoreComponent comp)
        {
            for (int i = 0; i < comp.Platform.Weapons.Count; i++)  {
                var w = comp.Platform.Weapons[i];

                if (w.ActiveAmmoDef.AmmoDef.Const.MustCharge) continue;

                w.UpdateRof();
            }

            SetDps(comp);
        }

        internal static void SetDps(CoreComponent comp, bool change = false)
        {
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            for (int i = 0; i < comp.Platform.Weapons.Count; i++) {
                var w = comp.Platform.Weapons[i];
                if (!change && (!w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon || w.ActiveAmmoDef.AmmoDef.Const.MustCharge)) continue;
                comp.Session.FutureEvents.Schedule(w.SetWeaponDps, null, 1);
            }
        }

        internal void GeneralWeaponCleanUp()
        {
            if (Platform?.State == CorePlatform.PlatformState.Ready) {
                foreach (var w in Platform.Weapons) {

                    w.RayCallBackClean();

                    w.Comp.Session.AcqManager.Asleep.Remove(w.Acquire);
                    w.Comp.Session.AcqManager.MonitorState.Remove(w.Acquire);
                    w.Acquire.Monitoring = false;
                    w.Acquire.IsSleeping = false;
                    
                }
            }
        }

        public void CleanCompParticles()
        {
            if (Platform?.State == CorePlatform.PlatformState.Ready)
            {
                foreach (var w in Platform.Weapons)
                {
                    for (int i = 0; i < w.System.Values.Assignments.Barrels.Length; i++)
                    {
                        if (w.HitEffects?[i] != null)
                        {
                            Log.Line($"[Clean CompHitPartice] Weapon:{w.System.PartName} - Particle:{w.HitEffects[i].GetName()}");
                            w.HitEffects[i].Stop();
                            w.HitEffects[i] = null;
                        }
                        if (w.Effects1?[i] != null)
                        {
                            Log.Line($"[Clean Effects1] Weapon:{w.System.PartName} - Particle:{w.Effects1[i].GetName()}");
                            w.Effects1[i].Stop();
                            w.Effects1[i] = null;
                        }
                        if (w.Effects2?[i] != null)
                        {
                            Log.Line($"[Clean Effects2] Weapon:{w.System.PartName} - Particle:{ w.Effects2[i].GetName()}");
                            w.Effects2[i].Stop();
                            w.Effects2[i] = null;
                        }
                    }
                }
            }
        }

        public void CleanCompSounds()
        {
            if (Platform?.State == CorePlatform.PlatformState.Ready) {

                foreach (var w in Platform.Weapons) {

                    if (w.AvCapable && w.System.FiringSound == CoreSystem.FiringSoundState.WhenDone)
                        Session.SoundsToClean.Add(new Session.CleanSound {Force = true, Emitter = w.FiringEmitter, EmitterPool = Session.Emitters, SoundPair = w.FiringSound, SoundPairPool = w.System.FireWhenDonePairs, SpawnTick = Session.Tick});

                    if (w.AvCapable && w.System.PreFireSound)
                        Session.SoundsToClean.Add(new Session.CleanSound { Force = true, Emitter = w.PreFiringEmitter, EmitterPool = Session.Emitters, SoundPair = w.PreFiringSound, SoundPairPool = w.System.PreFirePairs, SpawnTick = Session.Tick });

                    if (w.AvCapable && w.System.WeaponReloadSound)
                        Session.SoundsToClean.Add(new Session.CleanSound { Force = true, Emitter = w.ReloadEmitter, EmitterPool = Session.Emitters, SoundPair = w.ReloadSound, SoundPairPool = w.System.ReloadPairs, SpawnTick = Session.Tick });

                    if (w.AvCapable && w.System.BarrelRotationSound)
                        Session.SoundsToClean.Add(new Session.CleanSound { Emitter = w.RotateEmitter, EmitterPool = Session.Emitters, SoundPair = w.RotateSound, SoundPairPool = w.System.RotatePairs, SpawnTick = Session.Tick });
                }

                if (Session.PurgedAll)
                {
                    Session.CleanSounds();
                    Log.Line($"purged already called");
                }
            }
        }

        public void StopAllSounds()
        {
            foreach (var w in Platform.Weapons)
            {
                w.StopReloadSound();
                w.StopRotateSound();
                w.StopShootingAv(false);
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

        internal bool ShootOnceCheck(int weaponToCheck = -1)
        {
            var checkAllWeapons = weaponToCheck == -1;
            var numOfWeapons = checkAllWeapons ? Platform.Weapons.Count : 1;
            var loadedWeapons = 0;

            for (int i = 0; i < Platform.Weapons.Count; i++)
            {
                var w = Platform.Weapons[i];

                if (w.State.Overheated)
                    return false;

                if ((w.Ammo.CurrentAmmo > 0 || w.System.DesignatorWeapon) && (checkAllWeapons || weaponToCheck == i))
                    ++loadedWeapons;
            }
            if (numOfWeapons == loadedWeapons)
            {

                for (int i = 0; i < Platform.Weapons.Count; i++)
                {

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
            else if (action == TriggerActions.TriggerClick || action == TriggerActions.TriggerOnce || action == TriggerActions.TriggerOn)
                Data.Repo.Base.State.Control = CompStateValues.ControlMode.Toolbar;
            else
                Data.Repo.Base.State.Control = CompStateValues.ControlMode.None;

            playerId = Session.HandlesInput && playerId == -1 ? Session.PlayerId : playerId;
            var newId = action == TriggerActions.TriggerOff && !Data.Repo.Base.State.TrackingReticle ? -1 : playerId;
            Data.Repo.Base.State.PlayerId = newId;
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
                    PeakDps += ammo.Const.PeakDps / (float)patternSize;
                    EffectiveDps += ammo.Const.EffectiveDps / (float)patternSize;
                    ShotsPerSec += ammo.Const.ShotsPerSec / (float)patternSize;
                    BaseDps += ammo.Const.BaseDps / (float)patternSize;
                    AreaDps += ammo.Const.AreaDps / (float)patternSize;
                    DetDps += ammo.Const.DetDps / (float)patternSize;
                }
            }

            maxTrajectory = 0;
            if (weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory > maxTrajectory)
                maxTrajectory = weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;

            if (weapon.System.TrackProjectile)
                Ai.PointDefense = true;
        }

    }
}
