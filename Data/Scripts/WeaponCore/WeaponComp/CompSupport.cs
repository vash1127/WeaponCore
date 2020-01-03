using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.VisualScripting;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;

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

        internal void RemoveSinkDelegate(object o)
        {
            SinkInfo.RequiredInputFunc = null;
            MyCube.ResourceSink.Init(MyStringHash.GetOrCompute("Charging"), SinkInfo);
        }

        internal void RemoveComp()
        {
            try
            {
                WeaponComponent comp;
                if (Ai.WeaponBase.TryRemove(MyCube, out comp))
                {
                    comp.RemoveCompList();
                    if (Platform.State == MyWeaponPlatform.PlatformState.Ready)
                    {
                        WeaponCount wCount;
                        if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount))
                        {
                            wCount.Current--;
                            WeaponCount cntRemoved;
                            if (wCount.Current == 0) Ai.WeaponCounter.TryRemove(MyCube.BlockDefinition.Id.SubtypeId, out cntRemoved);
                        }

                        for (int i = 0; i < Platform.Weapons.Length; i++)
                        {
                            var w = Platform.Weapons[i];
                            w.StopShooting();
                            w.WeaponCache.HitEntity.Clean();
                            if (w.DrawingPower)
                                w.StopPowerDraw();
                        }

                        StopAllSounds();
                        Platform.RemoveParts(this);
                        
                        Ai.OptimalDps -= OptimalDps;
                        
                    }
                    else Log.Line($"RemoveComp platform not ready");
                }
                else
                {
                    Log.Line($"no comp found to remove: {MyCube.DebugName} - [marked](myCube:{MyCube.MarkedForClose} - myGrid:{MyCube.CubeGrid.MarkedForClose} - AiGrid:{Ai.MyGrid.MarkedForClose}) - gridMismatch:{MyCube.CubeGrid != Ai.MyGrid})");
                    GridAi gridAi;
                    if (Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out gridAi))
                    {
                        Log.Line($"cube matches different grid {MyCube.DebugName} - [marked](myCube:{MyCube.MarkedForClose} - myGrid:{MyCube.CubeGrid.MarkedForClose} - AiGrid:{Ai.MyGrid.MarkedForClose}) - gridMismatch:{MyCube.CubeGrid != Ai.MyGrid})");
                        if (gridAi.WeaponBase.TryRemove(MyCube, out comp))
                        {
                            Log.Line($"cube FOUND in other grid's Ai");
                            comp.RemoveCompList();

                            if (Platform.State == MyWeaponPlatform.PlatformState.Ready)
                            {
                                WeaponCount wCount;
                                if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount))
                                {
                                    wCount.Current--;
                                    WeaponCount cntRemoved;
                                    if (wCount.Current == 0) Ai.WeaponCounter.TryRemove(MyCube.BlockDefinition.Id.SubtypeId, out cntRemoved);
                                }

                                for (int i = 0; i < Platform.Weapons.Length; i++)
                                {
                                    var w = Platform.Weapons[i];
                                    w.StopShooting();
                                    w.WeaponCache.HitEntity.Clean();
                                    if (w.DrawingPower)
                                        w.StopPowerDraw();
                                }

                                StopAllSounds();
                                Platform.RemoveParts(this);

                                Ai.OptimalDps -= OptimalDps;

                            }
                        }
                        else Log.Line($"cube NOT found in other grid's Ai");
                    }
                }

                if (Ai.WeaponBase.Count == 0)
                {
                    WeaponCount wCount;
                    if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount))
                        Ai.Session.WeaponCountPool.Return(wCount);

                    GridAi gridAi;
                    Ai.Session.GridTargetingAIs.TryRemove(Ai.MyGrid, out gridAi);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in RemoveComp: {ex}"); }
        }

        internal void AddCompList()
        {
            GridAi gridAi;
            if (Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out gridAi) && gridAi != Ai)
            {
                Log.Line($"AddCompList grid mismatch");
            }
            if (Ai.WeaponsIdx.ContainsKey(this))
            {
                Log.Line($"add failure: aiContains:{Ai.WeaponBase.ContainsKey(MyCube)} - Marked:{MyCube.MarkedForClose} - AiGridMatch:{Ai.MyGrid == MyCube.CubeGrid} - hasGridAi:{Ai.Session.GridTargetingAIs.ContainsKey(MyCube.CubeGrid)}");

                GridAi gridAiTmp;
                if (Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out gridAiTmp))
                {
                    Log.Line($"gridAiHasMyComp:{gridAiTmp.WeaponBase.ContainsKey(MyCube)}");
                }
                return;
            }
            Ai.WeaponsIdx.Add(this, Ai.Weapons.Count);
            Ai.Weapons.Add(this);
        }

        internal void RemoveCompList()
        {
            GridAi gridAi;
            if (Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out gridAi) && gridAi != Ai)
            {
                Log.Line($"RemoveCompList grid mismatch");
            }

            int idx;
            if (!Ai.WeaponsIdx.TryGetValue(this, out idx))
            {
                Log.Line($"remove failure: aiContains:{Ai.WeaponBase.ContainsKey(MyCube)} - Marked:{MyCube.MarkedForClose} - AiGridMatch:{Ai.MyGrid == MyCube.CubeGrid}  - hasGridAi:{Ai.Session.GridTargetingAIs.ContainsKey(MyCube.CubeGrid)}");
                GridAi gridAiTmp;
                if (Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out gridAiTmp))
                {
                    Log.Line($"gridAiHasMyComp:{gridAiTmp.WeaponBase.ContainsKey(MyCube)}");
                }
                return;
            }

            Ai.Weapons.RemoveAtFast(idx);
            if (idx < Ai.Weapons.Count)
                Ai.WeaponsIdx[Ai.Weapons[idx]] = idx;

            Ai.WeaponsIdx.Remove(this);
        }

        public void StopRotSound(bool force)
        {
            if (RotationEmitter != null)
            {
                if (!RotationEmitter.IsPlaying)
                    return;
                RotationEmitter.StopSound(force);
            }
        }

        public void StopAllSounds()
        {
            RotationEmitter?.StopSound(true, true);
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
