using System;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void RequestDbUpdate()
        {
            using (MyGrid.Pin()) {
                if (MyGrid == null || MyGrid.MarkedForClose || !MyGrid.InScene)
                    return;

                var bigOwners = MyGrid.BigOwners;
                MyOwner = bigOwners == null || bigOwners.Count <= 0 ? 0 : MyOwner = bigOwners[0];
            }

            ScanInProgress = true;

            GridVolume = MyGrid.PositionComp.WorldVolume;
            ScanVolume = GridVolume;
            ScanVolume.Radius = MaxTargetingRange;
            Session.DbsToUpdate.Add(new DbScan {Ai = this, Version = Version});
            TargetsUpdatedTick = Session.Tick;
        }

        internal void Scan()
        {
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref ScanVolume, _possibleTargets);
            NearByEntitiesTmp = _possibleTargets.Count;

            foreach (var grid in PrevSubGrids)
                RemSubGrids.Add(grid);
            PrevSubGrids.Clear();
            for (int i = 0; i < NearByEntitiesTmp; i++) {

                var ent = _possibleTargets[i];
                using (ent.Pin()) {

                    if (ent is MyVoxelBase || ent.Physics == null || ent is MyFloatingObject || ent.MarkedForClose || !ent.InScene || ent.IsPreview || ent.Physics.IsPhantom) continue;

                    var grid = ent as MyCubeGrid;
                    if (grid != null && MyGrid.IsSameConstructAs(grid)) {
                        PrevSubGrids.Add(grid);
                        continue;
                    }

                    Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo;
                    if (!CreateEntInfo(ent, MyOwner, out entInfo))
                    {
                        continue;
                    }

                    switch (entInfo.Relationship) {
                        case MyRelationsBetweenPlayerAndBlock.Owner:
                        case MyRelationsBetweenPlayerAndBlock.FactionShare:
                        case MyRelationsBetweenPlayerAndBlock.Friends:
                            continue;
                    }

                    if (grid != null) {

                        FatMap fatMap;
                        if (!Session.GridToFatMap.TryGetValue(grid, out fatMap) || fatMap.Trash)
                            continue;

                        var allFat = fatMap.MyCubeBocks;
                        var fatCount = allFat.Count;

                        if (fatCount <= 0)
                            continue;

                        if (fatCount <= 20)  { // possible debris

                            var valid = false;
                            for (int j = 0; j < fatCount; j++) {
                                var fat = allFat[j];
                                if (fat is IMyTerminalBlock && fat.IsWorking) {
                                    valid = true;
                                    break;
                                }
                            }
                            if (!valid) continue;
                        }

                        int partCount;
                        GridAi targetAi;
                        if (Session.GridTargetingAIs.TryGetValue(grid, out targetAi)) {
                            targetAi.TargetAisTmp.Add(this);
                            TargetAisTmp.Add(targetAi);
                            partCount = targetAi.Construct.BlockCount;
                        }
                        else 
                            partCount = fatMap.MostBlocks;

                        NewEntities.Add(new DetectInfo(Session, ent, entInfo, partCount, fatCount));
                        ValidGrids.Add(ent);
                    }
                    else NewEntities.Add(new DetectInfo(Session, ent, entInfo, 1, 0));
                }
            }
            FinalizeTargetDb();
            SubGridDetect();
        }

        private void FinalizeTargetDb()
        {
            MyPlanetTmp = MyGamePruningStructure.GetClosestPlanet(ScanVolume.Center);
            ObstructionsTmp.Clear();
            StaticsInRangeTmp.Clear();
            for (int i = 0; i < NearByEntitiesTmp; i++) {

                var ent = _possibleTargets[i];
                var hasPhysics = ent.Physics != null;
                if (Session.ShieldApiLoaded && hasPhysics && !ent.Physics.Enabled && ent.Physics.IsPhantom && !ent.Render.CastShadows && ent.Render.Visible) {

                    long testId;
                    if (long.TryParse(ent.Name, out testId)) {

                        var shieldblock = Session.SApi.MatchEntToShieldFast(ent, false);
                        if (shieldblock != null)
                            NearByShieldsTmp.Add(new Shields { Id = testId, ShieldEnt = ent, ShieldBlock = (MyCubeBlock)shieldblock});
                    }
                }
                var voxel = ent as MyVoxelBase;
                var grid = ent as MyCubeGrid;

                var blockingThings = ent.Physics != null && grid != null || voxel != null && voxel == voxel.RootVoxel;
                if (!blockingThings || voxel != null && (voxel.RootVoxel is MyPlanet || voxel.PositionComp.LocalVolume.Radius < 15)) continue;

                if (voxel != null || ent.Physics.IsStatic)
                    StaticsInRangeTmp.Add(ent);

                FatMap map;
                if (grid != null && (PrevSubGrids.Contains(grid) || ValidGrids.Contains(ent) || grid.PositionComp.LocalVolume.Radius < 10 || Session.GridToFatMap.TryGetValue(grid, out map) && map.Trash || grid.BigOwners.Count == 0) ) continue;

                ObstructionsTmp.Add(ent);
            }
            ValidGrids.Clear();
            _possibleTargets.Clear();
        }


        internal void NearByShield()
        {
            NearByFriendlyShields.Clear();
            for (int i = 0; i < NearByShieldsTmp.Count; i++) {

                var shield = NearByShieldsTmp[i];
                var shieldGrid = MyEntities.GetEntityByIdOrDefault(shield.Id) as MyCubeGrid;
                
                if (shieldGrid != null) {

                    if (shield.Id == MyGrid.EntityId || MyGrid.IsSameConstructAs(shieldGrid))  {
                        MyShield = shield.ShieldEnt;
                    }
                    else {
                        var relation = shield.ShieldBlock.IDModule.GetUserRelationToOwner(MyOwner);
                        var friendly = relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Friends;
                        
                        if (friendly) {
                            NearByFriendlyShields.Add(shield.ShieldEnt);
                            FriendlyShieldNear = true;
                        }
                        ShieldNear = true;
                    }
                }

            }
            NearByShieldsTmp.Clear();
        }

        internal void MyPlanetInfo(bool clear = false)
        {
            if (!clear) {

                MyPlanet = MyPlanetTmp;
                var gridVolume = MyGrid.PositionComp.WorldVolume;
                var gridRadius = gridVolume.Radius;
                var gridCenter = gridVolume.Center;
                var planetCenter = MyPlanet.PositionComp.WorldAABB.Center;
                ClosestPlanetSqr = double.MaxValue;

                if (new BoundingSphereD(planetCenter, MyPlanet.AtmosphereRadius + gridRadius).Intersects(gridVolume)) {

                    InPlanetGravity = true;
                    PlanetClosestPoint = MyPlanet.GetClosestSurfacePointGlobal(gridCenter);
                    ClosestPlanetCenter = planetCenter;
                    double pointDistSqr;
                    Vector3D.DistanceSquared(ref PlanetClosestPoint, ref gridCenter, out pointDistSqr);

                    pointDistSqr -= (gridRadius * gridRadius);
                    if (pointDistSqr < 0) pointDistSqr = 0;
                    ClosestPlanetSqr = pointDistSqr;
                    PlanetSurfaceInRange = pointDistSqr <= MaxTargetingRangeSqr;
                }
                else {
                    InPlanetGravity = false;
                    PlanetClosestPoint = MyPlanet.GetClosestSurfacePointGlobal(gridCenter);
                    ClosestPlanetCenter = planetCenter;
                    double pointDistSqr;
                    Vector3D.DistanceSquared(ref PlanetClosestPoint, ref gridCenter, out pointDistSqr);

                    pointDistSqr -= (gridRadius * gridRadius);
                    if (pointDistSqr < 0) pointDistSqr = 0;
                    ClosestPlanetSqr = pointDistSqr;
                }
            }
            else {
                MyPlanet = null;
                PlanetClosestPoint = Vector3D.Zero;
                PlanetSurfaceInRange = false;
                InPlanetGravity = false;
                ClosestPlanetSqr = double.MaxValue;
            }
        }

        internal void MyStaticInfo()
        {
            ClosestStaticSqr = double.MaxValue;
            StaticGridInRange = false;
            MyEntity closestEnt = null;
            var closestCenter = Vector3D.Zero;
            double closestDistSqr = double.MaxValue;
            for (int i = 0; i < StaticsInRange.Count; i++) {

                var ent = StaticsInRange[i];
                if (ent == null) continue;
                if (ent.MarkedForClose) continue;

                var staticCenter = ent.PositionComp.WorldAABB.Center;
                if (ent is MyCubeGrid) StaticGridInRange = true;

                double distSqr;
                Vector3D.DistanceSquared(ref staticCenter, ref ScanVolume.Center, out distSqr);
                if (distSqr < closestDistSqr) {
                    closestDistSqr = distSqr;
                    closestEnt = ent;
                    closestCenter = staticCenter;
                }
            }

            if (closestEnt != null) {

                var dist = Vector3D.Distance(ScanVolume.Center, closestCenter);
                dist -= closestEnt.PositionComp.LocalVolume.Radius;
                dist -= ScanVolume.Radius;
                if (dist < 0) dist = 0;

                var distSqr = dist * dist;
                if (ClosestPlanetSqr < distSqr) distSqr = ClosestPlanetSqr;

                ClosestStaticSqr = distSqr;
            }
            else if (ClosestPlanetSqr < ClosestStaticSqr) ClosestStaticSqr = ClosestPlanetSqr;
        }

        internal bool CreateEntInfo(MyEntity entity, long gridOwner, out Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo)
        {
            try
            {
                if (entity == null) {
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
                    return false;
                }
                var topMostParent = entity.GetTopMostParent() as MyCubeGrid;
                if (topMostParent != null) {

                    MyRelationsBetweenPlayerAndBlock relationship;
                    if (topMostParent.BigOwners.Count > 0) {

                        var topOwner = topMostParent.BigOwners[0];
                        relationship = MyIDModule.GetRelationPlayerBlock(gridOwner, topOwner, MyOwnershipShareModeEnum.Faction);

                        if (relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.Friends && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare) {

                            var topFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(topOwner);
                            if (topFaction != null) {

                                var rep = MyAPIGateway.Session.Factions.GetReputationBetweenPlayerAndFaction(gridOwner, topFaction.FactionId);
                                if (topFaction.Members.ContainsKey(gridOwner))
                                    relationship = MyRelationsBetweenPlayerAndBlock.FactionShare;
                                else if (rep < -500)
                                    relationship = MyRelationsBetweenPlayerAndBlock.Enemies;
                                else if (rep <= 500)
                                    relationship = MyRelationsBetweenPlayerAndBlock.Neutral;
                                else relationship = MyRelationsBetweenPlayerAndBlock.Friends;
                            }
                        }
                    }
                    else relationship = MyRelationsBetweenPlayerAndBlock.NoOwnership;
                    var type = topMostParent.GridSizeEnum != MyCubeSize.Small ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.LargeGrid : Sandbox.ModAPI.Ingame.MyDetectedEntityType.SmallGrid;
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(topMostParent.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Tick);
                    return true;
                }

                var myCharacter = entity as IMyCharacter;
                if (myCharacter != null) {

                    var controllingId = myCharacter.ControllerInfo?.ControllingIdentityId;
                    var playerId = controllingId ?? 0;
                    var type = !myCharacter.IsPlayer ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterOther : Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterHuman;
                    var relationPlayerBlock = MyIDModule.GetRelationPlayerBlock(gridOwner, playerId, MyOwnershipShareModeEnum.Faction);

                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationPlayerBlock, new BoundingBoxD(), Session.Tick);
                    return !myCharacter.IsDead && myCharacter.Integrity > 0;
                }

                const MyRelationsBetweenPlayerAndBlock relationship1 = MyRelationsBetweenPlayerAndBlock.Neutral;
                var myPlanet = entity as MyPlanet;

                if (myPlanet != null) {
                    const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Planet;
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Tick);
                    return true;
                }
                if (entity is MyVoxelMap) {
                    const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Asteroid;
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Tick);
                    return true;
                }
                if (entity is MyMeteor) {
                    const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Meteor;
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, MyRelationsBetweenPlayerAndBlock.Enemies, new BoundingBoxD(), Session.Tick);
                    return true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CreateEntInfo: {ex}"); }
            entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
            return false;
        }
    }
}
