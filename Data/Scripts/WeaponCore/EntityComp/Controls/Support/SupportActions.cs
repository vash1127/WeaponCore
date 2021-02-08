using System.Text;
using Sandbox.ModAPI;
using WeaponCore.Support;
using WeaponCore.Platform;
using static WeaponCore.Support.CoreComponent.TriggerActions;

namespace WeaponCore.Control
{
    public  static partial class CustomActions
    {
        #region Call Actions
        internal static void SupportActionToggleShow(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as SupportSys.SupportComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.ArmorShowArea;
            var newValue = newBool ? 1 : 0;

            SupportSys.SupportComponent.RequestSetValue(comp, "ArmorShowArea", newValue, comp.Session.PlayerId);
        }

        #endregion

        #region Writters
        #endregion
    }
}
