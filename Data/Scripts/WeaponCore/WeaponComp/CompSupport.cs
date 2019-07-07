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
                var mousePos = MyAPIGateway.Input.GetMousePosition();
                var startPos = new Vector2(800, 700);
                var endPos = new Vector2(1070, 750);
                var match1 = mousePos.Between(ref startPos, ref endPos);
                var match2 = mousePos.Y > 700 && mousePos.Y < 760 && mousePos.X > 810 && mousePos.X < 1070;
                if (!(match1 && match2)) MyCube.UpdateTerminal();
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

            var blockCenter = MyCube.PositionComp.WorldAABB.Center;
            var blockUp = MyCube.PositionComp.WorldMatrix.Up;
            MyPivotDir = weaponForward;
            MyPivotPos = UtilsStatic.GetClosestPointOnLine1(blockCenter, blockUp, weaponCenter, weaponForward);
            //MyPivotTestLine = new LineD(MyCube.PositionComp.WorldAABB.Center, MyPivotPos);
        }

        public void StopRotSound(bool force)
        {
            if (TrackingWeapon.System.TurretRotationSound && RotationEmitter != null)
            {
                if (!RotationEmitter.IsPlaying)
                    return;
                Log.Line("StopRotSound");
                RotationEmitter.StopSound(force);
            }
        }
    }
}
