using System;
using System.Threading;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;
namespace WeaponCore
{
    public partial class Session
    {
        public void UpdateDbsInQueue()
        {
            DbsUpdating = true;
            MyAPIGateway.Parallel.Start(ProcessDbs, ProcessDbsCallBack);
            //ProcessDbs();
            //ProcessDbsCallBack();
        }

        private void ProcessDbs()
        {
            //MyAPIGateway.Parallel.For(0, DbsToUpdate.Count, x => DbsToUpdate[x].UpdateTargetDb(), 6);

            foreach (var db in DbsToUpdate)
            {
                db.UpdateTargetDb();
            }

            foreach (var db in DbsToUpdate)
            {
                db.FinalizeTargetDb();
            }
        }

        private void ProcessDbsCallBack()
        {
            foreach (var db in DbsToUpdate)
            {
                if (db.MyPlanetTmp != null)
                {
                    var gridBox = db.MyGrid.PositionComp.WorldAABB;
                    if (db.MyPlanetTmp.IntersectsWithGravityFast(ref gridBox)) db.MyPlanetInfo();
                    else if (db.MyPlanet != null) db.MyPlanetInfo(clear: true);
                }

                for (int i = 0; i < db.SubGridsTmp.Count; i++) db.SubGrids.Add(db.SubGridsTmp[i]);
                db.SubGridsTmp.Clear();

                for (int i = 0; i < db.SortedTargets.Count; i++) db.SortedTargets[i].Clean();
                db.SortedTargets.Clear();

                for (int i = 0; i < db.NewEntities.Count; i++)
                {
                    var detectInfo = db.NewEntities[i];
                    var ent = detectInfo.Parent;
                    var dictTypes = detectInfo.DictTypes;
                    var grid = ent as MyCubeGrid;
                    GridAi.TargetInfo targetInfo;

                    if (grid == null)
                        targetInfo = new GridAi.TargetInfo(detectInfo.EntInfo, ent, false, null, 1, db.MyGrid, db);
                    else
                        targetInfo = new GridAi.TargetInfo(detectInfo.EntInfo, grid, true, dictTypes, grid.GetFatBlocks().Count, db.MyGrid, db) { TypeDict = dictTypes };

                    db.SortedTargets.Add(targetInfo);
                }
                db.NewEntities.Clear();
                db.SortedTargets.Sort(db.TargetCompare1);

                db.Threats.Clear();
                db.Threats.Capacity = db.ThreatsTmp.Count;
                for (var i = 0; i < db.ThreatsTmp.Count; i++) db.Threats.Add(db.ThreatsTmp[i]);
                db.ThreatsTmp.Clear();

                db.TargetAis.Clear();
                db.TargetAis.Capacity = db.TargetAisTmp.Count;
                for (var i = 0; i < db.TargetAisTmp.Count; i++) db.TargetAis.Add(db.TargetAisTmp[i]);
                db.TargetAisTmp.Clear();

                db.Obstructions.Clear();
                for (int i = 0; i < db.ObstructionsTmp.Count; i++) db.Obstructions.Add(db.ObstructionsTmp[i]);
                db.ObstructionsTmp.Clear();

                db.StaticsInRange.Clear();
                if (db.PlanetSurfaceInRange) db.StaticsInRangeTmp.Add(db.MyPlanet);
                var staticCount = db.StaticsInRangeTmp.Count;

                for (int i = 0; i < staticCount; i++) db.StaticsInRange.Add(db.StaticsInRangeTmp[i]);
                db.StaticsInRangeTmp.Clear();
                db.StaticEntitiesInRange = staticCount > 0;

                db.DbReady = db.SortedTargets.Count > 0 || db.Threats.Count > 0 || db.FirstRun;
                db.FirstRun = false;
                //Log.Line($"[DB] - dbReady:{db.DbReady} - liveProjectiles:{db.LiveProjectile.Count} - armedGrids:{db.Threats.Count} - obstructions:{db.Obstructions.Count} - targets:{db.SortedTargets.Count} - checkedTargets:{db.NewEntities.Count} - targetRoots:{db.Targeting.TargetRoots.Count} - forGrid:{db.MyGrid.DebugName}");
                db.MyShield = db.MyShieldTmp;
                Interlocked.Exchange(ref db.DbUpdating, 0);
            }
            DbsToUpdate.Clear();
            DbsUpdating = false;
        }

        public void Handler(object o)
        {
            try
            {
                var message = o as byte[];
                if (message == null) return;
                var slaveDefArray = MyAPIGateway.Utilities.SerializeFromBinary<WeaponDefinition[]>(message);
                foreach (var wepDef in slaveDefArray)
                    _weaponDefinitions.Add(wepDef);
            }
            catch (Exception ex) { Log.Line($"Exception in Handler: {ex}"); }
        }

        internal void ResetGps()
        {
            if (TargetGps == null)
            {
                TargetGps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.MaxValue, true, true);
                MyAPIGateway.Session.GPS.AddLocalGps(TargetGps);
                MyVisualScriptLogicProvider.SetGPSColor(TargetGps.Name, Color.Yellow);
            }
        }

        internal void RemoveGps()
        {
            if (TargetGps != null)
            {
                MyAPIGateway.Session.GPS.RemoveLocalGps(TargetGps);
                TargetGps = null;
            }
        }

        internal void SetGpsInfo(Vector3D pos, string name)
        {
            if (TargetGps != null)
            {
                TargetGps.Coords = pos;
                TargetGps.Name = name;
            }
        }

