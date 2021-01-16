using System;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Helpers;
using Sandbox.ModAPI;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal void ChangeActiveAmmoServer()
        {
            var proposed = ProposedAmmoId != -1;
            var ammoType = proposed ? System.AmmoTypes[ProposedAmmoId] : System.AmmoTypes[Ammo.AmmoTypeId];
            ScheduleAmmoChange = false;

            if (ActiveAmmoDef == ammoType)
                return;
            
            ++Ammo.AmmoCycleId;

            if (proposed)
            {
                Ammo.AmmoTypeId = ProposedAmmoId;
                ProposedAmmoId = -1;
                Ammo.CurrentAmmo = 0;
                Ammo.CurrentMags = 0;
            }

            ActiveAmmoDef = System.AmmoTypes[Ammo.AmmoTypeId];
            PrepAmmoShuffle();

            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                Ammo.CurrentMags = Comp.BlockInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();
            
            CheckInventorySystem = true;

            UpdateRof();
            SetWeaponDps();
            UpdateWeaponRange();

            if (System.Session.MpActive)
                System.Session.SendWeaponAmmoData(this);
        }

        internal void ChangeActiveAmmoClient()
        {
            var ammoType = System.AmmoTypes[Ammo.AmmoTypeId];

            if (ActiveAmmoDef == ammoType)
                return;

            ActiveAmmoDef = System.AmmoTypes[Ammo.AmmoTypeId];
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
                    if (Comp.BlockInventory.CanItemsBeAdded(ammoChange.Amount, oldType.AmmoDefinitionId))
                        Comp.BlockInventory.AddItems(ammoChange.Amount, ammoChange.Item.Content);
                    else
                    {
                        if (!Comp.Session.MpActive)
                            MyAPIGateway.Utilities.ShowNotification($"Weapon inventory full, ejecting {ammoChange.Item.Content.SubtypeName} magazine", 3000, "Red");
                        else if (Comp.Data.Repo.Base.State.PlayerId > 0)
                        {
                            var message = $"Weapon inventory full, ejecting {ammoChange.Item.Content.SubtypeName} magazine";
                            Comp.Session.SendClientNotify(Comp.Data.Repo.Base.State.PlayerId, message, true, "Red", 3000);
                        }
                        MyFloatingObjects.Spawn(ammoChange.Item, Dummies[0].Info.Position, MyPivotFwd, MyPivotUp);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AmmoChange: {ex} - {((AmmoLoad)o).Amount} - {((AmmoLoad)o).Item.Content.SubtypeName}"); }
        }

        internal void ChangeAmmo(int newAmmoId)
        {

            if (System.Session.IsServer)
            {
                ProposedAmmoId = newAmmoId;
                var instantChange = System.Session.IsCreative || !ActiveAmmoDef.AmmoDef.Const.Reloadable;
                var canReload = Ammo.CurrentAmmo == 0 && ActiveAmmoDef.AmmoDef.Const.Reloadable;
                var proposedAmmo = System.AmmoTypes[ProposedAmmoId];

                var unloadMag = !canReload && !instantChange && !Reloading && !ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && Ammo.CurrentAmmo == ActiveAmmoDef.AmmoDef.Const.MagazineSize;

                if (unloadMag && proposedAmmo.AmmoDef.Const.Reloadable)
                {
                    Ammo.CurrentAmmo = 0;
                    canReload = true;
                    System.Session.FutureEvents.Schedule(AmmoChange, new AmmoLoad { Amount = 1, Change = AmmoLoad.ChangeType.Add, OldId = Ammo.AmmoTypeId, Item = ActiveAmmoDef.AmmoDef.Const.AmmoItem }, 1);
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

            Ammo.CurrentMags = Comp.BlockInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();
            var energyDrainable = ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && Comp.Ai.HasPower;
            var nothingToLoad = Ammo.CurrentMags <= 0 && !energyDrainable;

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
                if (Ammo.CurrentMags <= 0 && !energyDrainable && ActiveAmmoDef.AmmoDef.Const.Reloadable && !System.DesignatorWeapon) {
                    if (!NoMagsToLoad) 
                        EventTriggerStateChanged(EventTriggers.NoMagsToLoad, true);
                    NoMagsToLoad = true;
                }
                return false;
            }
            ClientStartId = Reload.StartId;
            ClientMakeUpShots += Ammo.CurrentAmmo;
            Ammo.CurrentAmmo = 0;

            if (NoMagsToLoad) {
                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, false);
                NoMagsToLoad = false;
            }

            ClientReloading = true;
            Reloading = true;
            FinishBurst = false;

            if (!ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay) ShotsFired = 0;

            StartReload();
            return true;
        }

        internal bool ComputeServerStorage()
        {
            var s = Comp.Session;

            if (System.DesignatorWeapon || !Comp.IsWorking || !ActiveAmmoDef.AmmoDef.Const.Reloadable || !Comp.MyCube.HasInventory ) return false;

            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
            {
                if (!s.IsCreative)
                {
                    Comp.CurrentInventoryVolume = (float)Comp.BlockInventory.CurrentVolume;
                    var freeSpace = System.MaxAmmoVolume - Comp.CurrentInventoryVolume;
                    var spotsFree = (int)(freeSpace / ActiveAmmoDef.AmmoDef.Const.MagVolume);
                    Ammo.CurrentMags = Comp.BlockInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();
                    CurrentAmmoVolume = Ammo.CurrentMags * ActiveAmmoDef.AmmoDef.Const.MagVolume;

                    var magsNeeded = (int)((System.FullAmmoVolume - CurrentAmmoVolume) / ActiveAmmoDef.AmmoDef.Const.MagVolume);
                    magsNeeded = magsNeeded > spotsFree ? spotsFree : magsNeeded;

                    var needsAmmo = magsNeeded > 0 && CurrentAmmoVolume < 0.25f * System.MaxAmmoVolume && freeSpace > ActiveAmmoDef.AmmoDef.Const.MagVolume * magsNeeded;
                    var failSafeTimer = s.Tick - LastInventoryTick > 600;
                    
                    if (needsAmmo && (CheckInventorySystem || failSafeTimer && Comp.Ai.Construct.RootAi.Construct.OutOfAmmoWeapons.Contains(this)) && !s.WeaponToPullAmmo.Contains(this)) {

                        CheckInventorySystem = false;
                        LastInventoryTick = s.Tick;
                        s.WeaponToPullAmmo.Add(this);
                        s.GridsToUpdateInventories.Add(Comp.Ai);
                    }
                    else if (CheckInventorySystem && failSafeTimer && !s.WeaponToPullAmmo.Contains(this))
                        CheckInventorySystem = false;
                }
            }

            var invalidStates = Ammo.CurrentAmmo != 0 || Reloading;
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
                
                if (Comp.BlockInventory.ItemsCanBeRemoved(1, ActiveAmmoDef.AmmoDef.Const.AmmoItem))
                    Comp.BlockInventory.RemoveItems(ActiveAmmoDef.AmmoDef.Const.AmmoItem.ItemId, 1);
                else if (Comp.BlockInventory.ItemCount > 0 && Comp.BlockInventory.ContainItems(1, ActiveAmmoDef.AmmoDef.Const.AmmoItem.Content))
                {
                    Comp.BlockInventory.Remove(ActiveAmmoDef.AmmoDef.Const.AmmoItem, 1);
                }

                Ammo.CurrentMags = Comp.BlockInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();
                if (System.Session.IsServer && Ammo.CurrentMags == 0)
                    CheckInventorySystem = true;
            }

            StartReload();
            return true;
        }

        internal void StartReload()
        {
            Reloading = true;
            EventTriggerStateChanged(EventTriggers.Reloading, true);

            if (ActiveAmmoDef.AmmoDef.Const.MustCharge && !Comp.Session.ChargingWeaponsIndexer.ContainsKey(this))
                ChargeReload();
            else if (!ActiveAmmoDef.AmmoDef.Const.MustCharge) {
                if (System.ReloadTime > 0) {
                    CancelableReloadAction += Reloaded;
                    ReloadSubscribed = true;
                    Comp.Session.FutureEvents.Schedule(CancelableReloadAction, null, (uint)System.ReloadTime);
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
            using (Comp.MyCube.Pin())
            {

                if (State == null || Comp.Data.Repo == null || Comp.Ai == null || Comp.MyCube.MarkedForClose) return;


                LastLoadedTick = Comp.Session.Tick;

                if (ActiveAmmoDef.AmmoDef.Const.MustCharge) {

                    Comp.CurrentCharge -= Ammo.CurrentCharge;
                    Ammo.CurrentCharge = MaxCharge;
                    Comp.CurrentCharge += MaxCharge;

                    ChargeUntilTick = 0;
                    ChargeDelayTicks = 0;
                }
                else if (ReloadSubscribed) {
                    CancelableReloadAction -= Reloaded;
                    ReloadSubscribed = false;
                }

                EventTriggerStateChanged(EventTriggers.Reloading, false);

                Ammo.CurrentAmmo = !ActiveAmmoDef.AmmoDef.Const.EnergyAmmo ? ActiveAmmoDef.AmmoDef.Const.MagazineDef.Capacity : ActiveAmmoDef.AmmoDef.Const.EnergyMagSize;

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

                ++ClientEndId;
                Reloading = false;
            }

        }
        public void ChargeReload(bool syncCharge = false)
        {
            Comp.CurrentCharge -= Ammo.CurrentCharge;
            Ammo.CurrentCharge = 0;
            Ammo.CurrentAmmo = 0;
            Comp.Session.UniqueListAdd(this, Comp.Session.ChargingWeaponsIndexer, Comp.Session.ChargingWeapons);

            if (!Comp.UnlimitedPower)
                Comp.Ai.RequestedWeaponsDraw += RequiredPower;

            ChargeUntilTick = syncCharge ? ChargeUntilTick : (uint)System.ReloadTime + Comp.Session.Tick;
            Comp.Ai.OverPowered = Comp.Ai.RequestedWeaponsDraw > 0 && Comp.Ai.RequestedWeaponsDraw > Comp.Ai.GridMaxPower;
        }
    }
}
