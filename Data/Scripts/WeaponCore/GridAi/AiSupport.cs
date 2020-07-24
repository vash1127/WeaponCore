using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.Session;
using static WeaponCore.WeaponRandomGenerator;
using static WeaponCore.Support.WeaponComponent;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void CompChange(bool add, WeaponComponent comp)
        {
            if (add) {

                if (WeaponsIdx.ContainsKey(comp)) {
                    Log.Line($"CompAddFailed:<{comp.MyCube.EntityId}> - comp({comp.MyCube.DebugName}[{comp.MyCube.BlockDefinition.Id.SubtypeName}]) already existed in {MyGrid.DebugName}");
                    return;
                }

                if (comp.HasArmor) {
                    for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                        var w = comp.Platform.Weapons[i];
                        if (w.System.Armor != WeaponDefinition.HardPointDef.HardwareDef.ArmorState.IsWeapon)
                         Armor.Add(w.Comp.MyCube, w);
                    }
                    Session.ArmorCubes.Add(comp.MyCube, comp);
                }
                WeaponsIdx.Add(comp, Weapons.Count);
                //Session.IdToCompMap.Add(comp.MyCube.EntityId, comp);
                Weapons.Add(comp);
            }
            else {

                int idx;
                if (!WeaponsIdx.TryGetValue(comp, out idx)) {
                    Log.Line($"CompRemoveFailed: <{comp.MyCube.EntityId}> - {Weapons.Count}[{WeaponsIdx.Count}]({WeaponBase.Count}) - {Weapons.Contains(comp)}[{Weapons.Count}] - {Session.GridTargetingAIs[comp.MyCube.CubeGrid].WeaponBase.ContainsKey(comp.MyCube)} - {Session.GridTargetingAIs[comp.MyCube.CubeGrid].WeaponBase.Count} ");
                    return;
                }

                if (comp.HasArmor) {
                    for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                        var w = comp.Platform.Weapons[i];
                        if (w.System.Armor != WeaponDefinition.HardPointDef.HardwareDef.ArmorState.IsWeapon)
                            Armor.Remove(w.Comp.MyCube);
                    }
                    Session.ArmorCubes.Remove(comp.MyCube);
                }

                Weapons.RemoveAtFast(idx);
                if (idx < Weapons.Count)
                    WeaponsIdx[Weapons[idx]] = idx;

                //Session.IdToCompMap.Remove(comp.MyCube.EntityId);
                WeaponsIdx.Remove(comp);
            }
        }

        private readonly List<string> _tmpGroupKeys = new List<string>();
        internal void ReScanBlockGroups()
        {
            if (Session.Tick - LastGroupScanTick < 60)
                return;
            
            if (TerminalSystem == null)
                TerminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(MyGrid);
            
            if (TerminalSystem != null) {

                TerminalSystem.GetBlockGroups(null, group =>
                {
                    GroupInfo groupInfo = null;
                    if (Construct.Data.Repo.BlockGroups.TryGetValue(group.Name, out groupInfo)) {
                        groupInfo.ChangeState = GroupInfo.ChangeStates.None;
                        groupInfo.Name = group.Name;
                        groupInfo.CompIds.Clear();
                    }

                    group.GetBlocks(null, block =>
                    {
                        var cube = (MyCubeBlock) block;
                        WeaponComponent comp;
                        if (cube.Components.TryGet(out comp) && SubGrids.Contains(cube.CubeGrid)) {

                            if (groupInfo == null) {
                                groupInfo = Session.GroupInfoPool.Get();
                                groupInfo.Name = group.Name;
                                groupInfo.ChangeState = GroupInfo.ChangeStates.Add;
                                Construct.Data.Repo.BlockGroups.Add(group.Name, groupInfo);
                            }
                            groupInfo.CompIds.Add(comp.MyCube.EntityId);
                            if (groupInfo.ChangeState == GroupInfo.ChangeStates.None)
                                groupInfo.ChangeState = GroupInfo.ChangeStates.Modify;
                        }
                        return false;
                    });

                    return false;
                });

                foreach (var group in Construct.Data.Repo.BlockGroups) {
                    if (group.Value.ChangeState == GroupInfo.ChangeStates.None)
                    {
                        Session.GroupInfoPool.Return(group.Value);
                        _tmpGroupKeys.Add(group.Key);
                    }
                    else group.Value.ChangeState = GroupInfo.ChangeStates.None;
                }

                for (int i = 0; i < _tmpGroupKeys.Count; i++)
                    Construct.Data.Repo.BlockGroups.Remove(_tmpGroupKeys[i]);
                _tmpGroupKeys.Clear();

                ScanBlockGroups = false;

                LastGroupScanTick = Session.Tick;
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
            if (type == RandomType.Acquire)
                rnd = rng.AcquireRandom;
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
            NearByEntitySphere = new BoundingSphereD(MyGrid.PositionComp.WorldAABB.Center, checkVol);
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
            for (int i = 0; i < Weapons.Count; i++) {

                var comp = Weapons[i];
                for (int x = 0; x < comp.Platform.Weapons.Length; x++) {
                    var w = comp.Platform.Weapons[x];
                    w.StopReloadSound();
                    w.StopShooting();
                }
            }
        }

        /*
        internal void TurnManualShootOff()
        {
            if (TurnOffManualTick == Session.Tick) return;

            TurnOffManualTick = Session.Tick;

            foreach (var cubeComp in Weapons) {

                var comp = cubeComp;
                if (comp?.Platform.State != MyWeaponPlatform.PlatformState.Ready) continue;
                var currPlayer = comp.State.Value.CurrentPlayerControl;

                if (currPlayer.PlayerId != Session.PlayerId) continue;

                var cState = comp.State.Value;
                var overRides = comp.Set.Value.Overrides;

                currPlayer.ControlType = ControlType.None;
                currPlayer.PlayerId = -1;

                if (comp.Session.MpActive)
                    comp.Session.SendControlingPlayer(comp);

                if (cState.ClickShoot) {

                    for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                        var wState = cState.Weapons[comp.Platform.Weapons[i].WeaponId];

                        if (cState.ClickShoot)
                            wState.ManualShoot = ShootActions.ShootOff;
                    }

                    cState.ClickShoot = false;

                    //if (comp.Session.MpActive)
                        //comp.Session.SendActionShootUpdate(comp, ShootActions.ShootOff);
                }
                else if (overRides.TargetPainter || overRides.ManualControl) {

                    overRides.TargetPainter = false;
                    overRides.ManualControl = false;
                    if (comp.Session.MpActive) {
                        //comp.Session.SendOverRidesUpdate(comp, overRides);
                        comp.TrackReticle = false;
                        comp.Session.SendTrackReticleUpdate(comp);
                    }

                    GroupInfo group;
                    if (!string.IsNullOrEmpty(comp.State.Value.CurrentBlockGroup) && BlockGroups.TryGetValue(comp.State.Value.CurrentBlockGroup, out group)) {
                        GroupsToCheck.Add(group);
                        ScanBlockGroupSettings = true;
                    }
                }
            }
        }
        */

        internal void CheckReload(MyDefinitionId magId)
        {
            foreach (var w in OutOfAmmoWeapons) {
                if (w.ActiveAmmoDef.AmmoDefinitionId == magId)
                    Session.CheckStorage.Add(w);
            }
        }

        internal void UpdateGridPower()
        {
            try
            {
                if (Weapons.Count == 0) {
                    Log.Line($"no valid weapon in powerDist");
                    return;
                }

                GridCurrentPower = 0;
                GridMaxPower = 0;
                for (int i = -1, j = 0; i < Weapons.Count; i++, j++) {

                    var powerBlock = j == 0 ? PowerBlock : Weapons[i].MyCube;
                    if (powerBlock == null || j == 0 && PowerDirty) continue;

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
            catch (Exception ex) { Log.Line($"Exception in UpdateGridPower: {ex} - SessionNull{Session == null} - FakeShipControllerNull{FakeShipController == null} - PowerDistributorNull{PowerDistributor == null} - MyGridNull{MyGrid == null}"); }
        }

        internal void GridDelayedClose()
        {
            if (Session == null || MyGrid == null || Closed) {
                Log.Line($"GridDelayedClose: Session is null {Session == null} - Grid is null {MyGrid == null}  - Closed: {Closed}");
                return;
            }

            if (!ScanInProgress && Session.DbTask.IsComplete && Session.Tick - ProjectileTicker > 59 && Session.Tick - AiMarkedTick > 59) {

                lock (AiLock) {
                
                    if (ScanInProgress)
                        return;
                    Session.GridAiPool.Return(this);
                }
            }
        }

        internal void GridForceClose()
        {
            if (Session == null || MyGrid == null || Closed) {
                Log.Line($"GridDelayedClose: Session is null {Session == null} - Grid is null {MyGrid == null} - Closed: {Closed}");
                return;
            }

            Session.GridAiPool.Return(this);
        }

        internal void CleanSortedTargets()
        {
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var tInfo = SortedTargets[i];
                tInfo.Target = null;
                tInfo.MyAi = null;
                tInfo.MyGrid = null;
                tInfo.TargetAi = null;
                Session.TargetInfoPool.Return(tInfo);
            }
            SortedTargets.Clear();
        }

        internal void CleanUp()
        {
            AiCloseTick = Session.Tick;

            if (Session.IsClient)
                Session.SendUpdateRequest(MyGrid.EntityId, PacketType.ClientAiRemove);

            RegisterMyGridEvents(false);

            foreach (var grid in SubGrids) {
                if (grid == MyGrid) continue;
                RemSubGrids.Add(grid);
            }

            AddSubGrids.Clear();
            SubGridChanges(true);

            SubGrids.Clear();

            Data.Repo.ControllingPlayers.Clear();
            Data.Repo.ActiveTerminal = 0;

            CleanSortedTargets();
            InventoryIndexer.Clear();
            Construct.Clean();

            Obstructions.Clear();
            ObstructionsTmp.Clear();
            TargetAis.Clear();
            TargetAisTmp.Clear();
            EntitiesInRange.Clear();
            Batteries.Clear();
            Targets.Clear();
            Weapons.Clear();
            WeaponsIdx.Clear();
            WeaponBase.Clear();
            Inventories.Clear();
            LiveProjectile.Clear();
            DeadProjectiles.Clear();
            NearByShieldsTmp.Clear();
            NearByFriendlyShields.Clear();
            StaticsInRange.Clear();
            StaticsInRangeTmp.Clear();
            TestShields.Clear();
            NewEntities.Clear();
            SourceCount = 0;
            BlockCount = 0;
            MyOwner = 0;
            ProjectileTicker = 0;
            NearByEntities = 0;
            NearByEntitiesTmp = 0;
            MyProjectiles = 0;
            AccelChecked = false;
            PointDefense = false;
            FadeOut = false;
            SupressMouseShoot = false;
            OverPowered = false;
            UpdatePowerSources = false;
            AvailablePowerChanged = false;
            PowerIncrease = false;
            RequestedPowerChanged = false;
            RequestIncrease = false;
            DbReady = false;
            GridInit = false;
            Data.Clean();

            MyShield = null;
            MyPlanetTmp = null;
            MyPlanet = null;
            TerminalSystem = null;
            LastTerminal = null;
            PowerDistributor = null;
            PowerBlock = null;
            MyGrid = null;
            PowerDistributor = null;
            Session = null;
            Closed = true;
            Version++;
        }
    }
}
