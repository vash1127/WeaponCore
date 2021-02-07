using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.WeaponRandomGenerator;

namespace WeaponCore.Support
{
    public partial class Ai
    {
        internal void CompChange(bool add, CoreComponent comp)
        {
            int idx;
            switch (comp.Type)
            {
                case CoreComponent.CompType.Weapon:
                    var wComp = (Weapon.WeaponComponent)comp;

                    if (add)
                    {
                        if (WeaponIdx.ContainsKey(wComp))
                        {
                            Log.Line($"CompAddFailed:<{wComp.CoreEntity.EntityId}> - comp({wComp.CoreEntity.DebugName}[{wComp.SubtypeName}]) already existed in {TopEntity.DebugName}");
                            return;
                        }

                        WeaponIdx.Add(wComp, WeaponComps.Count);
                        WeaponComps.Add(wComp);
                    }
                    else
                    {
                        if (!WeaponIdx.TryGetValue(wComp, out idx)) {
                            Log.Line($"CompRemoveFailed: <{wComp.CoreEntity.EntityId}> - {WeaponComps.Count}[{WeaponIdx.Count}]({CompBase.Count}) - {WeaponComps.Contains(wComp)}[{WeaponComps.Count}] - {Session.GridAIs[wComp.TopEntity].CompBase.ContainsKey(wComp.CoreEntity)} - {Session.GridAIs[wComp.TopEntity].CompBase.Count} ");
                            return;
                        }

                        WeaponComps.RemoveAtFast(idx);
                        if (idx < WeaponComps.Count)
                            WeaponIdx[WeaponComps[idx]] = idx;
                        WeaponIdx.Remove(wComp);
                    }

                    break;
                case CoreComponent.CompType.Upgrade:
                    var uComp = (Upgrade.UpgradeComponent)comp;

                    if (add)
                    {
                        if (UpgradeIdx.ContainsKey(uComp))
                        {
                            Log.Line($"CompAddFailed:<{uComp.CoreEntity.EntityId}> - comp({uComp.CoreEntity.DebugName}[{uComp.SubtypeName}]) already existed in {TopEntity.DebugName}");
                            return;
                        }

                        UpgradeIdx.Add(uComp, UpgradeComps.Count);
                        UpgradeComps.Add(uComp);
                    }
                    else
                    {
                        if (!UpgradeIdx.TryGetValue(uComp, out idx))
                        {
                            Log.Line($"CompRemoveFailed: <{uComp.CoreEntity.EntityId}> - {WeaponComps.Count}[{UpgradeIdx.Count}]({CompBase.Count}) - {UpgradeComps.Contains(uComp)}[{WeaponComps.Count}] - {Session.GridAIs[uComp.TopEntity].CompBase.ContainsKey(uComp.CoreEntity)} - {Session.GridAIs[uComp.TopEntity].CompBase.Count} ");
                            return;
                        }

                        UpgradeComps.RemoveAtFast(idx);
                        if (idx < UpgradeComps.Count)
                            UpgradeIdx[UpgradeComps[idx]] = idx;
                        UpgradeIdx.Remove(uComp);
                    }


                    break;
                case CoreComponent.CompType.Support:

                    var sComp = (SupportSys.SupportComponent)comp;
                    if (add)
                    {
                        if (SupportIdx.ContainsKey(sComp))
                        {
                            Log.Line($"CompAddFailed:<{sComp.CoreEntity.EntityId}> - comp({sComp.CoreEntity.DebugName}[{sComp.SubtypeName}]) already existed in {TopEntity.DebugName}");
                            return;
                        }

                        SupportIdx.Add(sComp, SupportComps.Count);
                        SupportComps.Add(sComp);
                    }
                    else
                    {
                        if (!SupportIdx.TryGetValue(sComp, out idx))
                        {
                            Log.Line($"CompRemoveFailed: <{sComp.CoreEntity.EntityId}> - {WeaponComps.Count}[{SupportIdx.Count}]({CompBase.Count}) - {SupportComps.Contains(sComp)}[{SupportComps.Count}] - {Session.GridAIs[sComp.TopEntity].CompBase.ContainsKey(sComp.CoreEntity)} - {Session.GridAIs[sComp.TopEntity].CompBase.Count} ");
                            return;
                        }

                        SupportComps.RemoveAtFast(idx);
                        if (idx < SupportComps.Count)
                            SupportIdx[SupportComps[idx]] = idx;
                        SupportIdx.Remove(sComp);
                    }

                    break;
                case CoreComponent.CompType.Phantom:
                    var pComp = (Phantom.PhantomComponent)comp;
                    if (add)
                    {
                        if (PhantomIdx.ContainsKey(pComp))
                        {
                            Log.Line($"CompAddFailed:<{pComp.CoreEntity.EntityId}> - comp({pComp.CoreEntity.DebugName}[{pComp.SubtypeName}]) already existed in {TopEntity.DebugName}");
                            return;
                        }

                        PhantomIdx.Add(pComp, PhantomComps.Count);
                        PhantomComps.Add(pComp);
                    }
                    else
                    {
                        if (!PhantomIdx.TryGetValue(pComp, out idx))
                        {
                            Log.Line($"CompRemoveFailed: <{pComp.CoreEntity.EntityId}> - {WeaponComps.Count}[{PhantomIdx.Count}]({CompBase.Count}) - {PhantomComps.Contains(pComp)}[{PhantomComps.Count}] - {Session.GridAIs[pComp.TopEntity].CompBase.ContainsKey(pComp.CoreEntity)} - {Session.GridAIs[pComp.TopEntity].CompBase.Count} ");
                            return;
                        }

                        PhantomComps.RemoveAtFast(idx);
                        if (idx < SupportComps.Count)
                            PhantomIdx[PhantomComps[idx]] = idx;
                        PhantomIdx.Remove(pComp);
                    }

                    break;
            }
        }
        
