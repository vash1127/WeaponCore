using System;
using System.Text;
using System.Threading;
using Jakaria;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
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
                RemSubGrids.Add((MyCubeGrid) grid);

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
                if (Session.ShieldApiLoaded && ent.DefinitionId?.SubtypeId == Session.ShieldHash && ent.Render.Visible) {

                    var shieldblock = Session.SApi.MatchEntToShieldFast(ent, false);
                    if (shieldblock != null)
                        NearByShieldsTmp.Add(new Shields { Id = ent.Hierarchy.ChildId, ShieldEnt = ent, ShieldBlock = (MyCubeBlock)shieldblock });
                }
                var voxel = ent as MyVoxelBase;
                var grid = ent as MyCubeGrid;
                var safeZone = ent as MySafeZone;

                var blockingThings = safeZone != null || ent.Physics != null && grid != null || voxel != null && voxel == voxel.RootVoxel;
                if (!blockingThings || voxel != null && (voxel.RootVoxel is MyPlanet || voxel.PositionComp.LocalVolume.Radius < 15)) continue;

                if (voxel != null || safeZone != null || ent.Physics.IsStatic)
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
                        TargetInfo info;
                        var found = Targets.TryGetValue(shield.ShieldBlock.CubeGrid, out info);
                        var relation = found ? info.EntInfo.Relationship : shield.ShieldBlock.IDModule.GetUserRelationToOwner(MyOwner);
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
                    TouchingWater = Session.WaterApiLoaded && GridTouchingWater();
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
                    TouchingWater = false;
                }
            }
            else {
                MyPlanet = null;
                PlanetClosestPoint = Vector3D.Zero;
                PlanetSurfaceInRange = false;
                InPlanetGravity = false;
                ClosestPlanetSqr = double.MaxValue;
                TouchingWater = false;
            }
        }

        private bool GridTouchingWater()
        {
            Water water;
            if (Session.WaterMap.TryGetValue(MyPlanet, out water)) {
                WaterVolume = new BoundingSphereD(MyPlanet.PositionComp.WorldAABB.Center, water.radius + water.waveHeight);
                return new MyOrientedBoundingBoxD(MyGrid.PositionComp.LocalAABB, MyGrid.PositionComp.WorldMatrixRef).Intersects(ref WaterVolume);
            }
            return false;
        }

        internal void MyStaticInfo()
        {
            ClosestStaticSqr = double.MaxValue;
            StaticGridInRange = false;
            MyEntity closestEnt = null;
            var closestCenter = Vector3D.Zero;
            double closestDistSqr = double.MaxValue;
            CanShoot = true;
            for (int i = 0; i < StaticsInRange.Count; i++) {

                var ent = StaticsInRange[i];
                if (ent == null) continue;
                if (ent.MarkedForClose) continue;
                var safeZone = ent as MySafeZone;
                

                var staticCenter = ent.PositionComp.WorldAABB.Center;
                if (ent is MyCubeGrid) StaticGridInRange = true;

                double distSqr;
                Vector3D.DistanceSquared(ref staticCenter, ref ScanVolume.Center, out distSqr);
                if (distSqr < closestDistSqr) {
                    closestDistSqr = distSqr;
                    closestEnt = ent;
                    closestCenter = staticCenter;
                }

                if (CanShoot && safeZone != null && safeZone.Enabled) {

                    if (safeZone.PositionComp.WorldVolume.Contains(MyGrid.PositionComp.WorldVolume) != ContainmentType.Disjoint && ((Session.SafeZoneAction)safeZone.AllowedActions & Session.SafeZoneAction.Shooting) == 0)
                        CanShoot = !TouchingSafeZone(safeZone);
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

        private bool TouchingSafeZone(MySafeZone safeZone)
        {
            var myObb = new MyOrientedBoundingBoxD(MyGrid.PositionComp.LocalAABB, MyGrid.PositionComp.WorldMatrixRef);
            
            if (safeZone.Shape == MySafeZoneShape.Sphere) {
                var sphere = new BoundingSphereD(safeZone.PositionComp.WorldVolume.Center, safeZone.Radius);
                return myObb.Intersects(ref sphere);
            }

            return new MyOrientedBoundingBoxD(safeZone.PositionComp.LocalAABB, safeZone.PositionComp.WorldMatrixRef).Contains(ref myObb) != ContainmentType.Disjoint;
        }

        internal bool CreateEntInfo(MyEntity entity, long gridOwner, out Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo)
        {
            try
            {
                if (entity == null)
                {
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
                    return false;
                }
                var topMostParent = entity.GetTopMostParent() as MyCubeGrid;
                if (topMostParent != null)
                {

                    MyRelationsBetweenPlayerAndBlock relationship;
                    var bigOwners = topMostParent.BigOwners;
                    var topOwner = bigOwners.Count > 0 ? bigOwners[0] : long.MinValue;
                    if (topOwner != long.MinValue)
                    {

                        relationship = MyIDModule.GetRelationPlayerBlock(gridOwner, topOwner, MyOwnershipShareModeEnum.Faction);

                        if (relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.Friends && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                        {

                            var topFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(topOwner);
                            if (topFaction != null)
                            {

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
                if (myCharacter != null)
                {

                    var controllingId = myCharacter.ControllerInfo?.ControllingIdentityId;
                    var playerId = controllingId ?? 0;
                    var type = !myCharacter.IsPlayer ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterOther : Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterHuman;
                    var relationPlayerBlock = MyIDModule.GetRelationPlayerBlock(gridOwner, playerId, MyOwnershipShareModeEnum.Faction);

                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationPlayerBlock, new BoundingBoxD(), Session.Tick);
                    return !myCharacter.IsDead && myCharacter.Integrity > 0;
                }

                const MyRelationsBetweenPlayerAndBlock relationship1 = MyRelationsBetweenPlayerAndBlock.Neutral;
                var myPlanet = entity as MyPlanet;

                if (myPlanet != null)
                {
                    const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Planet;
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Tick);
                    return true;
                }
                if (entity is MyVoxelMap)
                {
                    const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Asteroid;
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Tick);
                    return true;
                }
                if (entity is MyMeteor)
                {
                    const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Meteor;
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, MyRelationsBetweenPlayerAndBlock.Enemies, new BoundingBoxD(), Session.Tick);
                    return true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CreateEntInfo: {ex}"); }
            entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
            return false;
        }

        internal bool CreateEntInfoNew(MyEntity entity, long gridOwner, out Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo)
        {
            try
            {
                if (entity == null) {
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
                    return false;
                }
                var topMostParent = entity.GetTopMostParent() as MyCubeGrid;
                if (topMostParent != null) {
                    
                    var relationship = ComputeGridRelaations(gridOwner, topMostParent);
                    
                    var type = topMostParent.GridSizeEnum != MyCubeSize.Small ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.LargeGrid : Sandbox.ModAPI.Ingame.MyDetectedEntityType.SmallGrid;
                    entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(topMostParent.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Tick);
                    return true;
                }

                var myCharacter = entity as IMyCharacter;
                if (myCharacter != null) {

                    var controllingId = myCharacter.ControllerInfo?.ControllingIdentityId;
                    var playerId = controllingId ?? 0;
                    var type = !myCharacter.IsPlayer ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterOther : Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterHuman;
                    var relationPlayerBlock = ComputePlayerRelations(gridOwner, playerId);

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
        
        private MyRelationsBetweenPlayerAndBlock ComputePlayerRelations(long owner, long playerId)
        {
            var count = 0;
            MyRelationsBetweenPlayerAndBlock relationship = MyRelationsBetweenPlayerAndBlock.NoOwnership;
            for (int attempts = 0; attempts < 5; attempts++) {
                
                count++;
                try
                {
                    Session.GetRelationPlayerBlock(owner, playerId, MyOwnershipShareModeEnum.Faction);
                    break;
                }
                catch
                {
                    // ignored
                }
            }
            if (count > 1)
                Log.Line($"ComputeGridRelations failed {count - 1} times");
            return relationship;
        }

        private MyRelationsBetweenPlayerAndBlock ComputeGridRelaations(long owner, MyCubeGrid topGrid)
        {
            var count = 0;
            MyRelationsBetweenPlayerAndBlock relationship = MyRelationsBetweenPlayerAndBlock.NoOwnership;
            for (int attempts = 0; attempts < 5; attempts++)
            {

                count++;
                try
                {
                    var bigowners = topGrid.BigOwners;
                    var topOwner = bigowners.Count > 0 ? bigowners[0] : long.MinValue;
                    if (topOwner == long.MinValue)
                        return relationship;
                    
                    relationship = Session.GetRelationPlayerBlock(owner, topOwner, MyOwnershipShareModeEnum.Faction);

                    if (relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.Friends && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                    {

                        Session.FactionInfo topFaction;
                        if (Session.UserFactions.TryGetValue(topOwner, out topFaction))
                        {

                            var rep = MyAPIGateway.Session.Factions.GetReputationBetweenPlayerAndFaction(owner, topFaction.Faction.FactionId);
                            if (topFaction.Members.ContainsKey(owner))
                                relationship = MyRelationsBetweenPlayerAndBlock.FactionShare;
                            else if (rep < -500)
                                relationship = MyRelationsBetweenPlayerAndBlock.Enemies;
                            else if (rep <= 500)
                                relationship = MyRelationsBetweenPlayerAndBlock.Neutral;
                            else relationship = MyRelationsBetweenPlayerAndBlock.Friends;
                        }
                    }
                    break;
                }
                catch
                {
                    // ignored
                }
            }
            if (count > 1)
                Log.Line($"ComputePlayerRelations failed {count - 1} times");
            return relationship;
        }
    }
}
