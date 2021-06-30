﻿using System;
using System.Collections.Generic;
using System.Linq;
using CoreSystems.Platform;
using CoreSystems.Settings;
using CoreSystems.Support;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Input;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Settings.CoreSettings.ServerSettings;
using static CoreSystems.Support.WeaponDefinition.HardPointDef.HardwareDef.HardwareType;
namespace CoreSystems
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
            IsHost = IsServer && !DedicatedServer && MpActive;
            MpServer = IsHost || DedicatedServer;

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
            TargetGps = MyAPIGateway.Session.GPS.Create("WEAPONCORE", "", Vector3D.MaxValue, true);
            CheckDirtyGridInfos();

            ApiServer.Load();
            GenerateButtonMap();
            Settings = new CoreSettings(this);
            CounterKeenLogMessage();

            if (IsServer || DedicatedServer)
            {
                foreach (var platform in PartPlatforms)
                {
                    var core = platform.Value;
                    if (core.StructureType != CoreStructure.StructureTypes.Weapon) continue;
                    foreach (var system in core.PartSystems)
                    {
                        var part = (system.Value as WeaponSystem);
                        for (int i = 0; i < part.AmmoTypes.Length; i++)
                        {
                            var ammo = part.AmmoTypes[i];
                            AmmoModifer ammoModifer;
                            if (AmmoValuesMap.TryGetValue(ammo.AmmoDef, out ammoModifer))
                            {
                                ammo.AmmoDef.Const = new AmmoConstants(ammo, part.Values, part.Session, part, i);
                            }
                        }
                    }
                }
            }

            if (ShieldMod && !ShieldApiLoaded && SApi.Load())
            {
                ShieldApiLoaded = true;
                ShieldHash = MyStringHash.GetOrCompute("DefenseShield");
            }

            if (WaterMod && !WaterApiLoaded)
            {
                WaterApiLoaded = true;
                WApi.Load();
                WaterHash = MyStringHash.GetOrCompute("Water");
            }

            if (!CompsToStart.IsEmpty)
                StartComps();

            EarlyInitControls(this);
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
            IsHost = IsServer && !DedicatedServer && MpActive;
            MpServer = IsHost || DedicatedServer;
            LocalVersion = ModContext.ModId == "CoreSystems";

            CompileWeaponStructures();
            CompileUpgradeStructures();
            CompileSupportStructures();

            AssignPowerPriorities();


            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlHandler;
        }

        private readonly List<CoreStructure> _tmpStructureSorting = new List<CoreStructure>();
        private void AssignPowerPriorities()
        {
            _tmpStructureSorting.AddRange(PartPlatforms.Values);
            ShellSort(_tmpStructureSorting);

            for (int i = _tmpStructureSorting.Count - 1; i >= 0; i--)
            {
                var t = _tmpStructureSorting[i];

                var powerUser = t.ApproximatePeakPowerCombined >= 2;

                if (!powerUser) {
                    PowerGroups.Add(t, 0);
                    _tmpStructureSorting.RemoveAtFast(i);
                }
            }

            var total = _tmpStructureSorting.Count;
            var condition1 = total * 0.5f;
            var condition2 = total * 0.75f;

            for (int i = 0; i < _tmpStructureSorting.Count; i++)
            {
                var t = _tmpStructureSorting[i];

                if (i < condition1)
                    PowerGroups.Add(t, 0);
                else if (i < condition2)
                    PowerGroups.Add(t, 1);
                else
                    PowerGroups.Add(t, 2);
            }
            /*
            foreach (var g in PowerGroups)
            {
                Log.Line($"ChargePowerRequired: {g.Key.ApproximatePeakPowerCombined} - assingedPowerGroup:{g.Value}");
            }
            */
            _tmpStructureSorting.Clear();
        }

        static void ShellSort(List<CoreStructure> list)
        {
            int length = list.Count;

            for (int h = length / 2; h > 0; h /= 2)
            {
                for (int i = h; i < length; i += 1)
                {
                    var tempValue = list[i];
                    var temp = tempValue.ApproximatePeakPowerCombined;

                    int j;
                    for (j = i; j >= h && list[j - h].ApproximatePeakPowerCombined > temp; j -= h)
                    {
                        list[j] = list[j - h];
                    }

                    list[j] = tempValue;
                }
            }
        }

        private void CompileWeaponStructures()
        {
            foreach (var x in WeaponDefinitions)
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
            foreach (var x in WeaponDefinitions)
            {
                for (int i = 0; i < x.Assignments.MountPoints.Length; i++)
                {
                    var mount = x.Assignments.MountPoints[i];
                    var subTypeId = mount.SubtypeId;
                    var muzzlePartId = mount.MuzzlePartId;
                    var weapon = x.HardPoint.HardWare.Type == BlockWeapon || x.HardPoint.HardWare.Type == HandWeapon || x.HardPoint.HardWare.Type == Phantom;
                    var partAttachmentId = weapon ? muzzlePartId : x.HardPoint.PartName + $" {i}";
                    var azimuthPartId = mount.AzimuthPartId;
                    var elevationPartId = mount.ElevationPartId;
                    var spinPartId = mount.SpinPartId;
                    var phantomModel = mount.PhantomModel;

                    var extraInfo = new MyTuple<string, string, string, string> { Item1 = x.HardPoint.PartName, Item2 = azimuthPartId, Item3 = elevationPartId, Item4 = spinPartId };

                    if (!_subTypeMaps.ContainsKey(subTypeId))
                    {
                        _subTypeMaps[subTypeId] = new Dictionary<string, MyTuple<string, string, string, string>> { [partAttachmentId] = extraInfo };
                        _subTypeIdWeaponDefs[subTypeId] = new List<WeaponDefinition> { x };
                    }
                    else
                    {
                        _subTypeMaps[subTypeId][partAttachmentId] = extraInfo;
                        _subTypeIdWeaponDefs[subTypeId].Add(x);
                    }

                    if (!string.IsNullOrEmpty(phantomModel))
                        ModelMaps[subTypeId] = x.ModPath + phantomModel;

                    AmmoMaps[subTypeId] = new Dictionary<string, WeaponSystem.AmmoType>();

                    if (x.HardPoint.HardWare.Type == Phantom || x.HardPoint.HardWare.CriticalReaction.Enable)
                    {
                        PhantomDatabase[subTypeId] = new Dictionary<long, Weapon.WeaponComponent>();
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
                }
                else
                {
                    areaRestriction = new AreaRestriction();
                    AreaRestrictions[subTypeIdHash] = areaRestriction;
                }

                var parts = _subTypeIdWeaponDefs[subTypeMap.Key];

                var firstWeapon = true;
                string modPath = null;

                foreach (var partDef in parts)
                {
                    try
                    {
                        modPath = partDef.ModPath;
                        if (partDef.HardPoint.HardWare.Type != Phantom)
                        {
                            foreach (var def in AllDefinitions)
                            {

                                MyDefinitionId defid;
                                var matchingDef = def.Id.SubtypeName == subTypeMap.Key || (ReplaceVanilla && VanillaCoreIds.TryGetValue(MyStringHash.GetOrCompute(subTypeMap.Key), out defid) && defid == def.Id);

                                if (matchingDef)
                                {

                                    if (partDef.HardPoint.Other.RestrictionRadius > 0)
                                    {

                                        if (partDef.HardPoint.Other.CheckForAnyWeapon && !areaRestriction.CheckForAnyPart)
                                        {
                                            areaRestriction.CheckForAnyPart = true;
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
                                                areaRestriction.RestrictionRadius = partDef.HardPoint.Other.RestrictionRadius;
                                        }
                                    }
                                    CoreSystemsDefs[subTypeMap.Key] = def.Id;
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
                                                Log.Line($"WeaponAmmoData is null, check the ProtoWeaponAmmo definition for {subTypeMap.Key}");
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
                        else
                        {
                            CoreSystemsDefs[subTypeMap.Key] = new MyDefinitionId(MyObjectBuilderType.Invalid, MyStringHash.GetOrCompute(subTypeMap.Key));
                        }
                    }
                    catch (Exception e) { Log.Line($"Failed to load {partDef.HardPoint.PartName}", null, true); }
                }

                MyDefinitionId defId;
                if (CoreSystemsDefs.TryGetValue(subTypeMap.Key, out defId))
                {
                    var w = new WeaponStructure(this, subTypeMap, parts, modPath);
                    PartPlatforms[defId] = w;
                    switch (w.StructureType)
                    {
                        case CoreStructure.StructureTypes.Weapon:
                            if (w.HasTurret && w.EntityType == CoreStructure.EnittyTypes.Block)
                                CoreSystemsTurretBlockDefs.Add(defId);
                            else if (w.EntityType == CoreStructure.EnittyTypes.Block)
                                CoreSystemsFixedBlockDefs.Add(defId);
                            else if (w.EntityType == CoreStructure.EnittyTypes.Rifle)
                                CoreSystemsRifleDefs.Add(defId);
                            else
                                CoreSystemsPhantomDefs.Add(defId);
                            break;
                    }
                }
            }
            _subTypeMaps.Clear();
            _subTypeIdWeaponDefs.Clear();
        }

        private void CompileSupportStructures()
        {
            foreach (var x in SupportDefinitions)
            {
                for (int i = 0; i < x.Assignments.MountPoints.Length; i++)
                {
                    var mount = x.Assignments.MountPoints[i];
                    var subTypeId = mount.SubtypeId;
                    var partAttachmentId = x.HardPoint.PartName + $" {i}";

                    var extraInfo = new MyTuple<string, string, string, string> { Item1 = x.HardPoint.PartName, Item2 = "None", Item3 = "None", Item4 = "None"};

                    if (!_subTypeMaps.ContainsKey(subTypeId))
                    {

                        _subTypeMaps[subTypeId] = new Dictionary<string, MyTuple<string, string, string, string>> { [partAttachmentId] = extraInfo };

                        _subTypeIdSupportDefs[subTypeId] = new List<SupportDefinition> { x };
                    }
                    else
                    {
                        _subTypeMaps[subTypeId][partAttachmentId] = extraInfo;
                        _subTypeIdSupportDefs[subTypeId].Add(x);
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
                }
                else
                {
                    areaRestriction = new AreaRestriction();
                    AreaRestrictions[subTypeIdHash] = areaRestriction;
                }

                var parts = _subTypeIdSupportDefs[subTypeMap.Key];
                var firstDef = true;
                string modPath = null;

                foreach (var partDef in parts)
                {

                    try
                    {
                        modPath = partDef.ModPath;
                        foreach (var def in AllDefinitions)
                        {

                            MyDefinitionId defid;
                            var matchingDef = def.Id.SubtypeName == subTypeMap.Key || (ReplaceVanilla && VanillaCoreIds.TryGetValue(MyStringHash.GetOrCompute(subTypeMap.Key), out defid) && defid == def.Id);

                            if (matchingDef)
                            {
                                if (partDef.HardPoint.Other.RestrictionRadius > 0)
                                {

                                    if (partDef.HardPoint.Other.CheckForAnySupport && !areaRestriction.CheckForAnyPart)
                                    {
                                        areaRestriction.CheckForAnyPart = true;
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
                                            areaRestriction.RestrictionRadius = partDef.HardPoint.Other.RestrictionRadius;
                                    }
                                }
                                CoreSystemsDefs[subTypeMap.Key] = def.Id;

                                if (def is MyConveyorSorterDefinition)
                                {
                                    if (firstDef)
                                        ((MyConveyorSorterDefinition)def).InventorySize = Vector3.Zero;

                                    var size = Math.Pow(partDef.HardPoint.HardWare.InventorySize, 1d / 3d);

                                    ((MyConveyorSorterDefinition)def).InventorySize += new Vector3(size, size, size);
                                }

                                firstDef = false;

                                for (int i = 0; i < partDef.Assignments.MountPoints.Length; i++)
                                {
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
                    catch (Exception e) { Log.Line($"Failed to load {partDef.HardPoint.PartName}", null, true); }
                }

                MyDefinitionId defId;
                if (CoreSystemsDefs.TryGetValue(subTypeMap.Key, out defId))
                {
                    var u = new SupportStructure(this, subTypeMap, parts, modPath);
                    PartPlatforms[defId] = u;
                    CoreSystemsSupportDefs.Add(defId);
                }
            }

            _subTypeMaps.Clear();
            _subTypeIdSupportDefs.Clear();
        }

        private void CompileUpgradeStructures()
        {
            foreach (var x in UpgradeDefinitions)
            {
                for (int i = 0; i < x.Assignments.MountPoints.Length; i++)
                {
                    var mount = x.Assignments.MountPoints[i];
                    var subTypeId = mount.SubtypeId;
                    var partAttachmentId = x.HardPoint.PartName + $" {i}";

                    var extraInfo = new MyTuple<string, string, string, string> { Item1 = x.HardPoint.PartName, Item2 = "None", Item3 = "None", Item4 = "None"};

                    if (!_subTypeMaps.ContainsKey(subTypeId))
                    {

                        _subTypeMaps[subTypeId] = new Dictionary<string, MyTuple<string, string, string, string>> { [partAttachmentId] = extraInfo };

                        _subTypeIdUpgradeDefs[subTypeId] = new List<UpgradeDefinition> { x };
                    }
                    else
                    {
                        _subTypeMaps[subTypeId][partAttachmentId] = extraInfo;
                        _subTypeIdUpgradeDefs[subTypeId].Add(x);
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
                }
                else
                {
                    areaRestriction = new AreaRestriction();
                    AreaRestrictions[subTypeIdHash] = areaRestriction;
                }

                var parts = _subTypeIdUpgradeDefs[subTypeMap.Key];
                var firstDef = true;
                string modPath = null;

                foreach (var partDef in parts)
                {

                    try
                    {
                        modPath = partDef.ModPath;
                        foreach (var def in AllDefinitions)
                        {

                            MyDefinitionId defid;
                            var matchingDef = def.Id.SubtypeName == subTypeMap.Key || (ReplaceVanilla && VanillaCoreIds.TryGetValue(MyStringHash.GetOrCompute(subTypeMap.Key), out defid) && defid == def.Id);

                            if (matchingDef)
                            {

                                if (partDef.HardPoint.Other.RestrictionRadius > 0)
                                {

                                    if (partDef.HardPoint.Other.CheckForAnySupport && !areaRestriction.CheckForAnyPart)
                                    {
                                        areaRestriction.CheckForAnyPart = true;
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
                                            areaRestriction.RestrictionRadius = partDef.HardPoint.Other.RestrictionRadius;
                                    }
                                }

                                CoreSystemsDefs[subTypeMap.Key] = def.Id;

                                if (def is MyConveyorSorterDefinition)
                                {
                                    if (firstDef)
                                        ((MyConveyorSorterDefinition)def).InventorySize = Vector3.Zero;

                                    var size = Math.Pow(partDef.HardPoint.HardWare.InventorySize, 1d / 3d);

                                    ((MyConveyorSorterDefinition)def).InventorySize += new Vector3(size, size, size);
                                }

                                firstDef = false;

                                for (int i = 0; i < partDef.Assignments.MountPoints.Length; i++)
                                {
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
                    catch (Exception e) { Log.Line($"Failed to load {partDef.HardPoint.PartName}", null, true); }
                }

                MyDefinitionId defId;
                if (CoreSystemsDefs.TryGetValue(subTypeMap.Key, out defId))
                {
                    var u = new UpgradeStructure(this, subTypeMap, parts, modPath);
                    PartPlatforms[defId] = u;
                    CoreSystemsUpgradeDefs.Add(defId);
                }
            }

            _subTypeMaps.Clear();
            _subTypeIdUpgradeDefs.Clear();
        }
    }
}
