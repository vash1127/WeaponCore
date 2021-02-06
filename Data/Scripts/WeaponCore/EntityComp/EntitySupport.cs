using System;
using System.Collections.Generic;
using WeaponCore.Platform;
using static WeaponCore.Support.Ai;
namespace WeaponCore.Support
{
    public partial class CoreComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            if (Platform.State != CorePlatform.PlatformState.Ready || Status != Start.Started)
                return;

            if (Ai?.LastTerminal == CoreEntity)  {

                TerminalBlock.RefreshCustomInfo();

                if (update && InControlPanel)
                {
                    Cube.UpdateTerminal();
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

                    PartCounter wCount;
                    if (Ai.PartCounting.TryGetValue(SubTypeId, out wCount)) {
                        wCount.Current--;
                        Constructs.UpdatePartCounters(Ai);
                        if (wCount.Current == 0)
                        {
                            Ai.PartCounting.Remove(SubTypeId);
                            Session.PartCountPool.Return(wCount);
                        }
                    }
                    else if (Session.LocalVersion) Log.Line($"didnt find counter for: {SubTypeId} - MarkedForClose:{Ai.MarkedForClose} - AiAge:{Ai.Session.Tick - Ai.AiSpawnTick} - CubeMarked:{CoreEntity.MarkedForClose} - GridMarked:{TopEntity.MarkedForClose}");

                    if (Ai.Data.Repo.ActiveTerminal == CoreEntity.EntityId)
                        Ai.Data.Repo.ActiveTerminal = 0;
                    
                    Ai testAi;
                    CoreComponent comp;
                    if (Ai.PartBase.TryRemove(CoreEntity, out comp)) {
                        if (Platform.State == CorePlatform.PlatformState.Ready) {

                            for (int i = 0; i < comp.Platform.Weapons.Count; i++) {
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
                    else Log.Line($"RemoveComp Weaponbase didn't have my comp: {Ai.Session.CompsDelayed.Contains(this)} - FoundAi:{Ai.Session.GridTargetingAIs.TryGetValue(TopEntity, out testAi)} - sameAi:{testAi == Ai}");

                    if (Ai.PartBase.Count == 0) {
                        Ai ai;
                        Session.GridTargetingAIs.TryRemove(Ai.TopEntity, out ai);
                    }

                    Ai = null;
                }
                else if (Platform.State != CorePlatform.PlatformState.Delay) Log.Line($"CompRemove: Ai already null - State:{Platform.State} - Status:{Status}");
            }
            catch (Exception ex) { Log.Line($"Exception in RemoveComp: {ex} - AiNull:{Ai == null} - SessionNull:{Session == null}"); }
        }
    }
}
