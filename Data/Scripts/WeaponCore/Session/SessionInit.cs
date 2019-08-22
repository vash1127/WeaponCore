using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
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

            var env = MyDefinitionManager.Static.EnvironmentDefinition;
            if (env.LargeShipMaxSpeed > MaxEntitySpeed) MaxEntitySpeed = env.LargeShipMaxSpeed;
            else if (env.SmallShipMaxSpeed > MaxEntitySpeed) MaxEntitySpeed = env.SmallShipMaxSpeed;
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

            Physics = MyAPIGateway.Physics;
            Camera = MyAPIGateway.Session.Camera;
        }

        internal void Init()
        {
            if (Inited) return;
            Log.Init("debugdevelop.log");
            Log.Line($"Logging Started");
            DsUtil.Start("blockSpheres", true);

            foreach (var x in _weaponDefinitions)
            {
                var ae = x.Ammo.AreaEffect;
                var areaRadius = ae.AreaEffectRadius;
                var detonateRadius = ae.Detonation.DetonationRadius;
                var fragments = x.Ammo.Shrapnel.Fragments > 0 ? x.Ammo.Shrapnel.Fragments : 1;
                if (areaRadius > 0)
                {
                    if (!LargeBlockSphereDb.ContainsKey(ModRadius(areaRadius, true)))
                        GenerateBlockSphere(MyCubeSize.Large, ModRadius(areaRadius, true));
                    if (!LargeBlockSphereDb.ContainsKey(ModRadius(areaRadius / fragments, true)))
                        GenerateBlockSphere(MyCubeSize.Large, ModRadius(areaRadius / fragments, true));

                    if (!SmallBlockSphereDb.ContainsKey(ModRadius(areaRadius, false)))
                        GenerateBlockSphere(MyCubeSize.Small, ModRadius(areaRadius, false));
                    if (!SmallBlockSphereDb.ContainsKey(ModRadius(areaRadius / fragments, false)))
                        GenerateBlockSphere(MyCubeSize.Small, ModRadius(areaRadius / fragments, false));

                }
                if (detonateRadius > 0)
                {
                    if (!LargeBlockSphereDb.ContainsKey(ModRadius(detonateRadius, true)))
                        GenerateBlockSphere(MyCubeSize.Large, ModRadius(detonateRadius, true));
                    if (!LargeBlockSphereDb.ContainsKey(ModRadius(detonateRadius / fragments, true)))
                        GenerateBlockSphere(MyCubeSize.Large, ModRadius(detonateRadius / fragments, true));

                    if (!SmallBlockSphereDb.ContainsKey(ModRadius(detonateRadius, false)))
                        GenerateBlockSphere(MyCubeSize.Small, ModRadius(detonateRadius, false));
                    if (!SmallBlockSphereDb.ContainsKey(ModRadius(detonateRadius / fragments, false)))
                        GenerateBlockSphere(MyCubeSize.Small, ModRadius(detonateRadius / fragments, false));
                }
            }
            DsUtil.Complete();
            foreach (var weaponDef in _weaponDefinitions)
            {
                foreach (var mount in weaponDef.Assignments.MountPoints)
                {
                    var subTypeId = mount.SubtypeId;
                    var partId = mount.SubpartId;
                    if (!_turretDefinitions.ContainsKey(subTypeId))
                    {
                        _turretDefinitions[subTypeId] = new Dictionary<string, string>
                        {
                            [partId] = weaponDef.HardPoint.WeaponId
                        };
                        _subTypeIdToWeaponDefs[subTypeId] = new List<WeaponDefinition> {weaponDef};
                    }
                    else
                    {
                        _turretDefinitions[subTypeId][partId] = weaponDef.HardPoint.WeaponId;
                        _subTypeIdToWeaponDefs[subTypeId].Add(weaponDef);
                    }
                }
            }

            foreach (var tDef in _turretDefinitions)
            {
                var subTypeIdHash = MyStringHash.GetOrCompute(tDef.Key);
                SubTypeIdHashMap.Add(tDef.Key, subTypeIdHash);

                WeaponPlatforms.Add(subTypeIdHash, new WeaponStructure(tDef, _subTypeIdToWeaponDefs[tDef.Key]));
            }
            for (int i = 0; i < Projectiles.Wait.Length; i++)
            {
                Projectiles.EntityPool[i] = new EntityPool<MyEntity>[ModelCount];
                for (int j = 0; j < ModelCount; j++)
                    Projectiles.EntityPool[i][j] = new EntityPool<MyEntity>(0, ModelIdToName[j], WeaponCore.Projectiles.Projectiles.EntityActivator);
            }
            Inited = true;
        }
    }
}