        internal bool CheckTarget(GridAi ai)
        {
            if (Target == null) return false;
            if (Target.MarkedForClose || ai != TrackingAi)
            {
                Target = null;
                TrackingAi = null;
                return false;
            }

            return true;
        }

        internal void SetTarget(MyEntity entity, GridAi ai)
        {
            Target = entity;
            TrackingAi = ai;

            GridAi gridAi;
            if (GridTargetingAIs.TryGetValue((MyCubeGrid)entity, out gridAi))
            {
                TargetArmed = true;
            }
            else
            {
                foreach (var info in ai.SortedTargets)
                {
                    if (info.Target != entity) continue;
                    TargetArmed = info.TypeDict[TargetingDefinition.BlockTypes.Offense].Count > 0;
                    break;
                }
            }
        }

        internal void GetTargetInfo(GridAi ai, out double speed, out string armedStr, out string interceptStr)
        {
            var targetDir = Vector3D.Normalize(Target.Physics?.LinearVelocity ?? Vector3.Zero);
            var targetPos = Target.PositionComp.WorldAABB.Center;
            var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
            var myHeading = Vector3D.Normalize(myPos - targetPos);
            var degrees = Math.Cos(MathHelper.ToRadians(25));
            var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, degrees);

            armedStr = TargetArmed ? "Yes" : "No";
            interceptStr = intercept ? "Yes" : "No";
            speed = Math.Round(Target.Physics?.Speed ?? 0, 2);
        }

        internal static double ModRadius(double radius, bool largeBlock)
        {
            if (largeBlock && radius < 3) radius = 3;
            else if (largeBlock && radius > 25) radius = 25;
            else if (!largeBlock && radius > 5) radius = 5;

            radius = Math.Ceiling(radius);
            return radius;
        }

        internal void Timings()
        {
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
            Tick10 = Tick % 10 == 0;
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick180 = Tick % 180 == 0;
            Tick300 = Tick % 300 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;
            if (Tick60) ExplosionCounter = 0;
            if (_count++ == 59)
            {
                _count = 0;
            }
            _lCount++;
            if (_lCount == 129)
            {
                _lCount = 0;
                _eCount++;
                if (_eCount == 10) _eCount = 0;
            }
            if (!GameLoaded)
            {
                if (FirstLoop)
                {
                    if (!MiscLoaded)
                    {
                        MiscLoaded = true;
                        if (!IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);
                    }
                    GameLoaded = true;
                }
                else if (!FirstLoop)
                {
                    FirstLoop = true;
                    foreach (var t in AllDefinitions)
                    {
                        var name = t.Id.SubtypeName;
                        var contains = name.Contains("BlockArmor");
                        if (contains)
                        {
                            AllArmorBaseDefinitions.Add(t);
                            if (name.Contains("HeavyBlockArmor")) HeavyArmorBaseDefinitions.Add(t);
                        }
                    }
                }
            }

            if (ShieldMod && !ShieldApiLoaded && SApi.Load())
                ShieldApiLoaded = true;

            if (!CompsToStart.IsEmpty)
            {
                WeaponComponent weaponComp;
                CompsToStart.TryDequeue(out weaponComp);
                if (weaponComp.MyCube.CubeGrid.Physics != null)
                {
                    weaponComp.MyCube.Components.Add(weaponComp);
                    weaponComp.OnAddedToScene();
                    Log.Line($"added to comp");
                }
                //else RemoveGridAi(weaponComp);
            }
        }

        /*
        private void RemoveGridAi(WeaponComponent weaponComp)
        {
            WeaponComponent removedComp;
            GridTargetingAIs[weaponComp.MyCube.CubeGrid].WeaponBase.TryRemove(weaponComp.MyCube, out removedComp);

            GridAi removedAi;
            if (GridTargetingAIs[weaponComp.MyCube.CubeGrid].WeaponBase.Count == 0)
                GridTargetingAIs.TryRemove(weaponComp.MyCube.CubeGrid, out removedAi);
        }
        */

        private void CameraState()
        {
            CameraPos = Session.Camera.Position;
            ControlledEntity = Session.CameraController.Entity;
            if (ControlledEntity is IMyGunBaseUser)
            {
                var rawZoom = Session.Camera.FovWithZoom;
                Zoom = rawZoom <= 1 ? rawZoom : 1;
            }
            else Zoom = 1;
        }

        private void Paused()
        {
            Log.Line($"Stopping all AV due to pause");
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                foreach (var comp in gridAi.WeaponBase.Values)
                    comp.StopAllAv();
            }

            for (int i = 0; i < Projectiles.Wait.Length; i++)
                foreach (var p in Projectiles.ProjectilePool[i].Active)
                    p.PauseAv();
        }

        #region Events
        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id)) return;
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerConnected: {ex}"); }
        }

        private void PlayerDisconnected(long l)
        {
            try
            {
                IMyPlayer removedPlayer;
                Players.TryRemove(l, out removedPlayer);
                PlayerEventId++;
                if (removedPlayer.SteamUserId == AuthorSteamId)
                {
                    AuthorPlayerId = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerDisconnected: {ex}"); }
        }

        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                Players[id] = player;
                PlayerEventId++;
                if (player.SteamUserId == AuthorSteamId) AuthorPlayerId = player.IdentityId;
            }
            return false;
        }
        #endregion

        #region Misc
        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }
        #endregion

    }
}