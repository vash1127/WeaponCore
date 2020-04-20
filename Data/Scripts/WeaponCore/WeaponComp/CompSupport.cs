using System;
using WeaponCore.Platform;
using static WeaponCore.Session;
using static WeaponCore.Support.GridAi;
namespace WeaponCore.Support
{
    public partial class WeaponComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            if (Ai?.LastTerminal == MyCube)
            {
                TerminalBlock.RefreshCustomInfo();
            }

            if (update && InControlPanel)
            {
                MyCube.UpdateTerminal();
            }
        }

        internal void RemoveComp()
        {
            try
            {
                RegisterEvents(false);
                if (Ai != null)
                {
                    Ai.OptimalDps -= PeakDps;
                    Ai.EffectiveDps -= EffectiveDps;
                    WeaponComponent comp;
                    if (Ai.WeaponBase.TryRemove(MyCube, out comp))
                    {
                        if (Platform.State == MyWeaponPlatform.PlatformState.Ready)
                        {
                            WeaponCount wCount;
                            if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount))
                            {
                                wCount.Current--;
                                WeaponCount cntRemoved;
                                if (wCount.Current == 0) Ai.WeaponCounter.TryRemove(MyCube.BlockDefinition.Id.SubtypeId, out cntRemoved);
                            }

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.StopShooting();
                                w.WeaponCache.HitEntity.Clean();
                                if (w.DrawingPower)
                                    w.StopPowerDraw();
                            }
                        }
                        
                        if (Ai.WeaponBase.Count == 0)
                        {
                            WeaponCount wCount;
                            if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount))
                                Session.WeaponCountPool.Return(wCount);

                            GridAi gridAi;
                            Session.GridTargetingAIs.TryRemove(Ai.MyGrid, out gridAi);
                        }
                        Ai.CompChange(false, this);
                    }
                    else
                        Log.Line($"RemoveComp Weaponbase didn't have my comp");

                    Ai = null;
                }
                else Log.Line("Ai already null");
            }
            catch (Exception ex) { Log.Line($"Exception in RemoveComp: {ex} - AiNull:{Ai == null} - SessionNull:{Session == null}"); }
        }

        public void StopAllSounds()
        {
            foreach (var w in Platform.Weapons)
            {
                w.StopReloadSound();
                w.StopRotateSound();
                w.StopShooting(true);
            }
        }

        public void StopAllGraphics()
        {
            foreach (var w in Platform.Weapons)
                w.StopBarrelAv = true;

            Session.Av.RunAvBarrels1();
            Session.Av.RunAvBarrels2();

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

        public void StopAllAv()
        {
            if (Platform?.State != MyWeaponPlatform.PlatformState.Ready) return;
            StopAllSounds();
            StopAllGraphics();
        }
    }
}
