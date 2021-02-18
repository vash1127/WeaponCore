using System;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using static CoreSystems.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace CoreSystems.Platform
{
    public partial class Weapon 
    {
        internal void ChangeActiveAmmoServer()
        {
            var proposed = ProposedAmmoId != -1;
            var ammoType = proposed ? System.AmmoTypes[ProposedAmmoId] : System.AmmoTypes[ProtoWeaponAmmo.AmmoTypeId];
            ScheduleAmmoChange = false;

            if (ActiveAmmoDef == ammoType)
                return;
            
            ++ProtoWeaponAmmo.AmmoCycleId;

            if (proposed)
            {
                ProtoWeaponAmmo.AmmoTypeId = ProposedAmmoId;
                ProposedAmmoId = -1;
                ProtoWeaponAmmo.CurrentAmmo = 0;
                ProtoWeaponAmmo.CurrentMags = 0;
            }

            ActiveAmmoDef = System.AmmoTypes[ProtoWeaponAmmo.AmmoTypeId];
            PrepAmmoShuffle();

            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                ProtoWeaponAmmo.CurrentMags = Comp.CoreInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();
            
            CheckInventorySystem = true;

            UpdateRof();
            SetWeaponDps();
            UpdateWeaponRange();

            if (System.Session.MpActive)
                System.Session.SendWeaponAmmoData(this);
        }

        internal void ChangeActiveAmmoClient()
        {
            var ammoType = System.AmmoTypes[ProtoWeaponAmmo.AmmoTypeId];

            if (ActiveAmmoDef == ammoType)
                return;

            ActiveAmmoDef = System.AmmoTypes[ProtoWeaponAmmo.AmmoTypeId];
            PrepAmmoShuffle();

            UpdateRof();
            SetWeaponDps();
            UpdateWeaponRange();
        }

        internal void PrepAmmoShuffle()
        {
            if (AmmoShufflePattern.Length != ActiveAmmoDef.AmmoDef.Const.PatternIndexCnt) 
                Array.Resize(ref AmmoShufflePattern, ActiveAmmoDef.AmmoDef.Const.PatternIndexCnt);

            for (int i = 0; i < AmmoShufflePattern.Length; i++)
                AmmoShufflePattern[i] = i;
        }

        internal void AmmoChange(object o)
        {
            try
            {
                var ammoChange = (AmmoLoad)o;
                if (ammoChange.Change == AmmoLoad.ChangeType.Add)
                {
                    var oldType = System.AmmoTypes[ammoChange.OldId];
                    if (Comp.CoreInventory.CanItemsBeAdded(ammoChange.Amount, oldType.AmmoDefinitionId))
                        Comp.CoreInventory.AddItems(ammoChange.Amount, ammoChange.Item.Content);
                    else
                    {
                        if (!Comp.Session.MpActive)
                            MyAPIGateway.Utilities.ShowNotification($"Weapon inventory full, ejecting {ammoChange.Item.Content.SubtypeName} magazine", 3000, "Red");
                        else if (Comp.Data.Repo.Values.State.PlayerId > 0)
                        {
                            var message = $"Weapon inventory full, ejecting {ammoChange.Item.Content.SubtypeName} magazine";
                            Comp.Session.SendClientNotify(Comp.Data.Repo.Values.State.PlayerId, message, true, "Red", 3000);
                        }
                        MyFloatingObjects.Spawn(ammoChange.Item, Dummies[0].Info.Position, MyPivotFwd, MyPivotUp);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AmmoChange: {ex} - {((AmmoLoad)o).Amount} - {((AmmoLoad)o).Item.Content.SubtypeName}", null, true); }
        }

        internal void ChangeAmmo(int newAmmoId)
        {

            if (System.Session.IsServer)
            {
                ProposedAmmoId = newAmmoId;
                var instantChange = System.Session.IsCreative || !ActiveAmmoDef.AmmoDef.Const.Reloadable;
                var canReload = ProtoWeaponAmmo.CurrentAmmo == 0 && ActiveAmmoDef.AmmoDef.Const.Reloadable;
                var proposedAmmo = System.AmmoTypes[ProposedAmmoId];

                var unloadMag = !canReload && !instantChange && !Loading && !ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && ProtoWeaponAmmo.CurrentAmmo == ActiveAmmoDef.AmmoDef.Const.MagazineSize;

                if (unloadMag && proposedAmmo.AmmoDef.Const.Reloadable)
                {
                    ProtoWeaponAmmo.CurrentAmmo = 0;
                    canReload = true;
                    System.Session.FutureEvents.Schedule(AmmoChange, new AmmoLoad { Amount = 1, Change = AmmoLoad.ChangeType.Add, OldId = ProtoWeaponAmmo.AmmoTypeId, Item = ActiveAmmoDef.AmmoDef.Const.AmmoItem }, 1);
                }

                if (instantChange)
                    ChangeActiveAmmoServer();
                else 
                    ScheduleAmmoChange = true;

                if (proposedAmmo.AmmoDef.Const.Reloadable && canReload)
                    ComputeServerStorage();
            }
            else 
                System.Session.SendAmmoCycleRequest(this, newAmmoId);
        }

        internal bool HasAmmo()
        {
            if (Comp.Session.IsCreative || !ActiveAmmoDef.AmmoDef.Const.Reloadable || System.DesignatorWeapon) {
                NoMagsToLoad = false;
                return true;
            }

            ProtoWeaponAmmo.CurrentMags = Comp.CoreInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();
            var energyDrainable = ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && Comp.Ai.HasPower;
            var nothingToLoad = ProtoWeaponAmmo.CurrentMags <= 0 && !energyDrainable;

            if (NoMagsToLoad) {
                if (nothingToLoad)
                    return false;

                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, false);
                Comp.Ai.Construct.RootAi.Construct.OutOfAmmoWeapons.Remove(this);
                NoMagsToLoad = false;
                LastMagSeenTick = System.Session.Tick;

                if (System.Session.MpActive)
                    System.Session.SendWeaponAmmoData(this);
            }
            else if (nothingToLoad)
            {
                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, true);
                Comp.Ai.Construct.RootAi.Construct.OutOfAmmoWeapons.Add(this);

                if (!NoMagsToLoad) {
                    CheckInventorySystem = true;
                    if (System.Session.MpActive)
                        System.Session.SendWeaponAmmoData(this);
                }
                NoMagsToLoad = true;
            }

            return !NoMagsToLoad;
        }

        internal bool ClientReload(bool networkCaller = false)
        {
            var syncUp = Reload.StartId > ClientStartId;

            if (!syncUp) {
                var energyDrainable = ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && Comp.Ai.HasPower;
                if (ProtoWeaponAmmo.CurrentMags <= 0 && !energyDrainable && ActiveAmmoDef.AmmoDef.Const.Reloadable && !System.DesignatorWeapon) {
                    if (!NoMagsToLoad) 
                        EventTriggerStateChanged(EventTriggers.NoMagsToLoad, true);
                    NoMagsToLoad = true;
                }
                return false;
            }
            ClientStartId = Reload.StartId;
            ClientMakeUpShots += ProtoWeaponAmmo.CurrentAmmo;
            ProtoWeaponAmmo.CurrentAmmo = 0;

            if (NoMagsToLoad) {
                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, false);
                NoMagsToLoad = false;
            }

            ClientReloading = true;
            Loading = true;
            FinishBurst = false;

            if (!ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay) ShotsFired = 0;

            StartReload();
            return true;
        }

        internal bool ComputeServerStorage()
        {
            var s = Comp.Session;

            if (System.DesignatorWeapon || !Comp.IsWorking || !ActiveAmmoDef.AmmoDef.Const.Reloadable || !Comp.CoreEntity.HasInventory ) return false;

            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
            {
                if (!s.IsCreative)
                {
                    Comp.CurrentInventoryVolume = (float)Comp.CoreInventory.CurrentVolume;
                    var freeVolume = System.MaxAmmoVolume - Comp.CurrentInventoryVolume;
                    var spotsFree = (int)(freeVolume / ActiveAmmoDef.AmmoDef.Const.MagVolume);
                    ProtoWeaponAmmo.CurrentMags = Comp.CoreInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();
                    CurrentAmmoVolume = ProtoWeaponAmmo.CurrentMags * ActiveAmmoDef.AmmoDef.Const.MagVolume;

                    var magsRequested = (int)((System.FullAmmoVolume - CurrentAmmoVolume) / ActiveAmmoDef.AmmoDef.Const.MagVolume);
                    var magsGranted = magsRequested > spotsFree ? spotsFree : magsRequested;
                    var requestedVolume = ActiveAmmoDef.AmmoDef.Const.MagVolume * magsGranted;
                    var spaceAvailable = freeVolume > requestedVolume;
                    var lowThreshold = System.MaxAmmoVolume * 0.25f;

                    var pullAmmo = magsGranted > 0 && CurrentAmmoVolume < lowThreshold && spaceAvailable;
                    
                    var failSafeTimer = s.Tick - LastInventoryTick > 600;
                    
                    if (pullAmmo && (CheckInventorySystem || failSafeTimer && Comp.Ai.Construct.RootAi.Construct.OutOfAmmoWeapons.Contains(this)) && !s.PartToPullConsumable.Contains(this)) {

                        CheckInventorySystem = false;
                        LastInventoryTick = s.Tick;
                        s.PartToPullConsumable.Add(this);
                        s.GridsToUpdateInventories.Add(Comp.Ai);
                    }
                    else if (CheckInventorySystem && failSafeTimer && !s.PartToPullConsumable.Contains(this))
                        CheckInventorySystem = false;
                }
            }
            var invalidStates = ProtoWeaponAmmo.CurrentAmmo != 0 || Loading;
            return !invalidStates && ServerReload();
        }

        internal bool ServerReload()
        {
            if (AnimationDelayTick > Comp.Session.Tick && (LastEventCanDelay || LastEvent == EventTriggers.Firing))
                return false;
            if (ScheduleAmmoChange) 
                ChangeActiveAmmoServer();

            var hasAmmo = HasAmmo();

            FinishBurst = false;
            ShootOnce = false;

            if (!hasAmmo) 
                return false;

            ++Reload.StartId;
            ++ClientStartId;

            if (!ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay) ShotsFired = 0;

            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo) {
                
                if (Comp.CoreInventory.ItemsCanBeRemoved(1, ActiveAmmoDef.AmmoDef.Const.AmmoItem))
                    Comp.CoreInventory.RemoveItems(ActiveAmmoDef.AmmoDef.Const.AmmoItem.ItemId, 1);
                else if (Comp.CoreInventory.ItemCount > 0 && Comp.CoreInventory.ContainItems(1, ActiveAmmoDef.AmmoDef.Const.AmmoItem.Content))
                {
                    Comp.CoreInventory.Remove(ActiveAmmoDef.AmmoDef.Const.AmmoItem, 1);
                }

                ProtoWeaponAmmo.CurrentMags = Comp.CoreInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();
                if (System.Session.IsServer && ProtoWeaponAmmo.CurrentMags == 0)
                    CheckInventorySystem = true;
            }

            StartReload();
            return true;
        }

        internal void StartReload()
        {
            Loading = true;
            EventTriggerStateChanged(EventTriggers.Reloading, true);

            if (ActiveAmmoDef.AmmoDef.Const.MustCharge)
                ChargeReload();
            
            if (!ActiveAmmoDef.AmmoDef.Const.MustCharge || ActiveAmmoDef.AmmoDef.Const.IsHybrid) {
                if (System.ReloadTime > 0) {
                    CancelableReloadAction += Reloaded;
                    ReloadSubscribed = true;
                    Comp.Session.FutureEvents.Schedule(CancelableReloadAction, true, (uint)System.ReloadTime);
                }
                else Reloaded();
            }

            if (System.Session.MpActive && System.Session.IsServer)
                System.Session.SendWeaponReload(this);

            if (ReloadEmitter == null || ReloadSound == null || ReloadEmitter.IsPlaying) return;
            ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);
        }

        internal void Reloaded(object o = null)
        {
            var callBack = o as bool? ?? false;

            using (Comp.CoreEntity.Pin()) {

                LastLoadedTick = Comp.Session.Tick;

                var invalidStates = PartState == null || Comp.Data.Repo == null || Comp.Ai == null || Comp.CoreEntity.MarkedForClose;

                if (!invalidStates) {

                    if (ActiveAmmoDef.AmmoDef.Const.MustCharge && !callBack) {

                        Log.Line($"charged");
                        ProtoWeaponAmmo.CurrentCharge = MaxCharge;
                        EstimatedCharge = MaxCharge;
                        
                        if (ActiveAmmoDef.AmmoDef.Const.IsHybrid && ReloadSubscribed)
                            return;
                    }
                    else if (ReloadSubscribed) {
                        CancelableReloadAction -= Reloaded;
                        ReloadSubscribed = false;
                        Log.Line($"AmmoLoaded");

                        if (ActiveAmmoDef.AmmoDef.Const.IsHybrid && Charging)
                            return;
                    }

                    EventTriggerStateChanged(EventTriggers.Reloading, false);

                    //ProtoWeaponAmmo.CurrentAmmo = !ActiveAmmoDef.AmmoDef.Const.EnergyAmmo ? ActiveAmmoDef.AmmoDef.Const.MagazineDef.Capacity : ActiveAmmoDef.AmmoDef.Const.EnergyMagSize;
                    ProtoWeaponAmmo.CurrentAmmo = ActiveAmmoDef.AmmoDef.Const.MagazineSize;
                    if (System.Session.IsServer) {

                        ++Reload.EndId;
                        ShootOnce = false;
                        if (System.Session.MpActive)
                            System.Session.SendWeaponReload(this);
                    }
                    else {
                        ClientReloading = false;
                        ClientMakeUpShots = 0;
                    }
                }
                ++ClientEndId;
                Loading = false;
            }
        }

        public void ChargeReload()
        {
            ProtoWeaponAmmo.CurrentCharge = 0;
            ProtoWeaponAmmo.CurrentAmmo = 0;
            EstimatedCharge = 0;

            Comp.Ai.Charger.Add(this);
        }
    }
}