        private static int[] GetDeck(ref int[] deck, ref int prevDeckLen, int firstCard, int cardsToSort, int cardsToShuffle, WeaponRandomGenerator rng, RandomType type)
        {
            var count = cardsToSort - firstCard;
            if (prevDeckLen < count) {
                deck = new int[count];
                prevDeckLen = count;
            }

            Random rnd;
            if (type == RandomType.Acquire) {
                rnd = rng.AcquireRandom;
                rng.AcquireCurrentCounter += count;
            }
            else {
                rnd = rng.ClientProjectileRandom;
                rng.ClientProjectileCurrentCounter += count;
            }

            for (int i = 0; i < count; i++) {

                var j = i < cardsToShuffle ? rnd.Next(i + 1) : i;
                deck[i] = deck[j];
                deck[j] = firstCard + i;
            }
            return deck;
        }


        internal void ComputeAccelSphere()
        {
            NearByEntityCache.Clear();
            if (MarkedForClose) return;

            AccelChecked = true;

            var numOfEntities = NearByEntities > 0 ? NearByEntities : 1f;
            var ratio = (MyProjectiles / numOfEntities) / 10f;
            var checkVol = Math.Max(ratio > 1 ? ScanVolume.Radius : ScanVolume.Radius * ratio, 500f);
            NearByEntitySphere = new BoundingSphereD(TopEntity.PositionComp.WorldAABB.Center, checkVol);
            var qType = ClosestStaticSqr < (checkVol * 2) * (checkVol * 2) ? MyEntityQueryType.Both : MyEntityQueryType.Dynamic;
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref NearByEntitySphere, NearByEntityCache, qType);
        }

        internal List<Projectile> GetProCache()
        {
            if (LiveProjectileTick > _pCacheTick) {
                ProjetileCache.Clear();
                ProjetileCache.AddRange(LiveProjectile);
                _pCacheTick = LiveProjectileTick;
            }
            return ProjetileCache;
        }

        private void WeaponShootOff()
        {
            for (int i = 0; i < WeaponComps.Count; i++) {

                var comp = WeaponComps[i];
                for (int x = 0; x < comp.Platform.Weapons.Count; x++) {
                    var w = comp.Platform.Weapons[x];
                    w.StopReloadSound();
                    w.StopShooting();
                }
            }
        }

