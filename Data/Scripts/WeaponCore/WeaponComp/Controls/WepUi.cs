using System;
using Sandbox.ModAPI;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    internal static class WepUi
    {
        internal static void RequestSetRof(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            if (comp.Session.IsServer) {
                comp.Data.Repo.Set.RofModifier = newValue;
                WeaponComponent.SetRof(comp);
            }
            else
                comp.Session.SendSetCompFloatRequest(comp, newValue, PacketType.RequestSetRof);
        }

        internal static void RequestSetDps(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            
            if (comp.Session.IsServer)  {
                comp.Data.Repo.Set.DpsModifier = newValue;
                WeaponComponent.SetDps(comp);
                if (comp.Session.MpActive)
                    comp.Session.SendCompData(comp);
            }
            else
                comp.Session.SendSetCompFloatRequest(comp, newValue, PacketType.RequestSetDps);
        }


        internal static void RequestSetRange(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            
            if (comp.Session.IsServer)  {
                
                comp.Data.Repo.Set.Range = newValue;
                WeaponComponent.SetRange(comp);
                if (comp.Session.MpActive)
                    comp.Session.SendCompData(comp);
            }
            else
                comp.Session.SendSetCompFloatRequest(comp, newValue, PacketType.RequestSetRange);
        }

        internal static void RequestSetGuidance(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            if (comp.Session.IsServer) {
                comp.Data.Repo.Set.Guidance = newValue;
                if (comp.Session.MpActive)
                    comp.Session.SendCompData(comp);
            }
            else
                comp.Session.SendSetCompBoolRequest(comp, newValue, PacketType.RequestSetGuidance);
        }

        internal static void RequestSetOverload(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            if (comp.Session.IsServer)  {

                comp.Data.Repo.Set.Overload = newValue ? 2 : 1;
                WeaponComponent.SetRof(comp);
                if (comp.Session.MpActive)
                    comp.Session.SendCompData(comp);
            }
            else
                comp.Session.SendSetCompBoolRequest(comp, newValue, PacketType.RequestSetOverload);
        }

        internal static bool GetGuidance(IMyTerminalBlock block, int wepId)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Set.Guidance;
        }

        internal static float GetDps(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return 0;
            return comp.Data.Repo.Set.DpsModifier;
        }

        internal static float GetRof(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return 0;
            return comp.Data.Repo.Set.RofModifier;
        }
        internal static bool GetOverload(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Set.Overload == 2;
        }


        internal static float GetRange(IMyTerminalBlock block) {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return 100;
            return comp.Data.Repo.Set.Range;
        }

        internal static bool ShowRange(IMyTerminalBlock block, int notUsed)
        {
            return true;
        }

        internal static float GetMinRange(IMyTerminalBlock block)
        {
            return 0;
        }

        internal static float GetMaxRange(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return 0;

            var maxTrajectory = 0f;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var curMax = comp.Platform.Weapons[i].GetMaxWeaponRange();
                if (curMax > maxTrajectory)
                    maxTrajectory = (float)curMax;
            }
            return maxTrajectory;
        }
    }
}
