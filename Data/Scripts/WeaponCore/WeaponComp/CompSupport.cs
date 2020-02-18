using System;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
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

        internal void SaveAndSendAll()
        {
            _firstSync = true;
            if (_isServer || _isDedicated)
            {
                Set.SaveSettings();
                Set.NetworkUpdate();
                State.SaveState();
                State.NetworkUpdate();
            }
            else
            {
                Set.NetworkUpdate();
                State.NetworkUpdate();
            }
        }

        internal void UpdateStateMP()
        {
            if(Session.IsMultiplayer)
                State.NetworkUpdate();
        }

        internal void UpdateSettingsMP()
        {
            if (Session.IsMultiplayer)
                Set.NetworkUpdate();
        }

        internal void RemoveComp()
        {
            try
            {
                RegisterEvents(false);
                if (Ai != null)
                {
                    Ai.CompChange(false, this);
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

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.StopShooting();
                                w.WeaponCache.HitEntity.Clean();
                                if (w.DrawingPower)
                                    w.StopPowerDraw();
                            }
                        }
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
            }
            catch (Exception ex) { Log.Line($"Exception in RemoveComp: {ex} - AiNull:{Ai == null} - SessionNull:{Session == null}"); }
        }

        internal void SavePartStates()
        {
            ResettingSubparts = true;
            for(int i = 0; i < SubpartStatesQuickList.Count; i++)
            {
                var part = SubpartStatesQuickList[i];
                SubpartStates[part] =  part.PositionComp.LocalMatrix;
                Log.Line($"Saved matrix {i}: {part.PositionComp.LocalMatrix}");
            }
        }

        /*internal void RestorePartStates(object o = null)
        {
            for (int i = 0; i < AllAnimations.Count; i++)
            {
                if (AllAnimations[i].Paused)
                    AllAnimations[i].Paused = false;
            }

            for (int i = 0; i < SubpartStatesQuickList.Count; i++)
            {
                if (!Session.VanillaSubpartNames.Contains(SubpartIndexToName[i]) || BaseType != BlockType.Turret)
                {
                    var part = SubpartStatesQuickList[i];
                    part.PositionComp.LocalMatrix = SubpartStates[part];
                    Log.Line($"loaded matrix {i}: {part.PositionComp.LocalMatrix}");
                }
            }

            if(BaseType == BlockType.Turret)
            {
                //TurretBase.Elevation = (float)Elevation;
                //TurretBase.Azimuth = (float)Azimuth;
            }
            ResettingSubparts = false;
        }*/

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

        public void StopAllAv()
        {
            if (Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            StopAllSounds();
            StopAllGraphics();
        }
    }
}
