
using Sandbox.ModAPI;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent 
    {
        private void ResetAmmoTimers(bool skip = false, Weapon weapon = null)
        {
            foreach (var w in Platform.Weapons)
            {
                if (skip && w == weapon) continue;
                w.AmmoUpdateTick = MyAi.MySession.Tick;
            }
        }
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

        internal void UpdatePivotPos(Weapon weapon, float upPivotOffsetLen)
        {
            var myPivotPos = MyCube.PositionComp.WorldAABB.Center;
            myPivotPos += MyCube.PositionComp.WorldMatrix.Up * upPivotOffsetLen;
            MyPivotPos = myPivotPos;
            MyPivotDir = weapon.EntityPart.PositionComp.WorldMatrix.Forward;
        }
    }
}
