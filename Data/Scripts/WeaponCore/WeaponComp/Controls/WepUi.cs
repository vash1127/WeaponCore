using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using WeaponCore.Support;

namespace WeaponCore
{
    internal static class WepUi
    {
        private static readonly List<MyTerminalControlComboBoxItem> VisibleModes = new List<MyTerminalControlComboBoxItem>()
        {
            new MyTerminalControlComboBoxItem() { Key = 0, Value = MyStringId.GetOrCompute("Always Visible") },
            new MyTerminalControlComboBoxItem() { Key = 1, Value = MyStringId.GetOrCompute("Never Visible") },
            new MyTerminalControlComboBoxItem() { Key = 2, Value = MyStringId.GetOrCompute("Visible On Hit") }
        };

        internal static bool GetEnable(IMyTerminalBlock block, int weaponId)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return false;
            return comp.Platform.Weapons[weaponId].Enabled;
        }

        internal static void SetEnable(IMyTerminalBlock block, int weaponId, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return;
            var weapon = comp.Platform.Weapons[weaponId];
            weapon.Enabled = newValue;
            weapon.StopShooting();
        }

        internal static bool GetGuidance(IMyTerminalBlock block, int i)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp?.Set.Value.Guidance ?? false;
        }

        internal static void SetGuidance(IMyTerminalBlock block, int weaponId, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return;
            comp.Set.Value.Guidance = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static float GetPowerLevel(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp?.Set.Value.PowerScale ?? 0f;
        }

        internal static void SetPowerLevel(IMyTerminalBlock block, float newValue)
        {
            var logic = block?.Components?.Get<WeaponComponent>();
            if (logic == null) return;
            logic.Set.Value.PowerScale = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetDoubleRate(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp?.Set.Value.Guidance ?? false;
        }

        internal static void SetDoubleRate(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null) return;
            comp.Set.Value.Guidance = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        internal static void ListAll(List<MyTerminalControlComboBoxItem> modeList)
        {
            foreach (var mode in VisibleModes) modeList.Add(mode);
        }

        internal static bool VisibleAll(IMyTerminalBlock block)
        {
            var logic = block?.Components?.Get<WeaponComponent>();
            return logic != null;
        }

        internal static bool EnableModes(IMyTerminalBlock block)
        {
            var logic = block?.Components?.Get<WeaponComponent>();
            return logic != null;
        }

        internal static long GetModes(IMyTerminalBlock block)
        {
            var logic = block?.Components?.Get<WeaponComponent>();
            return logic?.Set.Value.Modes ?? 0;
        }

        internal static void SetModes(IMyTerminalBlock block, long newValue)
        {
            var logic = block?.Components?.Get<WeaponComponent>();
            if (logic == null) return;
            logic.Set.Value.Modes = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }
    }
}
