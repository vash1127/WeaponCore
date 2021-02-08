using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    internal static partial class BlockUi
    {
        internal static bool GetShowArea(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<SupportSys.SupportComponent>();
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.ArmorShowArea;

        }

        internal static void RequestSetShowArea(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<SupportSys.SupportComponent>();
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            SupportSys.SupportComponent.RequestSetValue(comp, "ArmorShowArea", value, comp.Session.PlayerId);
        }
    }
}
