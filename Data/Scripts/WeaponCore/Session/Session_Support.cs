using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Serialization;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore
{
    public partial class Session
    {
        public void MasterLoadData()
        {
            //Log.Line($"MasterLoadData");
            MyAPIGateway.Utilities.RegisterMessageHandler(1, Handler);
            MyAPIGateway.Utilities.SendModMessage(2, null);
        }


        public void Handler(object o)
        {
            var message = o as MyTuple<string, string, string, string>[];
            if (message == null)
            {
                Log.Line($"invalid Config");
                return;
            }

            var platforms = MyAPIGateway.Utilities.SerializeFromXML<string[]>(message[0].Item1);
            var weaponMounts = MyAPIGateway.Utilities.SerializeFromXML<string[]>(message[0].Item2);
            var barrelAttachments = MyAPIGateway.Utilities.SerializeFromXML<string[]>(message[0].Item3);
            var weaponDefinitions = MyAPIGateway.Utilities.SerializeFromXML<WeaponDefinition[]>(message[0].Item4);
            /*
            foreach (var pair in tDef)
                MyConfig.TurretDefinitions.Add(pair.Key, new TurretDefinition(pair.Value.TurretMap));

            foreach (var pair in wDef)
                MyConfig.WeaponDefinitions.Add(pair.Key, pair.Value);

            foreach (var pair in bDef)
                MyConfig.BarrelDefinitions.Add(pair.Key, pair.Value);
            */
            Log.Line($"received config from slave");
        }

        internal void Timings()
        {
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick60 = Tick % 60 == 0;
            Tick180 = Tick % 180 == 0;
            Tick300 = Tick % 300 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;

            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10)
                {
                    _lCount = 0;
                    _eCount++;
                    if (_eCount == 10) _eCount = 0;
                }
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
        }

        internal void ProcessHits()
        {
            IThreadHits hitEvent;
            while (_projectiles.Hits.TryDequeue(out hitEvent)) hitEvent.Execute();
        }

        private void WebDispatchDone()
        {
            Dispatched = false;
        }

        #region Events
        public MyEntity3DSoundEmitter AudioReady(MyEntity entity)
        {
            if (Tick - SoundTick < 600 && Tick > 600) return null;
            SoundTick = Tick;

            SoundEmitter.StopSound(false);
            SoundEmitter.Entity = entity;
            SoundEmitter.CustomVolume = MyAPIGateway.Session.Config.GameVolume * 0.75f;
            return SoundEmitter;
        }

        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id))
                {
                    if (Enforced.Debug >= 2) Log.Line($"Player id({id}) already exists");
                    return;
                }
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
                if (Enforced.Debug >= 3) Log.Line($"Removed player, new playerCount:{Players.Count}");
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
                if (Enforced.Debug >= 3) Log.Line($"Added player: {player.DisplayName}, new playerCount:{Players.Count}");
            }
            return false;
        }

        #endregion

        #region Misc
        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            Log.Line(modPath);
            return modPath;
        }

        #endregion

    }
}