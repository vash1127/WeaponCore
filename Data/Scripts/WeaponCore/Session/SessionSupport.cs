using System;
using System.Threading;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.Input;
using WeaponCore.Support;
namespace WeaponCore
{
    public partial class Session
    {
        public void UpdateDbsInQueue()
        {
            DbUpdating = true;
            MyAPIGateway.Parallel.Start(ProcessDbs, ProcessDbsCallBack);
        }

        private void ProcessDbs()
        {
            MyAPIGateway.Parallel.For(0, DbsToUpdate.Count, x => DbsToUpdate[x].UpdateTargetDb(), 6);
        }

        private void ProcessDbsCallBack()
        {
            foreach (var db in DbsToUpdate)
            {
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
                db.SortedTargets.Sort(db.TargetCompare1);
                //Log.Line($"[DB] targets:{db.SortedTargets.Count} - checkedTargets:{db.NewEntities.Count} - targetRoots:{db.Targeting.TargetRoots.Count} - forGrid:{db.MyGrid.DebugName}");
                db.BlockTypeIsSorted.Clear();
                db.DbReady = db.SortedTargets.Count > 0;
                Interlocked.Exchange(ref db.DbUpdating, 0);

            }
            DbsToUpdate.Clear();
            DbUpdating = false;
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

        internal void Timings()
        {
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
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
            ControlledEntity = Session.CameraController.Entity;
            if (ControlledEntity is IMyGunBaseUser)
            {
                var rawZoom = MyAPIGateway.Session.Camera.FovWithZoom;
                Zoom = rawZoom <= 1 ? rawZoom : 1;
            }
            else Zoom = 1;

            MouseButtonPressed = MyAPIGateway.Input.IsAnyMousePressed();
            if (MouseButtonPressed)
            {
                MouseButtonLeft = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Left);
                MouseButtonMiddle = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Middle);
                MouseButtonRight = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Right);
            }
            else
            {
                MouseButtonLeft = false;
                MouseButtonMiddle = false;
                MouseButtonRight = false;
            }

            if (!_compsToStart.IsEmpty)
            {
                WeaponComponent weaponComp;
                _compsToStart.TryDequeue(out weaponComp);
                if (weaponComp.MyCube.CubeGrid.Physics != null)
                {
                    weaponComp.MyCube.Components.Add(weaponComp);
                    weaponComp.OnAddedToScene();
                    Log.Line($"added to comp");
                }
                else RemoveGridAi(weaponComp);
            }
            if (!DedicatedServer) CameraPos = Session.Camera.Position;
        }

        private void RemoveGridAi(WeaponComponent weaponComp)
        {
            WeaponComponent removedComp;
            GridTargetingAIs[weaponComp.MyCube.CubeGrid].WeaponBase.TryRemove(weaponComp.MyCube, out removedComp);

            GridAi removedAi;
            if (GridTargetingAIs[weaponComp.MyCube.CubeGrid].WeaponBase.Count == 0)
                GridTargetingAIs.TryRemove(weaponComp.MyCube.CubeGrid, out removedAi);
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

        #region Power

        private bool GetDistributor()
        {
            var gotDistributor = false;
            foreach (var set in BlockSets.Values)
            {
                foreach (var controller in set.ShipControllers)
                {
                    var distributor = controller.GridResourceDistributor;
                    if (distributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects)
                    {
                        MyResourceDist = controller.GridResourceDistributor;
                        gotDistributor = true;
                        break;
                    }
                }
            }

            if (!gotDistributor) MyResourceDist = null;

            _checkForDistributor = false;
            return gotDistributor;
        }

        private bool UpdateGridPower()
        {
            GridAvailablePower = 0;
            GridMaxPower = 0;
            GridCurrentPower = 0;
            _batteryMaxPower = 0;
            _batteryCurrentOutput = 0;
            _batteryCurrentInput = 0;

            if (MyResourceDist != null || _checkForDistributor && GetDistributor())
            {
                if (MyResourceDist.SourcesEnabled == MyMultipleEnabledEnum.NoObjects)
                {
                    if (!GetDistributor()) FallBackPowerCalc();
                }
                else
                {
                    GridMaxPower = MyResourceDist.MaxAvailableResourceByType(GId);
                    GridCurrentPower = MyResourceDist.TotalRequiredInputByType(GId);
                    foreach (var values in BlockSets.Values)
                    {
                        foreach (var battery in values.Batteries)
                        {
                            if (!battery.CubeBlock.IsWorking) continue;
                            var currentInput = battery.Sink.CurrentInputByType(GId);
                            var currentOutput = battery.Source.CurrentOutputByType(GId);
                            var maxOutput = battery.Source.MaxOutputByType(GId);
                            if (currentInput > 0)
                            {
                                _batteryCurrentInput += currentInput;
                                _batteryCurrentOutput -= currentInput;
                            }
                            _batteryMaxPower += maxOutput;
                            _batteryCurrentOutput += currentOutput;
                        }
                    }
                }
            }
            else FallBackPowerCalc();

            GridAvailablePower = GridMaxPower - GridCurrentPower;


            GridCurrentPower += _batteryCurrentInput;
            GridAvailablePower -= _batteryCurrentInput;

            return GridMaxPower > 0;
        }

        private void FallBackPowerCalc(bool reportOnly = false)
        {
            foreach (var values in BlockSets.Values)
            {
                foreach (var source in values.Sources)
                {
                    var battery = source.Entity as IMyBatteryBlock;
                    if (battery != null)
                    {
                        //Log.Line($"bMaxO:{battery.MaxOutput} - bCurrO:{battery.CurrentOutput} - bCurrI:{battery.CurrentInput} - Charging:{battery.IsCharging}");
                        if (!battery.IsWorking) continue;
                        var currentInput = battery.CurrentInput;
                        var currentOutput = source.CurrentOutputByType(GId);
                        var maxOutput = source.MaxOutputByType(GId);
                        if (currentInput > 0)
                        {
                            _batteryCurrentInput += currentInput;
                            if (currentOutput <= 0 && maxOutput <= 0) _batteryCurrentOutput -= currentInput;
                            else _batteryCurrentOutput -= currentInput;
                        }
                        _batteryMaxPower += maxOutput;
                        _batteryCurrentOutput += currentOutput;
                    }
                    else
                    {
                        GridMaxPower += source.MaxOutputByType(GId);
                        GridCurrentPower += source.CurrentOutputByType(GId);
                    }
                }
            }
            GridMaxPower += _batteryMaxPower;
            GridCurrentPower += _batteryCurrentOutput;
        }

        #endregion
    }
}