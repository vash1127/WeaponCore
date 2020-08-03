using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal void ChangeActiveAmmo()
        {
            var proposed = ProposedAmmoId != -1;
            var ammoType = proposed ? System.AmmoTypes[ProposedAmmoId] : System.AmmoTypes[State.AmmoTypeId];
            CanHoldMultMags = ((float)Comp.BlockInventory.MaxVolume * .75) > (ammoType.AmmoDef.Const.MagVolume * 2);
            ScheduleAmmoChange = false;

            if (ActiveAmmoDef == ammoType)
                return;

            if (proposed)
            {
                State.AmmoTypeId = ProposedAmmoId;
                ProposedAmmoId = -1;
            }

            ActiveAmmoDef = System.AmmoTypes[State.AmmoTypeId];
            SetWeaponDps();
            UpdateWeaponRange();

            if (System.Session.MpActive && System.Session.IsServer)
                System.Session.SendCompData(Comp);
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
                        else if (Comp.Data.Repo.State.PlayerId > 0)
                        {
                            var message = $"Weapon inventory full, ejecting {ammoChange.Item.Content.SubtypeName} magazine";
                            Comp.Session.SendClientNotify(Comp.Data.Repo.State.PlayerId, message, true, "Red", 3000);
                        }
                        MyFloatingObjects.Spawn(ammoChange.Item, Dummies[0].Info.Position, MyPivotDir, MyPivotUp);
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
                var canReload = State.CurrentAmmo == 0 && ActiveAmmoDef.AmmoDef.Const.Reloadable;
                var proposedAmmo = System.AmmoTypes[ProposedAmmoId];

                var unloadMag = !canReload && !instantChange && !Reloading && State.CurrentAmmo == ActiveAmmoDef.AmmoDef.Const.MagazineSize;

                if (unloadMag && proposedAmmo.AmmoDef.Const.Reloadable)
                {
                    State.CurrentAmmo = 0;
                    canReload = true;
                    System.Session.FutureEvents.Schedule(AmmoChange, new AmmoLoad { Amount = 1, Change = AmmoLoad.ChangeType.Add, OldId = State.AmmoTypeId, Item = ActiveAmmoDef.AmmoDef.Const.AmmoItem }, 1);
                }

                if (instantChange)
                    ChangeActiveAmmo();
                else if (canReload)
                    ScheduleAmmoChange = true;

                if (proposedAmmo.AmmoDef.Const.Reloadable && canReload)
                    Session.ComputeServerStorage(this);
            }
            else if (System.Session.MpActive)
                System.Session.SendAmmoCycleRequest(Comp, WeaponId, newAmmoId);
        }

        internal bool HasAmmo(bool updateMags = false)
        {
            if (Comp.Session.IsCreative || !ActiveAmmoDef.AmmoDef.Const.Reloadable || System.DesignatorWeapon)
            {
                NoMagsToLoad = false;
                return true;
            }

            if (updateMags) State.CurrentMags = Comp.BlockInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId);

            var energyDrainable = ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && Comp.Ai.HasPower;
            var nothingToLoad = State.CurrentMags <= 0 && !energyDrainable;

            if (NoMagsToLoad)
            {
                if (nothingToLoad)
                    return false;

                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, false);
                //Target.Reset(Comp.Session.Tick, Target.States.NoMagsToLoad);

                Comp.Ai.Construct.RootAi.Construct.OutOfAmmoWeapons.Remove(this);

                NoMagsToLoad = false;
            }
            else if (nothingToLoad)
            {
                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, true);
                Comp.Ai.Construct.RootAi.Construct.OutOfAmmoWeapons.Add(this);

                NoMagsToLoad = true;
            }

            return !NoMagsToLoad;
        }

        internal bool ClientReload(bool networkCaller = false)
        {
            var invalidState = State.CurrentAmmo != 0 || Reloading || State == null || ActiveAmmoDef.AmmoDef?.Const == null || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready;
            if (invalidState || !ActiveAmmoDef.AmmoDef.Const.Reloadable || System.DesignatorWeapon || !Comp.IsWorking)
                return false;

            if (AnimationDelayTick > System.Session.Tick && (LastEventCanDelay || LastEvent == EventTriggers.Firing))
                return false;

            if (State.CurrentMags <= 0)
            {
                if (!NoMagsToLoad)
                {
                    EventTriggerStateChanged(EventTriggers.NoMagsToLoad, true);
                }
                NoMagsToLoad = true;
                return false;
            }

            ClientReloadId = State.ReloadId;

            if (NoMagsToLoad)
            {
                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, false);
                NoMagsToLoad = false;
            }

            Reloading = true;
            FinishBurst = false;
            //ShootOnce = false;

            if (!ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay) ShotsFired = 0;

            StartReload();
            return true;
        }

        internal bool ServerReload()
        {
            var invalidState = State.CurrentAmmo != 0 || Reloading || PullingAmmo || State == null || ActiveAmmoDef.AmmoDef?.Const == null || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready;
            if (invalidState || !ActiveAmmoDef.AmmoDef.Const.Reloadable || System.DesignatorWeapon || !Comp.IsWorking)
                return false;

            if (AnimationDelayTick > Comp.Session.Tick && (LastEventCanDelay || LastEvent == EventTriggers.Firing))
                return false;

            var hadNoMags = NoMagsToLoad;
            var scheduledChange = ScheduleAmmoChange;

            if (scheduledChange)
                ChangeActiveAmmo();

            var hasAmmo = HasAmmo();

            FinishBurst = false;
            ShootOnce = false;

            if (!hasAmmo)
            {
                if (hadNoMags != NoMagsToLoad)
                {
                    if (System.Session.MpActive)
                        System.Session.SendWeaponState(this);
                }
                return false;
            }

            if (!ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay) ShotsFired = 0;

            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                Comp.BlockInventory.RemoveItemsOfType(1, ActiveAmmoDef.AmmoDefinitionId);

            StartReload();
            return true;
        }

        internal void StartReload()
        {
            Reloading = true;
            uint delay;
            if (System.WeaponAnimationLengths.TryGetValue(EventTriggers.Reloading, out delay))
            {
                AnimationDelayTick = Comp.Session.Tick + delay;
                EventTriggerStateChanged(EventTriggers.Reloading, true);
            }

            if (ActiveAmmoDef.AmmoDef.Const.MustCharge && !Comp.Session.ChargingWeaponsIndexer.ContainsKey(this))
                ChargeReload();
            else if (!ActiveAmmoDef.AmmoDef.Const.MustCharge)
            {
                if (System.ReloadTime > 0)
                {
                    CancelableReloadAction += Reloaded;
                    ReloadSubscribed = true;
                    Comp.Session.FutureEvents.Schedule(CancelableReloadAction, null, (uint)System.ReloadTime);
                }
                else Reloaded();
            }

            if (System.Session.MpActive && System.Session.IsServer)
                System.Session.SendWeaponState(this);

            if (ReloadEmitter == null || ReloadSound == null || ReloadEmitter.IsPlaying) return;
            ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);
        }

        internal void Reloaded(object o = null)
        {
            using (Comp.MyCube.Pin())
            {

                if (State == null || Comp.Data.Repo == null || Comp.Ai == null || Comp.MyCube.MarkedForClose) return;

                LastLoadedTick = Comp.Session.Tick;

                if (ActiveAmmoDef.AmmoDef.Const.MustCharge)
                {

                    if (System.Session.IsServer && ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                        State.CurrentAmmo = ActiveAmmoDef.AmmoDef.Const.EnergyMagSize;

                    Comp.CurrentCharge -= State.CurrentCharge;
                    State.CurrentCharge = MaxCharge;
                    Comp.CurrentCharge += MaxCharge;

                    ChargeUntilTick = 0;
                    ChargeDelayTicks = 0;
                }
                else if (ReloadSubscribed)
                {
                    CancelableReloadAction -= Reloaded;
                    ReloadSubscribed = false;
                }

                EventTriggerStateChanged(EventTriggers.Reloading, false);

                if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                {

                    if (System.Session.IsServer)
                        State.CurrentAmmo = ActiveAmmoDef.AmmoDef.Const.MagazineDef.Capacity;
                    else if (ClientReloadId == State.ReloadId)
                    {
                        State.CurrentAmmo = ActiveAmmoDef.AmmoDef.Const.MagazineDef.Capacity;
                        ClientSimShots = State.CurrentAmmo;
                    }
                }

                if (System.Session.IsServer)
                {
                    ++State.ReloadId;
                    if (System.Session.MpActive)
                        System.Session.SendWeaponState(this);
                }

                ShootOnce = false;
                Reloading = false;
            }

        }
        public void ChargeReload(bool syncCharge = false)
        {
            if (!syncCharge)
            {
                State.CurrentAmmo = 0;
                Comp.CurrentCharge -= State.CurrentCharge;
                State.CurrentCharge = 0;
            }

            Comp.Session.UniqueListAdd(this, Comp.Session.ChargingWeaponsIndexer, Comp.Session.ChargingWeapons);

            if (!Comp.UnlimitedPower)
                Comp.Ai.RequestedWeaponsDraw += RequiredPower;

            ChargeUntilTick = syncCharge ? ChargeUntilTick : (uint)System.ReloadTime + Comp.Session.Tick;
            Comp.Ai.OverPowered = Comp.Ai.RequestedWeaponsDraw > 0 && Comp.Ai.RequestedWeaponsDraw > Comp.Ai.GridMaxPower;
        }
    }
}
