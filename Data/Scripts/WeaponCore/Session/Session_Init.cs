using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Utils;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        private void BeforeStartInit()
        {

            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsServer = MyAPIGateway.Multiplayer.IsServer;
            DedicatedServer = MyAPIGateway.Utilities.IsDedicated;

            MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID, ReceivedPacket);

            if (!DedicatedServer && IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);

            MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest += PlayerConnected;
            if (!DedicatedServer)
            {
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControls;
            }

            if (MpActive)
            {
                SyncDist = MyAPIGateway.Session.SessionSettings.SyncDistance;
                SyncDistSqr = SyncDist * SyncDist;
                SyncBufferedDistSqr = SyncDistSqr + 250000;
            }
            else
            {
                SyncDist = MyAPIGateway.Session.SessionSettings.ViewDistance;
                SyncDistSqr = SyncDist * SyncDist;
                SyncBufferedDistSqr = SyncDistSqr + 250000;
            }

            foreach (var mod in MyAPIGateway.Session.Mods)
                if (mod.PublishedFileId == 1365616918) ShieldMod = true;
            ShieldMod = true;
        }

        internal void Init()
        {
            if (Inited) return;
            Log.Init("debugdevelop.log");
            Log.Line($"Logging Started");
            foreach (var weaponDef in _weaponDefinitions)
            {
                foreach (var mount in weaponDef.TurretDef.MountPoints)
                {
                    var subTypeId = mount.Key;
                    var subPartId = mount.Value;
                    if (!_turretDefinitions.ContainsKey(subTypeId))
                        _turretDefinitions[subTypeId] = new Dictionary<string, string>
                        {
                            [subPartId] = weaponDef.TurretDef.DefinitionId
                        };
                    else _turretDefinitions[subTypeId][subPartId] = weaponDef.TurretDef.DefinitionId;
                }
            }

            foreach (var def in _turretDefinitions)
            {
                var subTypeIdHash = MyStringHash.GetOrCompute(def.Key);
                SubTypeIdHashMap.Add(def.Key, subTypeIdHash);
                WeaponPlatforms.Add(subTypeIdHash, new WeaponStructure(def, _weaponDefinitions));
            }
            Inited = true;

            for (int i = 0; i < Projectiles.Wait.Length; i++)
            {
                Projectiles.EntityPool[i] = new EntityPool<MyEntity>[ModelCount];
                for (int j = 0; j < ModelCount; j++)
                    Projectiles.EntityPool[i][j] = new EntityPool<MyEntity>(0, ModelIdToName[j], WeaponCore.Projectiles.Projectiles.Activator);
            }
        }
    }
}
