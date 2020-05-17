using Sandbox.Game.Entities;
using WeaponCore.Control;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Session;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition;

namespace WeaponCore
{
    public partial class Session
    {
        private bool ServerCompStateUpdate(PacketObj data)
        {
            return true;
        }
        private bool ServerCompSettingsUpdate(PacketObj data)
        {
            return true;
        }
        private bool ServerClientMouseEvent(PacketObj data)
        {
            return true;
        }
        private bool ServerActiveControlUpdate(PacketObj data)
        {
            return true;
        }
        private bool ServerFakeTargetUpdate(PacketObj data)
        {
            return true;
        }
        private bool ServerGridSyncRequestUpdate(PacketObj data)
        {
            return true;
        }
        private bool ServerReticleUpdate(PacketObj data)
        {
            return true;
        }
        private bool ServerOverRidesUpdate(PacketObj data)
        {
            return true;
        }
        private bool ServerPlayerControlUpdate(PacketObj data)
        {
            return true;
        }
        private bool ServerWeaponUpdateRequest(PacketObj data)
        {
            return true;
        }
        private bool ServerClientEntityClosed(PacketObj data)
        {
            return true;
        }
        private bool ServerRequestMouseStates(PacketObj data)
        {
            return true;
        }
        private bool ServerCompToolbarShootState(PacketObj data)
        {
            return true;
        }
        private bool ServerRangeUpdate(PacketObj data)
        {
            return true;
        }
        private bool ServerCycleAmmo(PacketObj data)
        {
            return true;
        }
        private bool ServerRescanGroupRequest(PacketObj data)
        {
            return true;
        }
        private bool ServerFixedWeaponHitEvent(PacketObj data)
        {
            return true;
        }
        private bool ServerCompSyncRequest(PacketObj data)
        {
            return true;
        }
        private bool ServerFocusUpdate(PacketObj data)
        {
            return true;
        }
    }
}
