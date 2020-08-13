using System;
using Sandbox.ModAPI;
using WeaponCore.Platform;
using static WeaponCore.Support.GridAi;
namespace WeaponCore.Support
{
    public partial class WeaponComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            if (Platform.State != MyWeaponPlatform.PlatformState.Ready || Status != Start.Started)
                return;

            if (Ai?.LastTerminal == MyCube)  {

                TerminalBlock.RefreshCustomInfo();

                if (update && InControlPanel)
                {
                    MyCube.UpdateTerminal();
                }
            }
        }

        internal void RemoveComp()
        {
            try {

                if (Registered) 
                    RegisterEvents(false);

                if (Ai != null) {

                    Ai.OptimalDps -= PeakDps;
                    Ai.EffectiveDps -= EffectiveDps;

                    WeaponCount wCount;
                    if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount)) {
                        wCount.Current--;
                        Constructs.UpdateWeaponCounters(Ai);
                        if (wCount.Current == 0)
                        {
                            Ai.WeaponCounter.Remove(MyCube.BlockDefinition.Id.SubtypeId);
                            Session.WeaponCountPool.Return(wCount);
                        }
                    }
                    else if (Session.LocalVersion) Log.Line($"didnt find counter for: {MyCube.BlockDefinition.Id.SubtypeId} - {MyCube.BlockDefinition.Id.SubtypeId.String}");

                    if (Ai.Data.Repo.ActiveTerminal == MyCube.EntityId)
                        Ai.Data.Repo.ActiveTerminal = 0;

                    WeaponComponent comp;
                    if (Ai.WeaponBase.TryRemove(MyCube, out comp)) {
                        if (Platform.State == MyWeaponPlatform.PlatformState.Ready) {

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                                var w = comp.Platform.Weapons[i];
                                w.StopShooting();
                                w.WeaponCache.HitEntity.Clean();
                                if (!Session.IsClient) w.Target.Reset(Session.Tick, Target.States.AiLost);
                                if (w.DrawingPower)
                                    w.StopPowerDraw();
                            }
                        }
                        Ai.CompChange(false, this);
                    }
                    else Log.Line($"RemoveComp Weaponbase didn't have my comp: {Ai.Session.CompsDelayed.Contains(this)}");

                    if (Ai.WeaponBase.Count == 0) {
                        GridAi gridAi;
                        Session.GridTargetingAIs.TryRemove(Ai.MyGrid, out gridAi);
                    }

                    Ai = null;
                }
                else if (Platform.State != MyWeaponPlatform.PlatformState.Delay) Log.Line($"CompRemove: Ai already null - State:{Platform.State} - Status:{Status}");
            }
            catch (Exception ex) { Log.Line($"Exception in RemoveComp: {ex} - AiNull:{Ai == null} - SessionNull:{Session == null}"); }
        }

        internal static void SetRange(WeaponComponent comp)
        {
            foreach (var w in comp.Platform.Weapons)
                w.UpdateWeaponRange();
        }

        internal static void SetRof(WeaponComponent comp)
        {
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)  {
                var w = comp.Platform.Weapons[i];

                if (!w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon || w.ActiveAmmoDef.AmmoDef.Const.MustCharge) continue;

                var newRate = (int)(w.System.RateOfFire * comp.Data.Repo.Base.Set.RofModifier);
                if (newRate < 1)
                    newRate = 1;

                w.RateOfFire = newRate;
            }

            SetDps(comp);
        }

        internal static void SetDps(WeaponComponent comp, bool ammoChange = false)
        {
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                var w = comp.Platform.Weapons[i];
                if (!ammoChange && (!w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon || w.ActiveAmmoDef.AmmoDef.Const.MustCharge)) continue;
                comp.Session.FutureEvents.Schedule(w.SetWeaponDps, null, 1);
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

        public void StopAllGraphics()
        {
            foreach (var w in Platform.Weapons)
                w.StopBarrelAv = true;

            Session.Av.RunAvBarrels1();
            Session.Av.RunAvBarrels2();

        }

        internal void GeneralWeaponCleanUp()
        {
            if (Platform?.State == MyWeaponPlatform.PlatformState.Ready) {
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
            if (Platform?.State == MyWeaponPlatform.PlatformState.Ready)
            {
                foreach (var w in Platform.Weapons)
                {
                    for (int i = 0; i < w.System.Values.Assignments.Barrels.Length; i++)
                    {
                        if (w.HitEffects?[i] != null)
                        {
                            Log.Line($"[Clean CompHitPartice] Weapon:{w.System.WeaponName} - Particle:{w.HitEffects[i].GetName()}");
                            w.HitEffects[i].Stop();
                            w.HitEffects[i] = null;
                        }
                        if (w.BarrelEffects1?[i] != null)
                        {
                            Log.Line($"[Clean BarrelEffects1] Weapon:{w.System.WeaponName} - Particle:{w.BarrelEffects1[i].GetName()}");
                            w.BarrelEffects1[i].Stop();
                            w.BarrelEffects1[i] = null;
                        }
                        if (w.BarrelEffects2?[i] != null)
                        {
                            Log.Line($"[Clean BarrelEffects2] Weapon:{w.System.WeaponName} - Particle:{ w.BarrelEffects2[i].GetName()}");
                            w.BarrelEffects2[i].Stop();
                            w.BarrelEffects2[i] = null;
                        }
                    }
                }
            }
        }

        public void CleanCompSounds()
        {
            if (Platform?.State == MyWeaponPlatform.PlatformState.Ready)
            {
                foreach (var w in Platform.Weapons)
                {
                    if (w.AvCapable && w.System.FiringSound == WeaponSystem.FiringSoundState.WhenDone)
                    {
                        w.FiringEmitter.StopSound(true, true);
                        w.FiringEmitter.Entity = null;
                        w.System.Session.Emitters.Push(w.FiringEmitter);
                        w.System.Session.SoundPairs.Push(w.FiringSound);
                    }

                    if (w.AvCapable && w.System.PreFireSound)
                    {
                        w.PreFiringEmitter.StopSound(true, true);
                        w.PreFiringEmitter.Entity = null;
                        w.System.Session.Emitters.Push(w.PreFiringEmitter);
                        w.System.Session.SoundPairs.Push(w.PreFiringSound);
                    }


                    if (w.AvCapable && w.System.WeaponReloadSound)
                    {
                        w.ReloadEmitter.StopSound(true, true);
                        w.ReloadEmitter.Entity = null;
                        w.System.Session.Emitters.Push(w.ReloadEmitter);
                        w.System.Session.SoundPairs.Push(w.ReloadSound);
                    }

                    if (w.AvCapable && w.System.BarrelRotationSound)
                    {
                        w.RotateEmitter.StopSound(true, true);
                        w.RotateEmitter.Entity = null;
                        w.System.Session.Emitters.Push(w.RotateEmitter);
                        w.System.Session.SoundPairs.Push(w.RotateSound);
                    }

                }
            }
        }
    }
}
