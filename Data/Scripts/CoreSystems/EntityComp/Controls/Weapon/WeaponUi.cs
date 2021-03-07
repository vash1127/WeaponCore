using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace CoreSystems
{
    internal static partial class BlockUi
    {
        internal static void RequestSetRof(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            if (comp.Session.IsServer) {
                comp.Data.Repo.Values.Set.RofModifier = newValue;
                Weapon.WeaponComponent.SetRof(comp);
            }
            else
                comp.Session.SendSetCompFloatRequest(comp, newValue, PacketType.RequestSetRof);
        }

        internal static void RequestSetDps(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            
            if (comp.Session.IsServer)  {
                comp.Data.Repo.Values.Set.DpsModifier = newValue;
                Weapon.WeaponComponent.SetDps(comp, true);
                if (comp.Session.MpActive)
                    comp.Session.SendComp(comp);
            }
            else
                comp.Session.SendSetCompFloatRequest(comp, newValue, PacketType.RequestSetDps);
        }


        internal static void RequestSetRange(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            
            if (comp.Session.IsServer)  {
                
                comp.Data.Repo.Values.Set.Range = newValue;
                Weapon.WeaponComponent.SetRange(comp);
                if (comp.Session.MpActive)
                    comp.Session.SendComp(comp);
            }
            else
                comp.Session.SendSetCompFloatRequest(comp, newValue, PacketType.RequestSetRange);
        }

        internal static void RequestSetGuidance(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            if (comp.Session.IsServer) {
                comp.Data.Repo.Values.Set.Guidance = newValue;
                if (comp.Session.MpActive)
                    comp.Session.SendComp(comp);
            }
            else
                comp.Session.SendSetCompBoolRequest(comp, newValue, PacketType.RequestSetGuidance);
        }

        internal static void RequestSetOverload(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            if (comp.Session.IsServer)  {

                comp.Data.Repo.Values.Set.Overload = newValue ? 2 : 1;
                Weapon.WeaponComponent.SetRof(comp);
                if (comp.Session.MpActive)
                    comp.Session.SendComp(comp);
            }
            else
                comp.Session.SendSetCompBoolRequest(comp, newValue, PacketType.RequestSetOverload);
        }

        internal static bool GetGuidance(IMyTerminalBlock block, int wepId)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Guidance;
        }

        internal static float GetDps(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;
            return comp.Data.Repo.Values.Set.DpsModifier;
        }

        internal static float GetRof(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;
            return comp.Data.Repo.Values.Set.RofModifier;
        }
        internal static bool GetOverload(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overload == 2;
        }


        internal static float GetRange(IMyTerminalBlock block) {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 100;
            return comp.Data.Repo.Values.Set.Range;
        }

        internal static bool ShowRange(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;

            return comp.HasTurret;
        }

        internal static float GetMinRange(IMyTerminalBlock block)
        {
            return 0;
        }

        internal static float GetMaxRange(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;

            var maxTrajectory = 0f;
            for (int i = 0; i < comp.Platform.Weapons.Count; i++)
            {
                var curMax = comp.Platform.Weapons[i].GetMaxWeaponRange();
                if (curMax > maxTrajectory)
                    maxTrajectory = (float)curMax;
            }
            return maxTrajectory;
        }

        internal static bool GetNeutrals(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Neutrals;
        }

        internal static void RequestSetNeutrals(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            Weapon.WeaponComponent.RequestSetValue(comp, "Neutrals", value, comp.Session.PlayerId);
        }

        internal static bool GetUnowned(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Unowned;
        }

        internal static void RequestSetUnowned(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            Weapon.WeaponComponent.RequestSetValue(comp, "Unowned", value, comp.Session.PlayerId);
        }

        internal static bool GetFocusFire(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.FocusTargets;
        }

        internal static void RequestSetFocusFire(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            var value = newValue ? 1 : 0;
            Weapon.WeaponComponent.RequestSetValue(comp, "FocusTargets", value, comp.Session.PlayerId);
        }

        internal static bool GetSubSystems(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.FocusSubSystem;
        }

        internal static void RequestSetSubSystems(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            var value = newValue ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "FocusSubSystem", value, comp.Session.PlayerId);
        }

        internal static bool GetBiologicals(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Biologicals;
        }

        internal static void RequestSetBiologicals(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            var value = newValue ? 1 : 0;
            Weapon.WeaponComponent.RequestSetValue(comp, "Biologicals", value, comp.Session.PlayerId);
        }

        internal static bool GetProjectiles(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Projectiles;
        }

        internal static void RequestSetProjectiles(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            Weapon.WeaponComponent.RequestSetValue(comp, "Projectiles", value, comp.Session.PlayerId);
        }

        internal static bool GetMeteors(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Meteors;
        }

        internal static void RequestSetMeteors(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            Weapon.WeaponComponent.RequestSetValue(comp, "Meteors", value, comp.Session.PlayerId);
        }

        internal static bool GetGrids(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Grids;
        }

        internal static void RequestSetGrids(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            Weapon.WeaponComponent.RequestSetValue(comp, "Grids", value, comp.Session.PlayerId);
        }

        internal static bool GetShoot(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.State.TerminalAction == CoreComponent.TriggerActions.TriggerOn;
        }

        internal static void RequestSetShoot(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            Log.Line($"1: {comp == null}");
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? CoreComponent.TriggerActions.TriggerOn : CoreComponent.TriggerActions.TriggerOff;
            Log.Line($"2: {value} - {newValue}");

            comp.RequestShootUpdate(value, comp.Session.MpServer ? comp.Session.PlayerId : -1);
        }

        internal static long GetSubSystem(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;
            return (int)comp.Data.Repo.Values.Set.Overrides.SubSystem;
        }

        internal static void RequestSubSystem(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            Weapon.WeaponComponent.RequestSetValue(comp, "SubSystems", (int) newValue, comp.Session.PlayerId);
        }

        internal static void ListSubSystems(List<MyTerminalControlComboBoxItem> subSystemList)
        {
            foreach (var sub in SubList) subSystemList.Add(sub);
        }

        private static readonly List<MyTerminalControlComboBoxItem> SubList = new List<MyTerminalControlComboBoxItem>
        {
            new MyTerminalControlComboBoxItem { Key = 0, Value = MyStringId.GetOrCompute($"{(WeaponDefinition.TargetingDef.BlockTypes)0}") },
            new MyTerminalControlComboBoxItem { Key = 1, Value = MyStringId.GetOrCompute($"{(WeaponDefinition.TargetingDef.BlockTypes)1}") },
            new MyTerminalControlComboBoxItem { Key = 2, Value = MyStringId.GetOrCompute($"{(WeaponDefinition.TargetingDef.BlockTypes)2}") },
            new MyTerminalControlComboBoxItem { Key = 3, Value = MyStringId.GetOrCompute($"{(WeaponDefinition.TargetingDef.BlockTypes)3}") },
            new MyTerminalControlComboBoxItem { Key = 4, Value = MyStringId.GetOrCompute($"{(WeaponDefinition.TargetingDef.BlockTypes)4}") },
            new MyTerminalControlComboBoxItem { Key = 5, Value = MyStringId.GetOrCompute($"{(WeaponDefinition.TargetingDef.BlockTypes)5}") },
            new MyTerminalControlComboBoxItem { Key = 6, Value = MyStringId.GetOrCompute($"{(WeaponDefinition.TargetingDef.BlockTypes)6}") },
            new MyTerminalControlComboBoxItem { Key = 7, Value = MyStringId.GetOrCompute($"{(WeaponDefinition.TargetingDef.BlockTypes)7}") },
        };

        internal static long GetMovementMode(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;
            return (int)comp.Data.Repo.Values.Set.Overrides.MoveMode;
        }

        internal static void RequestMovementMode(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            Weapon.WeaponComponent.RequestSetValue(comp, "MovementModes", (int)newValue, comp.Session.PlayerId);
        }

        internal static void ListMovementModes(List<MyTerminalControlComboBoxItem> moveList)
        {
            foreach (var sub in MoveList) moveList.Add(sub);
        }

        private static readonly List<MyTerminalControlComboBoxItem> MoveList = new List<MyTerminalControlComboBoxItem>
        {
            new MyTerminalControlComboBoxItem { Key = 0, Value = MyStringId.GetOrCompute($"{(ProtoWeaponOverrides.MoveModes)0}") },
            new MyTerminalControlComboBoxItem { Key = 1, Value = MyStringId.GetOrCompute($"{(ProtoWeaponOverrides.MoveModes)1}") },
            new MyTerminalControlComboBoxItem { Key = 2, Value = MyStringId.GetOrCompute($"{(ProtoWeaponOverrides.MoveModes)2}") },
            new MyTerminalControlComboBoxItem { Key = 3, Value = MyStringId.GetOrCompute($"{(ProtoWeaponOverrides.MoveModes)3}") },
        };

        internal static long GetControlMode(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;
            return (int)comp.Data.Repo.Values.Set.Overrides.Control;
        }

        internal static void RequestControlMode(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            Weapon.WeaponComponent.RequestSetValue(comp, "ControlModes", (int)newValue, comp.Session.PlayerId);
        }

        internal static void ListControlModes(List<MyTerminalControlComboBoxItem> controlList)
        {
            foreach (var sub in ControlList) controlList.Add(sub);
        }

        private static readonly List<MyTerminalControlComboBoxItem> ControlList = new List<MyTerminalControlComboBoxItem>
        {
            new MyTerminalControlComboBoxItem { Key = 0, Value = MyStringId.GetOrCompute($"{(ProtoWeaponOverrides.ControlModes)0}") },
            new MyTerminalControlComboBoxItem { Key = 1, Value = MyStringId.GetOrCompute($"{(ProtoWeaponOverrides.ControlModes)1}") },
            new MyTerminalControlComboBoxItem { Key = 2, Value = MyStringId.GetOrCompute($"{(ProtoWeaponOverrides.ControlModes)2}") },
        };
    }
}
