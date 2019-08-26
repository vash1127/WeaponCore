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

        internal static bool GetGuidance(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            return comp?.Set.Value.Guidance ?? false;
        }

        internal static void SetGuidance(IMyTerminalBlock block, bool newValue)
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

        internal static bool VisibleAll(IMyTerminalBlock block, int id)
        {
            var logic = block?.Components?.Get<WeaponComponent>();
            return logic != null;
        }

        internal static bool EnableWeapon(IMyTerminalBlock block, int count, int id)
        {
            var logic = block?.Components?.Get<WeaponComponent>();
            var enable = false;
            if (logic != null)
            {
                for (int i = 0; i < logic.Platform.Weapons.Length; i++)
                {
                    if (logic.Platform.Weapons[i].System.WeaponId == id)
                        enable = true;
                }
            }
            //Log.Line($"{count} - {id} - {enable}");

            return enable;
        }

        internal static bool IsCoreWeapon(IMyTerminalBlock block, int count, int id)
        {
            var logic = block?.Components?.Get<WeaponComponent>();
            var enable = false;
            if (logic != null)
            {
                enable = true;
            }
            //Log.Line($"{count} - {id} - {enable}");

            return enable;
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
