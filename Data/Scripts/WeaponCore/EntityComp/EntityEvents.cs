using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using WeaponCore.Platform;
using static WeaponCore.Platform.CorePlatform;
using static WeaponCore.Session;
using static WeaponCore.Support.PartDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore.Support
{
    public partial class CoreComponent
    {
        internal void RegisterEvents(bool register = true)
        {
            if (register)
            {
                if (Registered)
                    Log.Line($"BaseComp RegisterEvents error");
                //TODO change this
                Registered = true;
                if (IsBlock)
                {
                    if (Type == CompType.Weapon)
                        TerminalBlock.AppendingCustomInfo += AppendingCustomInfoWeapon;
                    else if (TypeSpecific == CompTypeSpecific.Support)
                        TerminalBlock.AppendingCustomInfo += AppendingCustomInfoSupport;
                    else if (TypeSpecific == CompTypeSpecific.Upgrade)
                        TerminalBlock.AppendingCustomInfo += AppendingCustomInfoUpgrade;

                    Cube.IsWorkingChanged += IsWorkingChanged;
                    IsWorkingChanged(Cube);
                }

                CoreEntity.OnMarkForClose += OnMarkForClose;

                if (CoreInventory == null) Log.Line($"BlockInventory is null");
                else
                {
                    CoreInventory.InventoryContentChanged += OnContentsChanged;
                    Session.CoreInventoryItems[CoreInventory] = new ConcurrentDictionary<uint, BetterInventoryItem>();
                    Session.ConsumableItemList[CoreInventory] = Session.BetterItemsListPool.Get();

                    var items = CoreInventory.GetItems();
                    for (int i = 0; i < items.Count; i++)
                    {
                        var bItem = Session.BetterInventoryItems.Get();
                        var item = items[i];
                        bItem.Amount = (int)item.Amount;
                        bItem.Item = item;
                        bItem.Content = item.Content;

                        Session.CoreInventoryItems[CoreInventory][items[i].ItemId] = bItem;
                    }
                }
            }
            else
            {
                if (!Registered)
                    Log.Line($"BaseComp UnRegisterEvents error");

                if (Registered)
                {
                    //TODO change this
                    Registered = false;

                    if (IsBlock) {

                        if (Type == CompType.Weapon)
                            TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoWeapon;
                        else if (TypeSpecific == CompTypeSpecific.Support)
                            TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoSupport;
                        else if (TypeSpecific == CompTypeSpecific.Upgrade)
                            TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoUpgrade;

                        Cube.IsWorkingChanged -= IsWorkingChanged;
                    }

                    CoreEntity.OnMarkForClose -= OnMarkForClose;

                    if (CoreInventory == null) Log.Line($"BlockInventory is null");
                    else
                    {
                        CoreInventory.InventoryContentChanged -= OnContentsChanged;
                        ConcurrentDictionary<uint, BetterInventoryItem> removedItems;
                        MyConcurrentList<BetterInventoryItem> removedList;

                        if (Session.CoreInventoryItems.TryRemove(CoreInventory, out removedItems))
                        {
                            foreach (var inventoryItems in removedItems)
                                Session.BetterInventoryItems.Return(inventoryItems.Value);

                            removedItems.Clear();
                        }

                        if (Session.ConsumableItemList.TryRemove(CoreInventory, out removedList))
                            Session.BetterItemsListPool.Return(removedList);
                    }
                }
            }
        }

        private void OnContentsChanged(MyInventoryBase inv, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            BetterInventoryItem cachedItem;
            if (!Session.CoreInventoryItems[CoreInventory].TryGetValue(item.ItemId, out cachedItem))
            {
                cachedItem = Session.BetterInventoryItems.Get();
                cachedItem.Amount = (int)amount;
                cachedItem.Content = item.Content;
                cachedItem.Item = item;
                Session.CoreInventoryItems[CoreInventory].TryAdd(item.ItemId, cachedItem);
            }
            else if (cachedItem.Amount + amount > 0)
            {
                cachedItem.Amount += (int)amount;
            }
            else if (cachedItem.Amount + amount <= 0)
            {
                BetterInventoryItem removedItem;
                if (Session.CoreInventoryItems[CoreInventory].TryRemove(item.ItemId, out removedItem))
                    Session.BetterInventoryItems.Return(removedItem);
            }
            if (Session.IsServer && amount <= 0) {
                for (int i = 0; i < Platform.Weapons.Count; i++)
                    Platform.Weapons[i].CheckInventorySystem = true;
            }
        }

        private static void OnMarkForClose(MyEntity myEntity)
        {
            
            var comp = myEntity.Components.Get<CoreComponent>();
            if (comp?.Ai != null && comp.IsBlock && comp.Slim == comp.Ai.FakeShipController.SlimBlock)
            {
                comp.Ai.PowerDirty = true;
            }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            try {

                var wasFunctional = IsFunctional;
                IsFunctional = myCubeBlock.IsFunctional;

                if (Platform.State == PlatformState.Incomplete) {
                    Log.Line($"Init on Incomplete");
                    Init();
                }
                else {

                    if (!wasFunctional && IsFunctional && IsWorkingChangedTick > 0)
                        Status = Start.ReInit;
                    IsWorking = myCubeBlock.IsWorking;
                    if (Cube.ResourceSink.CurrentInputByType(GId) < 0) Log.Line($"IsWorking:{IsWorking}(was:{wasFunctional}) - Func:{IsFunctional} - GridAvailPow:{Ai.GridAvailablePower} - SinkPow:{SinkPower} - SinkReq:{Cube.ResourceSink.RequiredInputByType(GId)} - SinkCur:{Cube.ResourceSink.CurrentInputByType(GId)}");

                    if (!IsWorking && Registered) {
                        foreach (var w in Platform.Weapons)
                            w.StopShooting();
                    }
                    IsWorkingChangedTick = Session.Tick;
                }

                if (wasFunctional && !IsFunctional && Platform.State == PlatformState.Ready) {

                    if (Type == CompType.Weapon)
                        ((Weapon.WeaponComponent)this).NotFunctional();

                }
                
                if (Session.MpActive && Session.IsServer) {

                    if (Type == CompType.Weapon)
                        ((Weapon.WeaponComponent)this).PowerLoss();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in IsWorkingChanged: {ex}"); }
        }

        internal string GetSystemStatus()
        {
            if (!Cube.IsFunctional) return "[Fault]";
            if (!Cube.IsWorking) return "[Offline]";
            return Ai.AiOwner != 0 ? "[Online]" : "[Rogue Ai] Parts are unowned!!";
        }

        private void AppendingCustomInfoWeapon(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var comp = ((Weapon.WeaponComponent)this);
                var status = GetSystemStatus();

                stringBuilder.Append(status + 
                    "\nConstruct DPS: " + Ai.EffectiveDps.ToString("0.0") +
                    "\nShotsPerSec: " + comp.ShotsPerSec.ToString("0.000") +
                    "\n" +
                    "\nRealDps: " + comp.EffectiveDps.ToString("0.0") +
                    "\nPeakDps: " + comp.PeakDps.ToString("0.0") +
                    "\nBaseDps: " + comp.BaseDps.ToString("0.0") +
                    "\nAreaDps: " + comp.AreaDps.ToString("0.0") +
                    "\nExplode: " + comp.DetDps.ToString("0.0") +
                    "\nCurrent: " + comp.CurrentDps.ToString("0.0") +" ("+ (comp.CurrentDps / comp.PeakDps).ToString("P") + ")");

                if (HeatPerSecond > 0)
                    stringBuilder.Append("\n__________________________________" +
                    "\nHeat Generated / s: " + HeatPerSecond.ToString("0.0") + " W" +
                    "\nHeat Dissipated / s: " + HeatSinkRate.ToString("0.0") + " W" +
                    "\nCurrent Heat: " +CurrentHeat.ToString("0.0") + " j (" + (CurrentHeat / MaxHeat).ToString("P")+")");

                if (HeatPerSecond > 0 && comp.HasEnergyWeapon)
                    stringBuilder.Append("\n__________________________________");

                if (comp.HasEnergyWeapon)
                {
                    stringBuilder.Append("\nCurrent Draw: " + SinkPower.ToString("0.00") + " MWs");
                    if(comp.HasChargeWeapon) stringBuilder.Append("\nCurrent Charge: " + CurrentCharge.ToString("0.00") + " MWs");
                    stringBuilder.Append("\nRequired Power: " + MaxRequiredPower.ToString("0.00") + " MWs");
                }
                
                stringBuilder.Append("\n\n==== Weapons ====");

                var weaponCnt = Platform.Weapons.Count;
                for (int i = 0; i < weaponCnt; i++)
                {
                    var w = Platform.Weapons[i];
                    string shots;
                    if (w.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                    {
                        shots = "\nCharging:" + w.Charging;
                    }
                    else shots = "\n" + w.ActiveAmmoDef.AmmoDef.AmmoMagazine + ": " + w.Ammo.CurrentAmmo;

                    var burst = w.ActiveAmmoDef.AmmoDef.Const.BurstMode ? "\nBurst: " + w.ShotsFired + "(" + w.System.ShotsPerBurst + ") - Delay: " + w .System.Values.HardPoint.Loading.DelayAfterBurst : string.Empty;

                    var endReturn = i + 1 != weaponCnt ? "\n" : string.Empty;

                    stringBuilder.Append("\nName: " + w.System.PartName + shots + burst + "\nReloading: " + w.Reloading + endReturn);

                    string otherAmmo = null;
                    for (int j = 0; j < w.System.AmmoTypes.Length; j++)
                    {
                        var ammo = w.System.AmmoTypes[j];
                        if (ammo == w.ActiveAmmoDef || !ammo.AmmoDef.Const.IsTurretSelectable || string.IsNullOrEmpty(ammo.AmmoDef.AmmoMagazine) || ammo.AmmoName == "Blank" || ammo.AmmoName == "Energy")
                            continue;
                        
                        if (otherAmmo == null)
                            otherAmmo = "\n\nAlternate Magazines:";

                        otherAmmo += $"\n{ammo.AmmoDef.AmmoMagazine}";
                    }

                    if (otherAmmo != null)
                        stringBuilder.Append(otherAmmo);
                }

                if (Debug)
                {
                    foreach (var weapon in Platform.Weapons)
                    {
                        stringBuilder.Append($"\n\nWeapon: {weapon.System.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nTargetState: {weapon.Target.CurrentState} - Manual: {weapon.BaseComp.UserControlled || weapon.Target.IsFakeTarget}");
                        stringBuilder.Append($"\nEvent: {weapon.LastEvent} - Ammo :{!weapon.NoMagsToLoad}");
                        stringBuilder.Append($"\nOverHeat: {weapon.State.Overheated} - Shooting: {weapon.IsShooting}");
                        stringBuilder.Append($"\nisAligned: {weapon.Target.IsAligned}");
                        stringBuilder.Append($"\nCanShoot: {weapon.ShotReady} - Charging: {weapon.Charging}");
                        stringBuilder.Append($"\nAiShooting: {weapon.AiShooting}");
                        stringBuilder.Append($"\n{(weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo ? "ChargeSize: " + weapon.ActiveAmmoDef.AmmoDef.Const.ChargSize.ToString() : "MagSize: " +  weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize.ToString())} - CurrentCharge: {CurrentCharge}({weapon.Ammo.CurrentCharge})");
                        stringBuilder.Append($"\nChargeTime: {weapon.ChargeUntilTick}({weapon.BaseComp.Ai.Session.Tick}) - Delay: {weapon.ChargeDelayTicks}");
                        stringBuilder.Append($"\nCharging: {weapon.Charging}({weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge}) - Delay: {weapon.ChargeDelayTicks}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon AppendingCustomInfo: {ex}"); }
        }

        private void AppendingCustomInfoSupport(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetSystemStatus();

                stringBuilder.Append(status + "\nCurrent: )");

                stringBuilder.Append("\n\n==== SupportSys ====");

                var weaponCnt = Platform.ArmorSupports.Count;
                for (int i = 0; i < weaponCnt; i++)
                {
                    var a = Platform.ArmorSupports[i];
                }

                if (Debug)
                {
                    foreach (var support in Platform.ArmorSupports)
                    {
                        stringBuilder.Append($"\n\nPart: {support.System.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nManual: {support.BaseComp.UserControlled}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AppendingCustomInfoSupport: {ex}"); }
        }

        private void AppendingCustomInfoUpgrade(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetSystemStatus();

                stringBuilder.Append(status + "\nCurrent: )");

                stringBuilder.Append("\n\n==== Upgrade ====");

                var weaponCnt = Platform.ArmorSupports.Count;
                for (int i = 0; i < weaponCnt; i++)
                {
                    var a = Platform.Upgrades[i];
                }

                if (Debug)
                {
                    foreach (var upgrade in Platform.Upgrades)
                    {
                        stringBuilder.Append($"\n\nPart: {upgrade.System.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nManual: {upgrade.BaseComp.UserControlled}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AppendingCustomInfoUpgrade: {ex}"); }
        }
    }
}
