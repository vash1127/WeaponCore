using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
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
    }
}
