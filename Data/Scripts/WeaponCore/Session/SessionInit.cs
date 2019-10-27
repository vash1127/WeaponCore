using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ObjectBuilders;
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

            MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID, ReceivedPacket);

            if (!DedicatedServer && IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);

            MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest += PlayerConnected;

            Session.Player.Character.ControllerInfo.ControlReleased += PlayerControlReleased;
            Session.Player.Character.ControllerInfo.ControlAcquired += PlayerControlAcquired;

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

            if (TargetGps == null)
            {
                TargetGps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.MaxValue, true, true);
                MyAPIGateway.Session.GPS.AddLocalGps(TargetGps);
                MyVisualScriptLogicProvider.SetGPSColor(TargetGps.Name, Color.Yellow);
            }

            if (GridsUpdated) CheckDirtyGrids();
        }

        internal void Init()
        {
            if (Inited) return;
            Inited = true;
            Log.Init("debugdevelop.log");
            Log.Line($"Logging Started");
            HeatEmissives = CreateHeatEmissive();
            
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
            foreach (var weaponDef in _weaponDefinitions)
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
                            if (def.Id.SubtypeName == subTypeId && def is MyLargeTurretBaseDefinition)
                            {
                                var gunDef = (MyLargeTurretBaseDefinition)def;
                                var blockDefs = weaponDef.HardPoint.Block;
                                gunDef.MinAzimuthDegrees = blockDefs.MinAzimuth;
                                gunDef.MaxAzimuthDegrees = blockDefs.MaxAzimuth;
                                gunDef.MinElevationDegrees = blockDefs.MinElevation;
                                gunDef.MaxElevationDegrees = blockDefs.MaxElevation;
                                gunDef.RotationSpeed = (float)blockDefs.RotateRate;
                                gunDef.ElevationSpeed = (float)blockDefs.ElevateRate;
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

                WeaponPlatforms[subTypeIdHash] =  new WeaponStructure(this, tDef, _subTypeIdToWeaponDefs[tDef.Key]);
            }
            for (int i = 0; i < Projectiles.Wait.Length; i++)
            {
                Projectiles.EntityPool[i] = new EntityPool<MyEntity>[ModelCount];
                for (int j = 0; j < ModelCount; j++)
                    Projectiles.EntityPool[i][j] = new EntityPool<MyEntity>(0, ModelIdToName[j], WeaponCore.Projectiles.Projectiles.EntityActivator);
            }
        }

        internal void FixPrefabs()
        {
            var sMissileBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallMissileLauncher"));
            var rMissileBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallRocketLauncherReload"));
            var sGatBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallGatlingGun"));
            var gatBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "LargeGatlingTurret"));
            var lSGatBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallGatlingTurret"));
            var missileBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "LargeMissileTurret"));
            var interBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "LargeInteriorTurret"));

            foreach (var definition in MyDefinitionManager.Static.GetPrefabDefinitions())
            {
                for (int j = 0; j < definition.Value.CubeGrids.Length; j++)
                {
                    for (int i = 0; i < definition.Value.CubeGrids[j].CubeBlocks.Count; i++)
                    {
                        try
                        {
                            switch (definition.Value.CubeGrids[j].CubeBlocks[i].TypeId.ToString())
                            {
                                case "MyObjectBuilder_SmallMissileLauncher":
                                    if (string.IsNullOrEmpty(definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId
                                        .String))
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newSMissileOB = (MyObjectBuilder_CubeBlock)sMissileBuilder.Clone();
                                        newSMissileOB.EntityId = 0;
                                        newSMissileOB.BlockOrientation = origOB.BlockOrientation;
                                        newSMissileOB.Min = origOB.Min;
                                        newSMissileOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newSMissileOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newSMissileOB;
                                    }
                                    break;

                                case "MyObjectBuilder_SmallMissileLauncherReload":
                                    if (definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId.String == "SmallRocketLauncherReload")
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newSMissileOB = (MyObjectBuilder_CubeBlock)rMissileBuilder.Clone();
                                        newSMissileOB.EntityId = 0;
                                        newSMissileOB.BlockOrientation = origOB.BlockOrientation;
                                        newSMissileOB.Min = origOB.Min;
                                        newSMissileOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newSMissileOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newSMissileOB;
                                    }
                                    break;

                                case "MyObjectBuilder_SmallGatlingGun":
                                    if (string.IsNullOrEmpty(definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId
                                        .String))
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newSGatOB = (MyObjectBuilder_CubeBlock)sGatBuilder.Clone();
                                        newSGatOB.EntityId = 0;
                                        newSGatOB.BlockOrientation = origOB.BlockOrientation;
                                        newSGatOB.Min = origOB.Min;
                                        newSGatOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newSGatOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newSGatOB;
                                    }

                                    break;

                                case "MyObjectBuilder_LargeGatlingTurret":
                                    if (string.IsNullOrEmpty(definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId
                                        .String))
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newGatOB = (MyObjectBuilder_CubeBlock)gatBuilder.Clone();
                                        newGatOB.EntityId = 0;
                                        newGatOB.BlockOrientation = origOB.BlockOrientation;
                                        newGatOB.Min = origOB.Min;
                                        newGatOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newGatOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newGatOB;
                                    }
                                    else if (definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId.String ==
                                             "SmallGatlingTurret")
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newGatOB = (MyObjectBuilder_CubeBlock)lSGatBuilder.Clone();
                                        newGatOB.EntityId = 0;
                                        newGatOB.BlockOrientation = origOB.BlockOrientation;
                                        newGatOB.Min = origOB.Min;
                                        newGatOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newGatOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newGatOB;
                                    }

                                    break;

                                case "MyObjectBuilder_LargeMissileTurret":
                                    if (string.IsNullOrEmpty(definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId
                                        .String))
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newMissileOB = (MyObjectBuilder_CubeBlock)missileBuilder.Clone();
                                        newMissileOB.EntityId = 0;
                                        newMissileOB.BlockOrientation = origOB.BlockOrientation;
                                        newMissileOB.Min = origOB.Min;
                                        newMissileOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newMissileOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newMissileOB;
                                    }

                                    break;

                                case "MyObjectBuilder_InteriorTurret":
                                    if (definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId.String ==
                                        "LargeInteriorTurret")
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newInteriorOB = (MyObjectBuilder_CubeBlock)interBuilder.Clone();
                                        newInteriorOB.EntityId = 0;
                                        newInteriorOB.BlockOrientation = origOB.BlockOrientation;
                                        newInteriorOB.Min = origOB.Min;
                                        newInteriorOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newInteriorOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newInteriorOB;
                                    }

                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            //bad prefab xml
                        }
                    }
                }
            }
            foreach (var definition in MyDefinitionManager.Static.GetSpawnGroupDefinitions())
            {
                try
                {
                    definition.ReloadPrefabs();
                }
                catch (Exception e)
                {
                    //bad prefab xml
                }
            }
        }
    }
}
