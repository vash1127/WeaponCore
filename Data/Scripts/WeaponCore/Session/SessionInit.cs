using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using WeaponCore.Settings;
using WeaponCore.Support;
using static WeaponCore.Support.PartDefinition.HardPointDef.HardwareDef.HardwareType;
namespace WeaponCore
{
    public partial class Session
    {
        private void BeforeStartInit()
        {
            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsServer = MyAPIGateway.Multiplayer.IsServer;
            DedicatedServer = MyAPIGateway.Utilities.IsDedicated;
            IsCreative = MyAPIGateway.Session.CreativeMode || MyAPIGateway.Session.SessionSettings.InfiniteAmmo;
            IsClient = !IsServer && !DedicatedServer && MpActive;
            HandlesInput = !IsServer || IsServer && !DedicatedServer;
            if (IsServer || DedicatedServer)
                MyAPIGateway.Multiplayer.RegisterMessageHandler(ServerPacketId, ProccessServerPacket);
            else
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(ClientPacketId, ClientReceivedPacket);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(StringPacketId, StringReceived);
            }

            if (DamageHandler)
                Session.DamageSystem.RegisterBeforeDamageHandler(int.MinValue, BeforeDamageHandler);

            if (IsServer)
            {
                MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisconnected;
                MyVisualScriptLogicProvider.PlayerRespawnRequest += PlayerConnected;
            }

            if (HandlesInput)
                MyAPIGateway.Utilities.MessageEntered += ChatMessageSet;

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
            CheckDirtyGridInfos();

            ApiServer.Load();
            GenerateButtonMap();
            Settings = new CoreSettings(this);
            LocalVersion = ModContext.ModId == "WeaponCore";
            CounterKeenLogMessage();
            if (!CompsToStart.IsEmpty)
                StartComps();

        }

        internal void GenerateButtonMap()
        {
            var ieKeys = Enum.GetValues(typeof(MyKeys)).Cast<MyKeys>();
            var keys = ieKeys as MyKeys[] ?? ieKeys.ToArray();
            var kLength = keys.Length;
            for (int i = 0; i < kLength; i++)
            {
                var key = keys[i];
                 KeyMap[key.ToString()] = key;
            }

            var ieButtons = Enum.GetValues(typeof(MyMouseButtonsEnum)).Cast<MyMouseButtonsEnum>();
            var buttons = ieButtons as MyMouseButtonsEnum[] ?? ieButtons.ToArray();

            var bLength = buttons.Length;
            for (int i = 0; i < bLength; i++)
            {
                var button = buttons[i];
                MouseMap[button.ToString()] = button;
            }
        }

