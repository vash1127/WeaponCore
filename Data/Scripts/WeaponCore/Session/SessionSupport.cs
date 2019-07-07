using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Input;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore
{
    public partial class Session
    {
        public void Handler(object o)
        {
            try
            {
                var message = o as byte[];
                if (message == null) return;
                var slaveDefArray = MyAPIGateway.Utilities.SerializeFromBinary<WeaponDefinition[]>(message);
                foreach (var wepDef in slaveDefArray)
                    _weaponDefinitions.Add(wepDef);
            }
            catch (Exception ex) { Log.Line($"Exception in Handler: {ex}"); }
        }

        internal void Timings()
        {
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick180 = Tick % 180 == 0;
            Tick300 = Tick % 300 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;
            if (_count++ == 59)
            {
                _count = 0;
            }
            _lCount++;
            if (_lCount == 129)
            {
                _lCount = 0;
                _eCount++;
                if (_eCount == 10) _eCount = 0;
            }
            if (!GameLoaded && Tick > 100)
            {
                if (FirstLoop && Tick > 100)
                {
                    if (!MiscLoaded)
                    {
                        MiscLoaded = true;
                        if (!IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);
                    }
                    GameLoaded = true;
                }
                else if (!FirstLoop)
                {
                    FirstLoop = true;
                }
            }

            if (ShieldMod && !ShieldApiLoaded && SApi.Load())
                ShieldApiLoaded = true;
            ControlledEntity = Session.CameraController.Entity;
            if (ControlledEntity is IMyGunBaseUser)
            {
                var rawZoom = MyAPIGateway.Session.Camera.FovWithZoom;
                Zoom = rawZoom <= 1 ? rawZoom : 1;
            }
            else Zoom = 1;

            MouseButtonPressed = MyAPIGateway.Input.IsAnyMousePressed();
            if (MouseButtonPressed)
            {
                MouseButtonLeft = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Left);
                MouseButtonMiddle = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Middle);
                MouseButtonRight = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Right);
            }
            else
            {
                MouseButtonLeft = false;
                MouseButtonMiddle = false;
                MouseButtonRight = false;
            }

            if (!_compsToStart.IsEmpty)
            {
                WeaponComponent weaponComp;
                _compsToStart.TryDequeue(out weaponComp);
                weaponComp.MyCube.Components.Add(weaponComp);
                weaponComp.OnAddedToScene();
                Log.Line($"added to comp");
            }
            if (!DedicatedServer) CameraPos = Session.Camera.Position;
        }

        internal void ProcessHits()
        {
            IThreadHits hitEvent;
            while (Projectiles.Hits.TryDequeue(out hitEvent)) hitEvent.Execute();
        }

        private void WebDispatchDone()
        {
            Dispatched = false;
        }

        private void Paused()
        {
            Log.Line($"Stopping all sounds due to pause");
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                foreach (var comp in gridAi.WeaponBase.Values)
                {
                    comp.StopAllSounds();
                }
            }
        }

        #region Events
        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id)) return;
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerConnected: {ex}"); }
        }

        private void PlayerDisconnected(long l)
        {
            try
            {
                IMyPlayer removedPlayer;
                Players.TryRemove(l, out removedPlayer);
                PlayerEventId++;
                if (removedPlayer.SteamUserId == AuthorSteamId)
                {
                    AuthorPlayerId = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerDisconnected: {ex}"); }
        }

        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                Players[id] = player;
                PlayerEventId++;
                if (player.SteamUserId == AuthorSteamId) AuthorPlayerId = player.IdentityId;
            }
            return false;
        }
        #endregion

        #region Misc
        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }
        #endregion
    }
}