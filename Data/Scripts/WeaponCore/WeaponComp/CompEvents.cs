using System;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;

namespace WeaponCore.Support
{ 
    public partial class WeaponComponent
    {

        internal void RegisterEvents(bool register = true)
        {
            if (register)
            {
                Turret.AppendingCustomInfo += AppendingCustomInfo;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);
                BlockInventory.ContentsAdded += OnContentsAdded;
                BlockInventory.ContentsRemoved += OnContentsRemoved;
                BlockInventory.BeforeContentsChanged += OnBeforeContentsChanged;
            }
            else
            {
                Turret.AppendingCustomInfo -= AppendingCustomInfo;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
                BlockInventory.ContentsAdded -= OnContentsAdded;
                BlockInventory.ContentsRemoved -= OnContentsRemoved;
                BlockInventory.BeforeContentsChanged -= OnBeforeContentsChanged;
            }
        }

        private void OnBeforeContentsChanged(MyInventoryBase inventory)
        {
            try
            {
                if (FullInventory)
                {
                    Log.Line("BeforeChange, was full inventory, resetting AmmoTicks");
                    ResetAmmoTimers();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnInventoryContentChanged: {ex}"); }
        }

        internal void OnContentsAdded(MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                var defId = item.Content.GetId();

                int weaponId;
                if (!Platform.Structure.AmmoToWeaponIds.TryGetValue(defId, out weaponId)) return;

                var weapon = Platform.Weapons[weaponId];

                Session.Instance.InventoryEvent.Enqueue(new InventoryChange(weapon, item, amount, InventoryChange.ChangeType.Add));

            }
            catch (Exception ex) { Log.Line($"Exception in OnContentsAdded: {ex}"); }
        }

        internal void OnContentsRemoved(MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                var defId = item.Content.GetId();

                int weaponId;
                if (!Platform.Structure.AmmoToWeaponIds.TryGetValue(defId, out weaponId)) return;

                if (FullInventory)
                {
                    BlockInventory.Refresh();
                    FullInventory = BlockInventory.CargoPercentage >= 0.5;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnContentsRemoved: {ex}"); }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsWorking = myCubeBlock.IsWorking;
            IsFunctional = myCubeBlock.IsFunctional;
        }

        internal string GetShieldStatus()
        {
            if (!State.Value.Online && !MyCube.IsFunctional) return "[Controller Faulty]";
            if (!State.Value.Online && !MyCube.IsWorking) return "[Controller Offline]";
            return "[Shield Up]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetShieldStatus();
                if (status == "[Shield Up]" || status == "[Shield Down]" || status == "[Shield Offline]" || status == "[Insufficient Power]")
                {
                    stringBuilder.Append(status +
                                         "\n" +
                                         "\n[Shield Power]: " + SinkCurrentPower.ToString("0.0") + " Mw");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller AppendingCustomInfo: {ex}"); }
        }
    }
}
