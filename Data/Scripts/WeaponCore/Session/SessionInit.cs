using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Utils;
using VRageMath;
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

            if(IsServer || DedicatedServer)
                MyAPIGateway.Multiplayer.RegisterMessageHandler(ServerPacketId, ServerReceivedPacket);
            else
                MyAPIGateway.Multiplayer.RegisterMessageHandler(ClientPacketId, ClientReceivedPacket);

            if (!DedicatedServer && IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);

            MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest += PlayerConnected;

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
                SyncBufferedDistSqr = (SyncDist + 500) * (SyncDist + 500);
            }

            Physics = MyAPIGateway.Physics;
            Camera = MyAPIGateway.Session.Camera;
            TargetGps = MyAPIGateway.Session.GPS.Create("WEAPONCORE", "", Vector3D.MaxValue, true, false);

            CheckDirtyGrids();

            ApiServer.Load();
        }

        internal void Init()
        {
            if (Inited) return;
            Inited = true;
            Log.Init("debugdevelop.log");
            Log.Line($"Logging Started");

            foreach (var x in WeaponDefinitions)
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
            foreach (var weaponDef in WeaponDefinitions)
            {
                foreach (var mount in weaponDef.Assignments.MountPoints)
                {
                    var subTypeId = mount.SubtypeId;
                    var muzzlePartId = mount.MuzzlePartId;
                    var azimuthPartId = mount.AzimuthPartId;
                    var elevationPartId = mount.ElevationPartId;

                    var extraInfo = new MyTuple<string, string, string> { Item1 = weaponDef.HardPoint.WeaponId, Item2 = azimuthPartId, Item3 = elevationPartId};

                    if (!_turretDefinitions.ContainsKey(subTypeId))
                    {
                        foreach (var def in AllDefinitions)
                        {
                            MyDefinitionId defid;
                            if (def.Id.SubtypeName == subTypeId || (ReplaceVanilla && VanillaCoreIds.TryGetValue(MyStringHash.GetOrCompute(subTypeId), out defid) && defid == def.Id)) {
                                var gunDef = def as MyLargeTurretBaseDefinition;
                                if (gunDef != null)
                                {
                                    var blockDefs = weaponDef.HardPoint.Block;
                                    gunDef.MinAzimuthDegrees = blockDefs.MinAzimuth;
                                    gunDef.MaxAzimuthDegrees = blockDefs.MaxAzimuth;
                                    gunDef.MinElevationDegrees = blockDefs.MinElevation;
                                    gunDef.MaxElevationDegrees = blockDefs.MaxElevation;
                                    gunDef.RotationSpeed = (float)blockDefs.RotateRate;
                                    gunDef.ElevationSpeed = (float)blockDefs.ElevateRate;
                                    gunDef.AiEnabled = false;
                                }

                                WeaponCoreBlockDefs[subTypeId] = def.Id;
                            }
                        }
                        _turretDefinitions[subTypeId] = new Dictionary<string, MyTuple<string, string, string>>
                        {
                            [muzzlePartId] = extraInfo
                        };
                        _subTypeIdToWeaponDefs[subTypeId] = new List<WeaponDefinition> {weaponDef};
                    }
                    else
                    {
                        _turretDefinitions[subTypeId][muzzlePartId] = extraInfo;
                        _subTypeIdToWeaponDefs[subTypeId].Add(weaponDef);
                    }
                }
            }

            foreach (var tDef in _turretDefinitions)
            {
                var subTypeIdHash = MyStringHash.GetOrCompute(tDef.Key);
                SubTypeIdHashMap[tDef.Key] = subTypeIdHash;

                var weapons = _subTypeIdToWeaponDefs[tDef.Key];

                var hasTurret = false;
                foreach (var wepDef in weapons)
                {
                    if (wepDef.HardPoint.Block.TurretAttached)
                        hasTurret = true;
                }

                MyDefinitionId defId;
                if (WeaponCoreBlockDefs.TryGetValue(tDef.Key, out defId))
                {
                    if (hasTurret)
                        WeaponCoreTurretBlockDefs.Add(defId);
                    else
                        WeaponCoreFixedBlockDefs.Add(defId);
                }

                WeaponPlatforms[subTypeIdHash] = new WeaponStructure(this, tDef, weapons);
            }

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlHandler;
        }

    }
}
