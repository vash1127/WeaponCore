using System;
using System.Collections.Concurrent;
using ParallelTasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;
using WeaponCore.Platform;
using Sandbox.Definitions;
using System.Collections.Generic;
using Sandbox.Game;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;

namespace WeaponCore
{
    public partial class Session
    {
        public void Handler(object o)
        {
            try
            {
                var message = o as byte[];
                if (message == null) return;
                var slaveDefArray = MyAPIGateway.Utilities.SerializeFromBinary<WeaponDefinition[]>(message);

                var subTypes = new HashSet<string>();
                foreach (var wepDef in slaveDefArray)
                {
                    WeaponDefinitions.Add(wepDef);

                    for (int i = 0; i < wepDef.Assignments.MountPoints.Length; i++)
                        subTypes.Add(wepDef.Assignments.MountPoints[i].SubtypeId);
                }
                var group = MyStringHash.GetOrCompute("Charging");

                foreach (var def in AllDefinitions)
                {
                    if (subTypes.Contains(def.Id.SubtypeName))
                    {
                        if (def is MyLargeTurretBaseDefinition)
                        {
                            var weaponDef = def as MyLargeTurretBaseDefinition;
                            weaponDef.ResourceSinkGroup = group;
                        }
                        else if (def is MyConveyorSorterDefinition)
                        {
                            var weaponDef = def as MyConveyorSorterDefinition;
                            weaponDef.ResourceSinkGroup = group;
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Handler: {ex}"); }
        }

        public void UpgradeHandler(object o)
        {
            try
            {
                var message = o as byte[];
                if (message == null) return;
                var slaveDefArray = MyAPIGateway.Utilities.SerializeFromBinary<UpgradeDefinition[]>(message);
                foreach (var upgDef in slaveDefArray)
                    _upgradeDefinitions.Add(upgDef);
            }
            catch (Exception ex) { Log.Line($"Exception in Handler: {ex}"); }
        }


        private void StartComps()
        {
            var reassign = false;
            for (int i = 0; i < CompsToStart.Count; i++)
            {
                var weaponComp = CompsToStart[i];
                if (weaponComp.MyCube.CubeGrid.IsPreview)
                {
                    Log.Line($"[IsPreview] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.Ai.DelayedGridCleanUp(null);
                    weaponComp.RemoveCompList();
                    CompsToStart.Remove(weaponComp);
                    continue;
                }
                if (weaponComp.MyCube.CubeGrid.Physics == null && !weaponComp.MyCube.CubeGrid.MarkedForClose && weaponComp.MyCube.BlockDefinition.HasPhysics)
                    continue;
                if (weaponComp.Ai.MyGrid != weaponComp.MyCube.CubeGrid)
                {
                    if (!GridToFatMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                    {
                        Log.Line($"grid not yet in map1");
                        continue;
                    }

                    Log.Line($"[StartComps - gridMisMatch] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - WeaponName:{weaponComp.MyCube.BlockDefinition.Id.SubtypeId.String} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid} - {weaponComp.Ai.MyGrid.MarkedForClose}");
                    weaponComp.RemoveCompList();
                    InitComp(weaponComp.MyCube, false);
                    reassign = true;
                    CompsToStart.Remove(weaponComp);
                }
                else if (weaponComp.Platform.State == MyWeaponPlatform.PlatformState.Fresh)
                {
                    if (weaponComp.MyCube.MarkedForClose)
                    {
                        weaponComp.Ai.DelayedGridCleanUp(null);
                        CompsToStart.Remove(weaponComp);
                        continue;
                    }
                    if (!GridToFatMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                    {
                        //Log.Line($"grid not in map and Platform state is fresh: marked:{weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - working:{weaponComp.MyCube.IsWorking} - functional:{weaponComp.MyCube.IsFunctional} - {weaponComp.MyCube.CubeGrid.DebugName} - gridMisMatch:{weaponComp.Ai.MyGrid != weaponComp.MyCube.CubeGrid}");
                        continue;
                    }
                    //Log.Line($"[Init] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.MyCube.Components.Add(weaponComp);
                    CompsToStart.Remove(weaponComp);
                }
                else
                {
                    Log.Line($"comp didn't match CompsToStart condition, removing");
                    //Log.Line($"[Other] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - WeaponName:{weaponComp.Ob.SubtypeId.String} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.Ai.DelayedGridCleanUp(null);
                    CompsToStart.Remove(weaponComp);
                }
            }
            CompsToStart.ApplyRemovals();
            if (reassign)
            {
                CompsToStart.ApplyAdditions();
                StartComps();
            }
        }

        private void InitComp(MyCubeBlock cube, bool thread = true)
        {
            if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

            using (cube.Pin())
            {
                if (cube.MarkedForClose)
                {
                    Log.Line($"cube marked for close in InitComp");
                    return;
                }
                GridAi gridAi;
                if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                {
                    gridAi = GridAiPool.Get();
                    gridAi.Init(cube.CubeGrid, this);
                    GridTargetingAIs.TryAdd(cube.CubeGrid, gridAi);
                }

                var weaponComp = new WeaponComponent(gridAi, cube);
                if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp))
                {
                    weaponComp.AddCompList(); // thread safe because on first add nothing can access list from main thread?
                    var blockDef = cube.BlockDefinition.Id.SubtypeId;
                    if (!gridAi.WeaponCounter.ContainsKey(blockDef))
                        gridAi.WeaponCounter.TryAdd(blockDef, WeaponCountPool.Get());

                    CompsToStart.Add(weaponComp);
                    if (thread) CompsToStart.ApplyAdditions();
                }
                else Log.Line($"gridAi not found/created in InitComp");
            }
        }

        private void ChangeComps()
        {
            DsUtil2.Start("ChangeComps");
            foreach (var change in CompChanges)
            {
                if (change.Change != CompChange.ChangeType.OnRemovedFromSceneQueue && !GridToFatMap.ContainsKey(change.Comp.MyCube.CubeGrid))
                    continue;

                CompChange removed;
                switch (change.Change)
                {
                    case CompChange.ChangeType.PlatformInit:
                        CompChanges.TryDequeue(out removed);
                        change.Comp.PlatformInit();
                        break;
                    case CompChange.ChangeType.Init:
                        CompChanges.TryDequeue(out removed);
                        change.Comp.Init();
                        break;
                    case CompChange.ChangeType.Reinit:
                        CompChanges.TryDequeue(out removed);
                        change.Comp.ReInit();
                        break;
                    case CompChange.ChangeType.OnRemovedFromSceneQueue:
                        CompChanges.TryDequeue(out removed);
                        change.Comp.OnRemovedFromSceneQueue();
                        break;
                }
            }
            DsUtil2.Complete("ChangeComps", false, true);
        }

        private void DelayedComps()
        {
            foreach (var delayed in CompsDelayed)
            {
                WeaponComponent remove;
                if (delayed.MyCube.MarkedForClose)
                    CompsDelayed.TryDequeue(out remove);
                else if (delayed.MyCube.IsFunctional)
                {
                    Log.Line($"delayed released");
                    CompsDelayed.TryDequeue(out remove);
                    CompChanges.Enqueue(new CompChange { Ai = delayed.Ai, Comp = delayed, Change = CompChange.ChangeType.PlatformInit });
                }
            }
        }

        internal int LoadAssigner()
        {
            if (_loadCounter + 1 > 119) _loadCounter = 0;
            else ++_loadCounter;

            return _loadCounter;
        }

        internal ConcurrentDictionary<MyDefinitionId, ConcurrentDictionary<MyInventory, MyFixedPoint>> GetMasterInventory()
        {

            if (InventoryPool.Count > 0) return InventoryPool.Pop();
            return new ConcurrentDictionary<MyDefinitionId, ConcurrentDictionary<MyInventory, MyFixedPoint>>(AmmoInventoriesMaster, MyDefinitionId.Comparer);
        }

        internal void ReturnMasterInventory(ConcurrentDictionary<MyDefinitionId, ConcurrentDictionary<MyInventory, MyFixedPoint>> inventory)
        {
            InventoryPool.Push(inventory);
        }

        internal void Timings()
        {
            _paused = false;
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
            Tick10 = Tick % 10 == 0;
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick120 = Tick % 120 == 0;
            Tick180 = Tick % 180 == 0;
            Tick300 = Tick % 300 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;
            if (Tick60) ExplosionCounter = 0;
            if (Count++ == 119)
            {
                Count = 0;
                UiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                UiOpacity = MyAPIGateway.Session.Config.UIOpacity;
            }
            LCount++;
            if (LCount == 129)
            {
                LCount = 0;
                ECount++;
                if (ECount == 10) ECount = 0;
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
        }

        internal static double ModRadius(double radius, bool largeBlock)
        {
            if (largeBlock && radius < 3) radius = 3;
            else if (largeBlock && radius > 25) radius = 25;
            else if (!largeBlock && radius > 5) radius = 5;

            radius = Math.Ceiling(radius);
            return radius;
        }

        public void WeaponDebug(Weapon w)
        {
            DsDebugDraw.DrawLine(w.MyPivotTestLine, Color.Green, 0.05f);
            DsDebugDraw.DrawLine(w.MyBarrelTestLine, Color.Red, 0.05f);
            DsDebugDraw.DrawLine(w.MyCenterTestLine, Color.Blue, 0.05f);
            DsDebugDraw.DrawLine(w.MyAimTestLine, Color.Black, 0.07f);
            //DsDebugDraw.DrawSingleVec(w.MyPivotPos, 1f, Color.White);
            //DsDebugDraw.DrawBox(w.targetBox, Color.Plum);
            DsDebugDraw.DrawLine(w.LimitLine.From, w.LimitLine.To, Color.Orange, 0.05f);

            if (w.Target.State == Target.Targets.Acquired)
                DsDebugDraw.DrawLine(w.MyShootAlignmentLine, Color.Yellow, 0.05f);
        }

        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        private void Paused()
        {
            Pause = true;
            Log.Line($"Stopping all AV due to pause");
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                foreach (var comp in gridAi.WeaponBase.Values)
                    comp.StopAllAv();
            }

            foreach (var p in Projectiles.ProjectilePool.Active)
                p.PauseAv();
        }

        public bool TaskHasErrors(ref Task task, string taskName)
        {
            if (task.Exceptions != null && task.Exceptions.Length > 0)
            {
                foreach (var e in task.Exceptions)
                {
                    Log.Line($"{taskName} thread!\n{e}");
                }

                return true;
            }

            return false;
        }


        private void DeferedUpBlockTypeCleanUp(bool force = false)
        {
            foreach (var clean in BlockTypeCleanUp)
            {
                if (force || Tick - clean.RequestTick > 120)
                {
                    foreach (var item in clean.Collection)
                    {
                        item.Value.ClearImmediate();
                        ConcurrentListPool.Return(item.Value);
                    }
                    clean.Collection.Clear();
                    BlockTypePool.Return(clean.Collection);

                    DeferedTypeCleaning removed;
                    BlockTypeCleanUp.TryDequeue(out removed);
                }
            }
        }

        internal void PurgeAll()
        {
            FutureEvents.Purge((int)Tick);

            foreach (var item in _effectedCubes)
            {
                var cubeid = item.Key;
                var blockInfo = item.Value;
                var functBlock = blockInfo.FunctBlock;
                var cube = blockInfo.CubeBlock;

                if (cube == null || cube.MarkedForClose)
                {
                    _effectPurge.Enqueue(cubeid);
                    continue;
                }

                functBlock.EnabledChanged -= ForceDisable;
                functBlock.Enabled = blockInfo.FirstState;
                cube.SetDamageEffect(false);
                _effectPurge.Enqueue(cubeid);
            }

            while (_effectPurge.Count != 0)
            {
                _effectedCubes.Remove(_effectPurge.Dequeue());
            }
            /*
            foreach (var s in _shrinking)
            {
                s.Clean();
                ShrinkPool.Return(s);
                _shrinking.Remove(s);
            }
            _shrinking.ClearImmediate();
            ShrinkPool.Clean();
            */
            for (int i = _afterGlow.Count - 1; i >= 0; i--)
            {
                var g = _afterGlow[i];
                _afterGlow.RemoveAtFast(i);
            }
            _afterGlow.Clear();
            Glows.Clear();
            AvShotPool.Clean();

            DeferedUpBlockTypeCleanUp(true);

            foreach (var map in GridToFatMap.Keys)
                RemoveGridFromMap(map);
            
            GridToFatMap.Clear();
            FatMapPool.Clean();

            DirtyGridsTmp.Clear();

            foreach (var structure in WeaponPlatforms.Values)
            {
                structure.WeaponSystems.Clear();
                structure.AmmoToWeaponIds.Clear();
            }
            WeaponPlatforms.Clear();

            foreach (var gridToMap in GridToBlockTypeMap)
            {
                foreach (var map in gridToMap.Value)
                {
                    map.Value.ClearImmediate();
                    ConcurrentListPool.Return(map.Value);
                }
                gridToMap.Value.Clear();
                BlockTypePool.Return(gridToMap.Value);
            }
            GridToBlockTypeMap.Clear();

            DirtyGrids.Clear();

            DsUtil.Purge();
            DsUtil2.Purge();

            _effectActive = false;
            ShootingWeapons.Clear();
            AcquireTargets.Clear();
            RemoveEffectsFromGrid.Clear();
            WeaponAmmoPullQueue.Clear();
            AmmoToPullQueue.Clear();
            Hits.Clear();
            AllArmorBaseDefinitions.Clear();
            HeavyArmorBaseDefinitions.Clear();
            AllArmorBaseDefinitions.Clear();
            AcquireTargets.Clear();
            ChargingWeapons.Clear();
            ShootingWeapons.Clear();
            LargeBlockSphereDb.Clear();
            SmallBlockSphereDb.Clear();
            AnimationsToProcess.Clear();
            _subTypeIdToWeaponDefs.Clear();
            WeaponDefinitions.Clear();
            _slimsSortedList.Clear();
            _destroyedSlims.Clear();
            _slimsSet.Clear();
            _turretDefinitions.Clear();

            if (!CompsToStart.IsEmpty) Log.Line($"CompsToStart not empty");
            CompsToStart.ClearImmediate();
            
            GridEffectPool.Clean();
            GridEffectsPool.Clean();
            BlockTypePool.Clean();
            ConcurrentListPool.Clean();

            GroupInfoPool.Clean();
            TargetInfoPool.Clean();

            Projectiles.Clean();
            WeaponCoreBlockDefs.Clear();
            WeaponCoreFixedBlockDefs.Clear();
            WeaponCoreTurretBlockDefs.Clear();
            Projectiles.CheckPool.Clean();
            Projectiles.ShrapnelToSpawn.Clear();
            Projectiles.ShrapnelPool.Clean();
            Projectiles.FragmentPool.Clean();
            Projectiles.CheckPool.Clean();
            Projectiles.ProjectilePool.DeallocateAll();
            Projectiles.HitEntityPool.Clean();
            Projectiles.DrawProjectiles.Clear();
            Projectiles.CleanUp.Clear();
            Projectiles.InfoPool.DeallocateAll();
            Projectiles.V3Pool.Clean();

            if (DbsToUpdate.Count > 0) Log.Line("DbsToUpdate not empty at purge");
            DbsToUpdate.Clear();
            GridTargetingAIs.Clear();

            Projectiles.EntityPool = null;
            Projectiles = null;
            TrackingAi = null;
            UiInput = null;
            TargetUi = null;
            Placer = null;
            WheelUi = null;
            TargetGps = null;
            SApi.Unload();
            SApi = null;
            Api = null;
            ApiServer = null;

            WeaponDefinitions = null;
            AnimationsToProcess = null;
            ProjectileTree.Clear();
            ProjectileTree = null;

            AllDefinitions = null;
            SoundDefinitions = null;
            ActiveCockPit = null;
            ControlledEntity = null;
        }
    }
}