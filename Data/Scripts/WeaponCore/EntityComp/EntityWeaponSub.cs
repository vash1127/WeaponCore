using System;
using System.Collections.Generic;
using WeaponCore.Platform;
using static WeaponCore.Support.Ai;
namespace WeaponCore.Support
{
    public partial class CoreComponent 
    {


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
    }
}
