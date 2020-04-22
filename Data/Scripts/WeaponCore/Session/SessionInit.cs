using System;
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
            IsCreative = MyAPIGateway.Session.CreativeMode;
            IsClient = !IsServer && !DedicatedServer && MpActive;
            HandlesInput = !IsServer || IsServer && !DedicatedServer;

            if (IsServer || DedicatedServer)
                MyAPIGateway.Multiplayer.RegisterMessageHandler(ServerPacketId, ServerReceivedPacket);
            else
                MyAPIGateway.Multiplayer.RegisterMessageHandler(ClientPacketId, ClientReceivedPacket);
            
            if (IsServer)
            {
                MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisconnected;
                MyVisualScriptLogicProvider.PlayerRespawnRequest += PlayerConnected;
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
                SyncBufferedDistSqr = (SyncDist + 500) * (SyncDist + 500);
            }

            Physics = MyAPIGateway.Physics;
            Camera = MyAPIGateway.Session.Camera;
            TargetGps = MyAPIGateway.Session.GPS.Create("WEAPONCORE", "", Vector3D.MaxValue, true, false);

            CheckDirtyGrids();

            ApiServer.Load();

            if (!IsClient) Enforced = new Enforcements(this);
            else
            {
                //Client enforcement request
            }
        }

        internal void Init()
        {
            if (Inited) return;
            Inited = true;
            Log.Init("debugdevelop.log");
            Log.Line($"Logging Started");

            foreach (var x in WeaponDefinitions)
            {
                foreach (var ammo in x.Ammos)
                {
                    var ae = ammo.AreaEffect;
                    var areaRadius = ae.AreaEffectRadius;
                    var detonateRadius = ae.Detonation.DetonationRadius;
                    var fragments = ammo.Shrapnel.Fragments > 0 ? ammo.Shrapnel.Fragments : 1;
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
            }
            foreach (var weaponDef in WeaponDefinitions)
            {
                foreach (var mount in weaponDef.Assignments.MountPoints)
                {
                    var subTypeId = mount.SubtypeId;
                    var muzzlePartId = mount.MuzzlePartId;
                    var azimuthPartId = mount.AzimuthPartId;
                    var elevationPartId = mount.ElevationPartId;

                    var extraInfo = new MyTuple<string, string, string> { Item1 = weaponDef.HardPoint.WeaponName, Item2 = azimuthPartId, Item3 = elevationPartId};

                    if (!_turretDefinitions.ContainsKey(subTypeId))
                    {
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
                var firstWeapon = true;

                foreach (var wepDef in weapons)
                {
                    if (wepDef.HardPoint.Ai.TurretAttached)
                        hasTurret = true;

                    foreach (var def in AllDefinitions)
                    {

                        MyDefinitionId defid;
                        var matchingDef = def.Id.SubtypeName == tDef.Key || (ReplaceVanilla && VanillaCoreIds.TryGetValue(MyStringHash.GetOrCompute(tDef.Key), out defid) && defid == def.Id);

                        if (matchingDef)
                        {
                            if (def is MyWeaponBlockDefinition)
                            {
                                if (firstWeapon)
                                    ((MyWeaponBlockDefinition)def).InventoryMaxVolume = 0;

                                ((MyWeaponBlockDefinition)def).InventoryMaxVolume += wepDef.HardPoint.HardWare.InventorySize;
                            }
                            else if(def is MyConveyorSorterDefinition)
                            {
                                if (firstWeapon)
                                    ((MyConveyorSorterDefinition)def).InventorySize = Vector3.Zero;

                                var size = Math.Pow(wepDef.HardPoint.HardWare.InventorySize, 1d / 3d);

                                ((MyConveyorSorterDefinition)def).InventorySize += new Vector3(size, size, size);
                            }

                            firstWeapon = false;
                        }

                        for (int i = 0; i < wepDef.Assignments.MountPoints.Length; i++)
                        {
                            if (matchingDef && def is MyLargeTurretBaseDefinition && (VanillaSubpartNames.Contains(wepDef.Assignments.MountPoints[i].AzimuthPartId) || VanillaSubpartNames.Contains(wepDef.Assignments.MountPoints[i].ElevationPartId)))
                            {
                                var gunDef = (MyLargeTurretBaseDefinition)def;
                                var blockDefs = wepDef.HardPoint.HardWare;

                                gunDef.MinAzimuthDegrees = blockDefs.MinAzimuth;
                                gunDef.MaxAzimuthDegrees = blockDefs.MaxAzimuth;
                                gunDef.MinElevationDegrees = blockDefs.MinElevation;
                                gunDef.MaxElevationDegrees = blockDefs.MaxElevation;
                                gunDef.RotationSpeed = blockDefs.RotateRate / 60;
                                gunDef.ElevationSpeed = blockDefs.ElevateRate / 60;
                                gunDef.AiEnabled = false;
                            }
                        }
                    }
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
