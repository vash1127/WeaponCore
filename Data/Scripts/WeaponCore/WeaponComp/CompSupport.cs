﻿using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.VisualScripting;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Support.GridAi;
namespace WeaponCore.Support
{
    public partial class WeaponComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            if (Ai.LastTerminal == MyCube)
            {
                if (IsSorterTurret)
                    SorterBase.RefreshCustomInfo();
                else
                    MissileBase.RefreshCustomInfo();
            }

            if (update && InControlPanel)
                 MyCube.UpdateTerminal();
        }

        private void SaveAndSendAll()
        {
            _firstSync = true;
            if (!_isServer) return;
            Set.SaveSettings();
            Set.NetworkUpdate();
            State.SaveState();
            State.NetworkUpdate();
        }

        internal void RemoveComp()
        {
            try
            {
                Ai.CompChange(false, this);
                RegisterEvents(false);
                Ai.OptimalDps -= OptimalDps;
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
                    }

                    else Log.Line($"RemoveComp platform not ready");

                }
                else
                    Log.Line($"RemoveComp Weaponbase didn't have my comp");

                if (Ai.WeaponBase.Count == 0)
                {
                    WeaponCount wCount;
                    if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount))
                        Session.WeaponCountPool.Return(wCount);

                    GridAi gridAi;
                    Session.GridTargetingAIs.TryRemove(Ai.MyGrid, out gridAi);
                }

                Ai = null;
            }
            catch (Exception ex) { Log.Line($"Exception in RemoveComp: {ex}"); }
        }

        public void StopRotSound(bool force)
        {
            if (Platform.RotationEmitter != null)
            {
                if (!Platform.RotationEmitter.IsPlaying)
                    return;
                Platform.RotationEmitter.StopSound(force);
            }
        }

        public void StopAllSounds()
        {
            Platform.RotationEmitter?.StopSound(true, true);
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
            {
                foreach (var barrels in w.BarrelAvUpdater)
                {
                    var id = barrels.Key.MuzzleId;
                    if (w.System.BarrelEffect1)
                    {
                        if (w.BarrelEffects1?[id] != null)
                        {
                            w.BarrelEffects1[id].Stop(true);
                            w.BarrelEffects1[id] = null;
                        }
                    }
                    if (w.System.BarrelEffect2)
                    {
                        if (w.BarrelEffects2?[id] != null)
                        {
                            w.BarrelEffects2[id].Stop(true);
                            w.BarrelEffects2[id] = null;
                        }
                    }
                    if (w.HitEffects?[id] != null)
                    {
                        w.HitEffects[id].Stop(true);
                        w.HitEffects[id] = null;
                    }
                }
            }
        }

        public void StopAllAv()
        {
            if (Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            StopAllSounds();
            StopAllGraphics();
        }
    }
}
