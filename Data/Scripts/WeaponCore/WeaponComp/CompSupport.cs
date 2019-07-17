using Sandbox.ModAPI;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            Turret.RefreshCustomInfo();
            if (update && InControlPanel && InThisTerminal)
            {
                Log.Line("terminal update");
                 MyCube.UpdateTerminal();
            }
        }

        private void SaveAndSendAll()
        {
            _firstSync = true;
            if (!_isServer) return;
            Set.SaveSettings();
            Set.NetworkUpdate();
            State.SaveState();
            State.NetworkUpdate();
        }

        internal void UpdatePivotPos(Weapon weapon)
        {
            var weaponPComp = weapon.EntityPart.PositionComp;
            var weaponCenter = weaponPComp.WorldMatrix.Translation;
            var weaponForward = weaponPComp.WorldMatrix.Forward;
            var weaponUp = weaponPComp.WorldMatrix.Up;

            var blockCenter = MyCube.PositionComp.WorldAABB.Center;
            var blockUp = MyCube.PositionComp.WorldMatrix.Up;
            MyPivotDir = weaponForward;
            MyPivotUp = weaponUp;
            MyPivotPos = UtilsStatic.GetClosestPointOnLine1(blockCenter, blockUp, weaponCenter, weaponForward);
            MyPivotTestLine = new LineD(MyCube.PositionComp.WorldAABB.Center, MyPivotPos);
        }

        public void StopRotSound(bool force)
        {
            if (RotationEmitter != null)
            {
                if (!RotationEmitter.IsPlaying)
                    return;
                RotationEmitter.StopSound(force);
            }
        }

        public void StopAllSounds()
        {
            RotationEmitter?.StopSound(true, true);
            foreach (var w in Platform.Weapons)
            {
                w.StopReloadSound();
                w.StopRotateSound();
                w.StopShooting(true);
            }
        }

    }
}