        internal void UpdateGridPower()
        {
            try
            {
                if (CompBase.Count == 0) {
                    Log.Line($"no valid weapon in powerDist");
                    return;
                }

                GridCurrentPower = 0;
                GridMaxPower = 0;
                var first = true;
                foreach (var comp in CompBase.Values)
                {
                    if (!comp.IsBlock)
                        continue;

                    var powerBlock = first ? PowerBlock : comp.Cube;

                    if (powerBlock == null || first && PowerDirty) {
                        first = false;
                        continue;
                    }

                    first = false;
                    using (powerBlock.Pin()) {
                        using (powerBlock.CubeGrid.Pin()) {
                            try {

                                if (powerBlock.MarkedForClose || powerBlock.SlimBlock == null  || powerBlock.CubeGrid.MarkedForClose) 
                                    continue;

                                try {
                                    if (PowerBlock != powerBlock || PowerDistributor?.SourcesEnabled == MyMultipleEnabledEnum.NoObjects) {
                                        PowerBlock = powerBlock;
                                        FakeShipController.SlimBlock = powerBlock.SlimBlock;
                                        PowerDistributor = FakeShipController.GridResourceDistributor;
                                        PowerDirty = false;
                                    }
                                }
                                catch (Exception ex) { Log.Line($"Exception in UpdateGridPower: {ex} - Changed PowerBlock!"); }

                                if (PowerDistributor == null) {
                                    Log.Line($"powerDist is null");
                                    return;
                                }

                                try {
                                    GridMaxPower = PowerDistributor.MaxAvailableResourceByType(GId);
                                    GridCurrentPower = PowerDistributor.TotalRequiredInputByType(GId);
                                    break;
                                }
                                catch (Exception ex) { Log.Line($"Exception in UpdateGridPower: {ex} - impossible null!"); }

                            }
                            catch (Exception ex) { Log.Line($"Exception in UpdateGridPower: {ex} - main null catch"); }
                        }

                    }
                    Log.Line($"no valid power blocks");
                    return;
                }

                if (Session.Tick60) {

                    BatteryMaxPower = 0;
                    BatteryCurrentOutput = 0;
                    BatteryCurrentInput = 0;

                    foreach (var battery in Batteries) {

                        if (!battery.IsWorking) continue;
                        var currentInput = battery.CurrentInput;
                        var currentOutput = battery.CurrentOutput;
                        var maxOutput = battery.MaxOutput;

                        if (currentInput > 0) {
                            BatteryCurrentInput += currentInput;
                            if (battery.IsCharging) BatteryCurrentOutput -= currentInput;
                            else BatteryCurrentOutput -= currentInput;
                        }
                        BatteryMaxPower += maxOutput;
                        BatteryCurrentOutput += currentOutput;
                    }
                }

                GridAvailablePower = GridMaxPower - GridCurrentPower;

                GridCurrentPower += BatteryCurrentInput;
                GridAvailablePower -= BatteryCurrentInput;
                UpdatePowerSources = false;

                RequestedPowerChanged = Math.Abs(LastRequestedPower - RequestedWeaponsDraw) > 0.001 && LastRequestedPower > 0;
                AvailablePowerChanged = Math.Abs(GridMaxPower - LastAvailablePower) > 0.001 && LastAvailablePower > 0;

                RequestIncrease = LastRequestedPower < RequestedWeaponsDraw;
                PowerIncrease = LastAvailablePower < GridMaxPower;

                LastAvailablePower = GridMaxPower;
                LastRequestedPower = RequestedWeaponsDraw;

                HadPower = HasPower;
                HasPower = GridMaxPower > 0;

                LastPowerUpdateTick = Session.Tick;

                if (HasPower) return;
                if (HadPower)
                    WeaponShootOff();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateGridPower: {ex} - SessionNull{Session == null} - FakeShipControllerNull{FakeShipController == null} - PowerDistributorNull{PowerDistributor == null} - MyGridNull{TopEntity == null}"); }
        }

        private void ForceCloseAiInventories()
        {
            foreach (var pair in InventoryMonitor)
                InventoryRemove(pair.Key, pair.Value);
            
            if (InventoryMonitor.Count > 0) {
                Log.Line($"Found stale inventories during AI close - failedToRemove:{InventoryMonitor.Count}");
                InventoryMonitor.Clear();
            }

        }
        
        internal void AiDelayedClose()
        {
            if (Session == null || TopEntity == null || Closed) {
                Log.Line($"AiDelayedClose: Session is null {Session == null} - Grid is null {TopEntity == null}  - Closed: {Closed}");
                return;
            }

            if (!ScanInProgress && Session.Tick - ProjectileTicker > 59 && AiMarkedTick != uint.MaxValue && Session.Tick - AiMarkedTick > 119) {

                //lock (DbLock)
                using (DbLock.AcquireExclusiveUsing())
                {
                    if (ScanInProgress)
                        return;
                    Session.GridAiPool.Return(this);
                }
            }
        }

        internal void AiForceClose()
        {
            if (Session == null || TopEntity == null || Closed) {
                Log.Line($"AiDelayedClose: Session is null {Session == null} - Grid is null {TopEntity == null} - Closed: {Closed}");
                return;
            }

            RegisterMyGridEvents(false, true);
            Session.GridAiPool.Return(this);
        }

        internal void CleanSortedTargets()
        {
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var tInfo = SortedTargets[i];
                tInfo.Target = null;
                tInfo.MyAi = null;
                tInfo.TargetAi = null;
                Session.TargetInfoPool.Return(tInfo);
            }
            SortedTargets.Clear();
        }

