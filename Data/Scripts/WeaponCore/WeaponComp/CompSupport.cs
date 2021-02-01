using System;
using System.Collections.Generic;
using WeaponCore.Platform;
using static WeaponCore.Support.GridAi;
namespace WeaponCore.Support
{
    public partial class CoreComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            if (Platform.State != CorePlatform.PlatformState.Ready || Status != Start.Started)
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
                    else if (Session.LocalVersion) Log.Line($"didnt find counter for: {MyCube.BlockDefinition.Id.SubtypeId} - MarkedForClose:{Ai.MarkedForClose} - AiAge:{Ai.Session.Tick - Ai.AiSpawnTick} - CubeMarked:{MyCube.MarkedForClose} - GridMarked:{MyCube.CubeGrid.MarkedForClose}");

                    if (Ai.Data.Repo.ActiveTerminal == MyCube.EntityId)
                        Ai.Data.Repo.ActiveTerminal = 0;
                    
                    GridAi testAi;
                    CoreComponent comp;
                    if (Ai.WeaponBase.TryRemove(MyCube, out comp)) {
                        if (Platform.State == CorePlatform.PlatformState.Ready) {

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
                    else Log.Line($"RemoveComp Weaponbase didn't have my comp: {Ai.Session.CompsDelayed.Contains(this)} - FoundAi:{Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out testAi)} - sameAi:{testAi == Ai}");

                    if (Ai.WeaponBase.Count == 0) {
                        GridAi gridAi;
                        Session.GridTargetingAIs.TryRemove(Ai.MyGrid, out gridAi);
                    }

                    Ai = null;
                }
                else if (Platform.State != CorePlatform.PlatformState.Delay) Log.Line($"CompRemove: Ai already null - State:{Platform.State} - Status:{Status}");
            }
            catch (Exception ex) { Log.Line($"Exception in RemoveComp: {ex} - AiNull:{Ai == null} - SessionNull:{Session == null}"); }
        }

        internal static void SetRange(CoreComponent comp)
        {
            foreach (var w in comp.Platform.Weapons)
                w.UpdateWeaponRange();
        }

        internal static void SetRof(CoreComponent comp)
        {
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)  {
                var w = comp.Platform.Weapons[i];

                if (w.ActiveAmmoDef.ConsumableDef.Const.MustCharge) continue;

                w.UpdateRof();
            }

            SetDps(comp);
        }

        internal static void SetDps(CoreComponent comp, bool change = false)
        {
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                var w = comp.Platform.Weapons[i];
                if (!change && (!w.ActiveAmmoDef.ConsumableDef.Const.IsBeamWeapon || w.ActiveAmmoDef.ConsumableDef.Const.MustCharge)) continue;
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
    }
}
