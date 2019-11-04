using System;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.VisualScripting;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            if (IsSorterTurret)
                SorterBase.RefreshCustomInfo();
            else
                MissileBase.RefreshCustomInfo();

            if (update && InControlPanel)
            {
                 MyCube.UpdateTerminal();
            }
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
                WeaponComponent comp;
                if (Ai.WeaponBase.TryRemove(MyCube, out comp))
                {
                    //Log.Line($"removing Comp:{MyCube.DebugName} marked:{MyCube.MarkedForClose} - gridMismatch:{MyCube.CubeGrid != Ai.MyGrid} - grid:{MyCube.CubeGrid.DebugName}({Ai.MyGrid.DebugName})");
                    if (Platform != null && Platform.Inited)
                    {
                        GridAi.WeaponCount wCount;

                        if (Ai.WeaponCounter.TryGetValue(MyCube.BlockDefinition.Id.SubtypeId, out wCount))
                            wCount.Current--;

                        RegisterEvents(false);
                        StopAllSounds();
                        Platform.RemoveParts(this);

                        foreach (var groupName in GroupNames)
                            Ai.BlockGroups[groupName].Remove(MyCube);

                        GroupNames.Clear();
                        Ai.TotalSinkPower -= MaxRequiredPower;
                        Ai.OptimalDps -= OptimalDps;
                    }
                    else Log.Line("platform not initted");
                }
                else
                {
                    Log.Line($"no comp found to remove: {MyCube.DebugName} - marked:{MyCube.MarkedForClose} - gridMismatch:{MyCube.CubeGrid != Ai.MyGrid} - grid:{MyCube.CubeGrid.DebugName}({Ai.MyGrid.DebugName})");
                    GridAi gridAi;
                    if (Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out gridAi))
                    {
                        Log.Line($"cube matches different grid: marked:{MyCube.MarkedForClose}({gridAi.MyGrid.MarkedForClose}) - gridMisMatch: {gridAi.MyGrid != MyCube.CubeGrid} - grid:{MyCube.CubeGrid.DebugName}({Ai.MyGrid.DebugName})");
                        if (gridAi.WeaponBase.TryRemove(MyCube, out comp))
                        {
                            Log.Line($"cube removed from old grid");
                        }
                        else Log.Line($"cube not found in old grid: marked:{MyCube.MarkedForClose} - inScene:{MyCube.InScene}");
                    }
                    else Log.Line($"cube doesn't match any grid: grid:{MyCube.CubeGrid.DebugName}({Ai.MyGrid.DebugName})");
                }

                if (Ai.WeaponBase.Count == 0)
                {
                    GridAi gridAi;
                    if (Ai.Session.GridTargetingAIs.TryRemove(Ai.MyGrid, out gridAi))
                    {
                        //Log.Line($"remove gridAi: {Ai.MyGrid.DebugName} - gridMismatch:{Ai.MyGrid != MyCube.CubeGrid}");
                    }
                   // else Log.Line($"no gridAi found for:{Ai.MyGrid.DebugName}({Ai.MyGrid.MarkedForClose}) - {MyCube.DebugName}({MyCube.CubeGrid.MarkedForClose})");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in RemoveComp: {ex}"); }
        }

        internal void UpdateCompPower()
        {
            var shooting = false;
            for (int i = 0; i < Platform.Weapons.Length; i++)
            {
                if (Platform.Weapons[i].IsShooting && Platform.Weapons[i].System.EnergyAmmo) shooting = true;
            }
            if (shooting)
            {
                if (!Ai.AvailablePowerIncrease)
                {
                    if (Ai.ResetPower)
                    {
                        //Log.Line($"grid available: {Ai.GridAvailablePower + Ai.CurrentWeaponsDraw}");
                        Ai.WeaponCleanPower = Ai.GridMaxPower - (Ai.GridCurrentPower - Ai.CurrentWeaponsDraw);
                        Ai.ResetPower = false;
                    }

                    SinkPower = CompPowerPerc * Ai.WeaponCleanPower;

                    DelayTicks += (uint)(5 * MaxRequiredPower / SinkPower) - DelayTicks;
                    ShootTick = DelayTicks + Ai.Session.Tick;
                    Ai.RecalcDone = true;
                }
                else
                {
                    SinkPower = CurrentSinkPowerRequested;
                    Ai.ResetPower = true;
                }

                Sink.Update();
                TerminalRefresh();
            }            
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
            if (Platform == null)
            {
                Log.Line($"[StopAllSounds] MyCubeId:{MyCube.EntityId} - Grid:{MyCube.CubeGrid.DebugName} - WeaponName:{MyCube.BlockDefinition.Id.SubtypeId.String} - !Marked:{!MyCube.MarkedForClose} - inScene:{MyCube.InScene} - gridMatch:{MyCube.CubeGrid == Ai.MyGrid}");
                return;
            }
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
            StopAllSounds();
            StopAllGraphics();
        }
    }
}