        internal void CleanUp()
        {
            AiCloseTick = Session.Tick;

            TopEntity.Components.Remove<AiComponent>();

            if (Session.IsClient)
                Session.SendUpdateRequest(TopEntity.EntityId, PacketType.ClientAiRemove);

            Data.Repo.ControllingPlayers.Clear();
            Data.Repo.ActiveTerminal = 0;

            CleanSortedTargets();
            Construct.Clean();
            Obstructions.Clear();
            ObstructionsTmp.Clear();
            TargetAis.Clear();
            TargetAisTmp.Clear();
            EntitiesInRange.Clear();
            Batteries.Clear();
            Targets.Clear();

            WeaponComps.Clear();
            UpgradeComps.Clear();
            SupportComps.Clear();
            PhantomComps.Clear();
            WeaponIdx.Clear();
            SupportIdx.Clear();
            UpgradeIdx.Clear();
            PhantomIdx.Clear();
            CompBase.Clear();

            LiveProjectile.Clear();
            DeadProjectiles.Clear();
            NearByShieldsTmp.Clear();
            NearByFriendlyShields.Clear();
            StaticsInRange.Clear();
            StaticsInRangeTmp.Clear();
            TestShields.Clear();
            NewEntities.Clear();
            SubGridsRegistered.Clear();
            PreviousTargets.Clear();
            SourceCount = 0;
            PartCount = 0;
            AiOwner = 0;
            ProjectileTicker = 0;
            NearByEntities = 0;
            NearByEntitiesTmp = 0;
            MyProjectiles = 0;
            AccelChecked = false;
            PointDefense = false;
            FadeOut = false;
            SuppressMouseShoot = false;
            OverPowered = false;
            UpdatePowerSources = false;
            AvailablePowerChanged = false;
            PowerIncrease = false;
            RequestedPowerChanged = false;
            RequestIncrease = false;
            DbReady = false;
            GridInit = false;
            TouchingWater = false;
            BlockMonitoring = false;

            Data.Clean();

            MyShield = null;
            MyPlanetTmp = null;
            MyPlanet = null;
            TerminalSystem = null;
            LastTerminal = null;
            PowerDistributor = null;
            PowerBlock = null;
            TopEntity = null;
            PowerDistributor = null;
            Session = null;
            Closed = true;
            CanShoot = true;
            Version++;
        }
    }
}