        internal void Init()
        {
            if (Inited) return;
            Inited = true;
            Log.Init("debug", this);
            Log.Init("perf", this, false);
            Log.Init("stats", this, false);
            Log.Init("net", this, false);
            Log.Init("report", this, false);
            Log.Init("combat", this, false);
            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsServer = MyAPIGateway.Multiplayer.IsServer;
            DedicatedServer = MyAPIGateway.Utilities.IsDedicated;
            IsCreative = MyAPIGateway.Session.CreativeMode;
            IsClient = !IsServer && !DedicatedServer && MpActive;
            HandlesInput = !IsServer || IsServer && !DedicatedServer;

            foreach (var x in PartDefinitions)
            {
                if (x.HardPoint.HardWare.Type == BlockWeapon || x.HardPoint.HardWare.Type == HandWeapon)
                {
                    foreach (var ammo in x.Ammos)
                    {
                        var ae = ammo.AreaEffect;
                        var areaRadius = ae.Base.Radius > 0 ? ae.Base.Radius : ae.AreaEffectRadius;
                        var detonateRadius = ae.Detonation.DetonationRadius;
                        var fragments = ammo.Fragment.Fragments > 0 ? ammo.Fragment.Fragments : 1;
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
            }
            foreach (var x in PartDefinitions)
            {
                for (int i = 0; i < x.Assignments.MountPoints.Length; i++)
                {
                    var mount = x.Assignments.MountPoints[i];
                    var subTypeId = mount.SubtypeId;
                    var muzzleOrRootPartId = mount.MuzzlePartId;
                    var muzzlePartOrRoot = !string.IsNullOrEmpty(muzzleOrRootPartId) ? muzzleOrRootPartId : x.HardPoint.PartName + $" {i}";
                    var azimuthPartId = mount.AzimuthPartId;
                    var elevationPartId = mount.ElevationPartId;

                    var extraInfo = new MyTuple<string, string, string> { Item1 = x.HardPoint.PartName, Item2 = azimuthPartId, Item3 = elevationPartId };

                    if (!_subTypeMaps.ContainsKey(subTypeId)) {

                        _subTypeMaps[subTypeId] = new Dictionary<string, MyTuple<string, string, string>> {[muzzlePartOrRoot] = extraInfo};

                        _subTypeIdToPartDefs[subTypeId] = new List<PartDefinition> { x };
                    }
                    else {
                        _subTypeMaps[subTypeId][muzzlePartOrRoot] = extraInfo;
                        _subTypeIdToPartDefs[subTypeId].Add(x);
                    }
                }
            }

            foreach (var subTypeMap in _subTypeMaps)
            {
                var subTypeIdHash = MyStringHash.GetOrCompute(subTypeMap.Key);
                SubTypeIdHashMap[subTypeMap.Key] = subTypeIdHash;

                AreaRestriction areaRestriction;
                if (AreaRestrictions.ContainsKey(subTypeIdHash))
                {
                    areaRestriction = AreaRestrictions[subTypeIdHash];
                } else
                {
                    areaRestriction = new AreaRestriction();
                    AreaRestrictions[subTypeIdHash] = areaRestriction;
                }

                var parts = _subTypeIdToPartDefs[subTypeMap.Key];
                var isTurret = false;
                var isArmor = false;
                var isUpgrade = false;
                var isWeapon = false;
                var isRifle = false;
                var isPhantom = false;
                var firstWeapon = true;
                string modPath = null;
                foreach (var partDef in parts)
                {
                    try {
                        modPath = partDef.ModPath;
                        if (partDef.HardPoint.Ai.TurretAttached)
                            isTurret = true;

                        if (partDef.HardPoint.HardWare.Type == BlockWeapon || partDef.HardPoint.HardWare.Type == HandWeapon)
                        {
                            isWeapon = true;
                            if (partDef.HardPoint.HardWare.Type == HandWeapon)
                                isRifle = true;
                        }

                        if (partDef.HardPoint.HardWare.Type == Upgrade)
                            isUpgrade = true;

                        if (partDef.HardPoint.HardWare.Type == Phantom)
                            isPhantom = true;

                        if (isUpgrade && !isWeapon && !isPhantom) {
                            DamageHandler = true;
                            isArmor = true;
                        }
                        foreach (var def in AllDefinitions) {
                            MyDefinitionId defid;
                            var matchingDef = def.Id.SubtypeName == subTypeMap.Key || (ReplaceVanilla && VanillaCoreIds.TryGetValue(MyStringHash.GetOrCompute(subTypeMap.Key), out defid) && defid == def.Id);
                            if (matchingDef)
                            {
                                if (partDef.HardPoint.Other.RestrictionRadius > 0)
                                {
                                    if (partDef.HardPoint.Other.CheckForAnyWeapon && !areaRestriction.CheckForAnyWeapon)
                                    {
                                        areaRestriction.CheckForAnyWeapon = true;
                                    }
                                    if (partDef.HardPoint.Other.CheckInflatedBox)
                                    {
                                        if (areaRestriction.RestrictionBoxInflation < partDef.HardPoint.Other.RestrictionRadius)
                                        {
                                            areaRestriction.RestrictionBoxInflation = partDef.HardPoint.Other.RestrictionRadius;
                                        }
                                    }
                                    else
                                    {
                                        if (areaRestriction.RestrictionRadius < partDef.HardPoint.Other.RestrictionRadius)
                                        {
                                            areaRestriction.RestrictionRadius = partDef.HardPoint.Other.RestrictionRadius;
                                        }
                                    }
                                }

                                WeaponCoreDefs[subTypeMap.Key] = def.Id;
                                var designator = false;

                                for (int i = 0; i < partDef.Assignments.MountPoints.Length; i++)
                                {
                                    if (partDef.Assignments.MountPoints[i].MuzzlePartId == "Designator")
                                    {
                                        designator = true;
                                        break;
                                    }
                                }

                                if (!designator)
                                {

                                    var wepBlockDef = def as MyWeaponBlockDefinition;
                                    if (wepBlockDef != null)
                                    {
                                        if (firstWeapon)
                                            wepBlockDef.InventoryMaxVolume = 0;

                                        wepBlockDef.InventoryMaxVolume += partDef.HardPoint.HardWare.InventorySize;

                                        var weaponCsDef = MyDefinitionManager.Static.GetWeaponDefinition(wepBlockDef.WeaponDefinitionId);

                                        if (weaponCsDef.WeaponAmmoDatas[0] == null)
                                        {
                                            Log.Line($"WeaponAmmoData is null, check the Ammo definition for {subTypeMap.Key}");
                                        }
                                        weaponCsDef.WeaponAmmoDatas[0].RateOfFire = partDef.HardPoint.Loading.RateOfFire;

                                        weaponCsDef.WeaponAmmoDatas[0].ShotsInBurst = partDef.HardPoint.Loading.ShotsInBurst;
                                    }
                                    else if (def is MyConveyorSorterDefinition)
                                    {
                                        if (firstWeapon)
                                            ((MyConveyorSorterDefinition)def).InventorySize = Vector3.Zero;

                                        var size = Math.Pow(partDef.HardPoint.HardWare.InventorySize, 1d / 3d);

                                        ((MyConveyorSorterDefinition)def).InventorySize += new Vector3(size, size, size);
                                    }

                                    firstWeapon = false;

                                    for (int i = 0; i < partDef.Assignments.MountPoints.Length; i++)
                                    {

                                        var az = !string.IsNullOrEmpty(partDef.Assignments.MountPoints[i].AzimuthPartId) ? partDef.Assignments.MountPoints[i].AzimuthPartId : "MissileTurretBase1";
                                        var el = !string.IsNullOrEmpty(partDef.Assignments.MountPoints[i].ElevationPartId) ? partDef.Assignments.MountPoints[i].ElevationPartId : "MissileTurretBarrels";

                                        if (def is MyLargeTurretBaseDefinition && (VanillaSubpartNames.Contains(az) || VanillaSubpartNames.Contains(el)))
                                        {

                                            var gunDef = (MyLargeTurretBaseDefinition)def;
                                            var blockDefs = partDef.HardPoint.HardWare;
                                            gunDef.MinAzimuthDegrees = blockDefs.MinAzimuth;
                                            gunDef.MaxAzimuthDegrees = blockDefs.MaxAzimuth;
                                            gunDef.MinElevationDegrees = blockDefs.MinElevation;
                                            gunDef.MaxElevationDegrees = blockDefs.MaxElevation;
                                            gunDef.RotationSpeed = blockDefs.RotateRate / 60;
                                            gunDef.ElevationSpeed = blockDefs.ElevateRate / 60;
                                            gunDef.AiEnabled = false;
                                            gunDef.IdleRotation = false;
                                        }

                                        var cubeDef = def as MyCubeBlockDefinition;
                                        if (cubeDef != null)
                                        {
                                            for (int x = 0; x < partDef.Assignments.MountPoints.Length; x++)
                                            {
                                                var mp = partDef.Assignments.MountPoints[x];
                                                if (mp.SubtypeId == def.Id.SubtypeName)
                                                {
                                                    cubeDef.GeneralDamageMultiplier = mp.DurabilityMod > 0 ? mp.DurabilityMod : cubeDef.CubeSize == MyCubeSize.Large ? 0.25f : 0.05f;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                    }
                    catch (Exception e)
                    {
                        Log.Line($"Failed to load {partDef.HardPoint.PartName}");
                    }
                }

                MyDefinitionId defId;
                if (WeaponCoreDefs.TryGetValue(subTypeMap.Key, out defId))
                {
                    if (isWeapon)
                    {
                        if (isTurret)
                            WeaponCoreTurretBlockDefs.Add(defId);
                        else
                            WeaponCoreFixedBlockDefs.Add(defId);
                    }
                    else if (isUpgrade)
                    {
                        WeaponCoreUpgradeBlockDefs.Add(defId);
                    }
                    else if (isArmor)
                        WeaponCoreArmorBlockDefs.Add(defId);
                    else if (isPhantom)
                        WeaponCorePhantomDefs.Add(defId);
                    else if (isRifle)
                        WeaponCoreRifleDefs.Add(defId);
                }
                PartPlatforms[defId] = new CoreStructure(this, subTypeMap, parts, modPath);
            }

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlHandler;

        }

    }
}
