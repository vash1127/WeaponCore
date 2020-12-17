using System;
using System.Collections.Generic;
using System.Linq;
using Jakaria;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using VRage;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using WeaponCore.Settings;
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
            CheckDirtyGrids();

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

            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsServer = MyAPIGateway.Multiplayer.IsServer;
            DedicatedServer = MyAPIGateway.Utilities.IsDedicated;
            IsCreative = MyAPIGateway.Session.CreativeMode;
            IsClient = !IsServer && !DedicatedServer && MpActive;
            HandlesInput = !IsServer || IsServer && !DedicatedServer;

            foreach (var x in WeaponDefinitions)
            {
                foreach (var ammo in x.Ammos)
                {
                    var ae = ammo.AreaEffect;
                    var areaRadius = ae.Base.Radius > 0 ? ae.Base.Radius : ae.AreaEffectRadius;
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

                WeaponAreaRestriction areaRestriction;
                if (this.WeaponAreaRestrictions.ContainsKey(subTypeIdHash))
                {
                    areaRestriction = this.WeaponAreaRestrictions[subTypeIdHash];
                } else
                {
                    areaRestriction = new WeaponAreaRestriction();
                    WeaponAreaRestrictions[subTypeIdHash] = areaRestriction;
                }

                var weapons = _subTypeIdToWeaponDefs[tDef.Key];
                var hasTurret = false;
                var firstWeapon = true;
                string modPath = null;
                foreach (var wepDef in weapons)
                {
                    try {
                        modPath = wepDef.ModPath;
                        if (wepDef.HardPoint.Ai.TurretAttached)
                            hasTurret = true;

                        if (wepDef.HardPoint.HardWare.Armor != WeaponDefinition.HardPointDef.HardwareDef.ArmorState.IsWeapon)
                            DamageHandler = true;

                        foreach (var def in AllDefinitions) {
                            MyDefinitionId defid;
                            var matchingDef = def.Id.SubtypeName == tDef.Key || (ReplaceVanilla && VanillaCoreIds.TryGetValue(MyStringHash.GetOrCompute(tDef.Key), out defid) && defid == def.Id);
                            if (matchingDef)
                            {
                                if (wepDef.HardPoint.Other.RestrictionRadius > 0)
                                {
                                    if (wepDef.HardPoint.Other.CheckForAnyWeapon && !areaRestriction.CheckForAnyWeapon)
                                    {
                                        areaRestriction.CheckForAnyWeapon = true;
                                    }
                                    if (wepDef.HardPoint.Other.CheckInflatedBox)
                                    {
                                        if (areaRestriction.RestrictionBoxInflation < wepDef.HardPoint.Other.RestrictionRadius)
                                        {
                                            areaRestriction.RestrictionBoxInflation = wepDef.HardPoint.Other.RestrictionRadius;
                                        }
                                    }
                                    else
                                    {
                                        if (areaRestriction.RestrictionRadius < wepDef.HardPoint.Other.RestrictionRadius)
                                        {
                                            areaRestriction.RestrictionRadius = wepDef.HardPoint.Other.RestrictionRadius;
                                        }
                                    }
                                }

                                WeaponCoreBlockDefs[tDef.Key] = def.Id;
                                var designator = false;

                                for (int i = 0; i < wepDef.Assignments.MountPoints.Length; i++)
                                {
                                    if (wepDef.Assignments.MountPoints[i].MuzzlePartId == "Designator")
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

                                        wepBlockDef.InventoryMaxVolume += wepDef.HardPoint.HardWare.InventorySize;

                                        var weaponCsDef = MyDefinitionManager.Static.GetWeaponDefinition(wepBlockDef.WeaponDefinitionId);

                                        if (weaponCsDef.WeaponAmmoDatas[0] == null)
                                        {
                                            Log.Line($"WeaponAmmoData is null, check the Ammo definition for {tDef.Key}");
                                        }
                                        weaponCsDef.WeaponAmmoDatas[0].RateOfFire = wepDef.HardPoint.Loading.RateOfFire;

                                        weaponCsDef.WeaponAmmoDatas[0].ShotsInBurst = wepDef.HardPoint.Loading.ShotsInBurst;
                                    }
                                    else if (def is MyConveyorSorterDefinition)
                                    {
                                        if (firstWeapon)
                                            ((MyConveyorSorterDefinition)def).InventorySize = Vector3.Zero;

                                        var size = Math.Pow(wepDef.HardPoint.HardWare.InventorySize, 1d / 3d);

                                        ((MyConveyorSorterDefinition)def).InventorySize += new Vector3(size, size, size);
                                    }

                                    firstWeapon = false;

                                    for (int i = 0; i < wepDef.Assignments.MountPoints.Length; i++)
                                    {

                                        var az = !string.IsNullOrEmpty(wepDef.Assignments.MountPoints[i].AzimuthPartId) ? wepDef.Assignments.MountPoints[i].AzimuthPartId : "MissileTurretBase1";
                                        var el = !string.IsNullOrEmpty(wepDef.Assignments.MountPoints[i].ElevationPartId) ? wepDef.Assignments.MountPoints[i].ElevationPartId : "MissileTurretBarrels";

                                        if (def is MyLargeTurretBaseDefinition && (VanillaSubpartNames.Contains(az) || VanillaSubpartNames.Contains(el)))
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
                                            gunDef.IdleRotation = false;
                                        }

                                        var cubeDef = def as MyCubeBlockDefinition;
                                        if (cubeDef != null)
                                        {
                                            for (int x = 0; x < wepDef.Assignments.MountPoints.Length; x++)
                                            {
                                                var mp = wepDef.Assignments.MountPoints[x];
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
                        Log.Line($"Failed to load {wepDef.HardPoint.WeaponName}");
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
                WeaponPlatforms[defId] = new WeaponStructure(this, tDef, weapons, modPath);
            }


            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlHandler;
        }

    }
}
