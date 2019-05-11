using Sandbox.ModAPI;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Logic
    {
        public enum PlayerNotice
        {
            EmitterInit,
            FieldBlocked,
            OverLoad,
            EmpOverLoad,
            Remodulate,
            NoPower,
            NoLos
        }

        private void PlayerMessages(PlayerNotice notice)
        {
            double radius;
            if (notice == PlayerNotice.EmpOverLoad || notice == PlayerNotice.OverLoad) radius = 500;
            else radius = Turret.CubeGrid.PositionComp.WorldVolume.Radius * 2;

            var center = Turret.CubeGrid.PositionComp.WorldAABB.Center;
            var sphere = new BoundingSphereD(center, radius);
            var sendMessage = false;
            IMyPlayer targetPlayer = null;

            foreach (var player in Session.Instance.Players.Values)
            {
                if (player.IdentityId != MyAPIGateway.Session.Player.IdentityId) continue;
                if (!sphere.Intersects(player.Character.WorldVolume)) continue;
                var relation = MyAPIGateway.Session.Player.GetRelationTo(MyCube.OwnerId);
                if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) continue;
                sendMessage = true;
                targetPlayer = player;
                break;
            }
            if (sendMessage) BroadcastSound(targetPlayer.Character, notice);

            switch (notice)
            {
                case PlayerNotice.EmitterInit:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield is reinitializing and checking LOS, attempting startup in 30 seconds!", 4816);
                    break;
                case PlayerNotice.FieldBlocked:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + "-- the shield's field cannot form when in contact with a solid body", 6720, "Blue");
                    break;
                case PlayerNotice.OverLoad:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 8000, "Red");
                    break;
                case PlayerNotice.EmpOverLoad:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield was EMPed, restarting in 60 seconds!!", 8000, "Red");
                    break;
                case PlayerNotice.Remodulate:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield remodulating, restarting in 5 seconds.", 4800);
                    break;
                case PlayerNotice.NoLos:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- Emitter does not have line of sight, shield offline", 8000, "Red");
                    break;
                case PlayerNotice.NoPower:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- Insufficient Power, shield is failing!", 5000, "Red");
                    break;
            }
            if (Session.Enforced.Debug == 3) Log.Line($"[PlayerMessages] Sending:{sendMessage} - rangeToClinetPlayer:{Vector3D.Distance(sphere.Center, MyAPIGateway.Session.Player.Character.WorldVolume.Center)}");
        }

        private static void BroadcastSound(IMyCharacter character, PlayerNotice notice)
        {
            var soundEmitter = Session.Instance.AudioReady((MyEntity)character);
            if (soundEmitter == null) return;

            MySoundPair pair = null;
            switch (notice)
            {
                case PlayerNotice.EmitterInit:
                    pair = new MySoundPair("Arc_reinitializing");
                    break;
                case PlayerNotice.FieldBlocked:
                    pair = new MySoundPair("Arc_solidbody");
                    break;
                case PlayerNotice.OverLoad:
                    pair = new MySoundPair("Arc_overloaded");
                    break;
                case PlayerNotice.EmpOverLoad:
                    pair = new MySoundPair("Arc_EMP");
                    break;
                case PlayerNotice.Remodulate:
                    pair = new MySoundPair("Arc_remodulating");
                    break;
                case PlayerNotice.NoLos:
                    pair = new MySoundPair("Arc_noLOS");
                    break;
                case PlayerNotice.NoPower:
                    pair = new MySoundPair("Arc_insufficientpower");
                    break;
            }
            if (soundEmitter.Entity != null && pair != null) soundEmitter.PlaySingleSound(pair, true);
        }

        internal void BroadcastMessage(bool forceNoPower = false)
        {
            /*
            if (!state.Value.EmitterLos && Field.ShieldIsMobile && !state.Value.Waking) PlayerMessages(Controllers.PlayerNotice.NoLos);
            else if (state.Value.NoPower || forceNoPower) PlayerMessages(Controllers.PlayerNotice.NoPower);
            else if (state.Value.Overload) PlayerMessages(Controllers.PlayerNotice.OverLoad);
            else if (state.Value.EmpOverLoad) PlayerMessages(Controllers.PlayerNotice.EmpOverLoad);
            else if (state.Value.FieldBlocked) PlayerMessages(Controllers.PlayerNotice.FieldBlocked);
            else if (state.Value.Waking) PlayerMessages(Controllers.PlayerNotice.EmitterInit);
            else if (state.Value.Remodulate) PlayerMessages(Controllers.PlayerNotice.Remodulate);
            state.Value.Message = false;
            */
        }

        private void SaveAndSendAll()
        {
            _firstSync = true;
            if (!_isServer) return;
            Set.SaveSettings();
            Set.NetworkUpdate();
            State.SaveState();
            State.NetworkUpdate();
            if (Session.Enforced.Debug >= 3) Log.Line($"SaveAndSendAll: ControllerId [{Turret.EntityId}]");
        }
        /*
        public static Beam BeamOrientation(Barrel barrel, MyEntitySubpart turretHead, float range)
        {
            Vector3D barrelTip = turretHead.WorldMatrix.Translation +
                (turretHead.WorldMatrix.Forward * barrel.FO) +
                (turretHead.WorldMatrix.Right * barrel.ROff) +
                (turretHead.WorldMatrix.Left * barrel.LOff) +
                (turretHead.WorldMatrix.Up * barrel.UOff) +
                (turretHead.WorldMatrix.Down * barrel.DOff);

            Vector3D beamEndpoint = turretHead.WorldMatrix.Translation +
                (turretHead.WorldMatrix.Forward * range) +
                (turretHead.WorldMatrix.Right * barrel.ROff) +
                (turretHead.WorldMatrix.Left * barrel.LOff) +
                (turretHead.WorldMatrix.Up * barrel.UOff) +
                (turretHead.WorldMatrix.Down * barrel.DOff);

            Beam beam = new Beam(barrelTip, beamEndpoint);
            return beam;
        }
        */
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
    }
}