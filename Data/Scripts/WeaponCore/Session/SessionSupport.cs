using System;
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
                    //Log.Line($"[IsPreview] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.RemoveComp(onThread: false);
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

                    Log.Line($"[gridMisMatch] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - WeaponName:{weaponComp.MyCube.BlockDefinition.Id.SubtypeId.String} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid} - {weaponComp.Ai.MyGrid.MarkedForClose}");
                    weaponComp.RemoveComp(onThread: false);
                    InitComp(weaponComp.MyCube, false);
                    reassign = true;
                    CompsToStart.Remove(weaponComp);
                }
                else if (weaponComp.Platform.State == MyWeaponPlatform.PlatformState.Refresh)
                {
                    if (!GridToFatMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                    {
                        //Log.Line($"grid not in map2: marked:{weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - working:{weaponComp.MyCube.IsWorking} - functional:{weaponComp.MyCube.IsFunctional} - {weaponComp.MyCube.CubeGrid.DebugName} - gridMisMatch:{weaponComp.Ai.MyGrid != weaponComp.MyCube.CubeGrid}");
                        continue;
                    }
                    //Log.Line($"[Init] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.MyCube.Components.Add(weaponComp);
                    CompsToStart.Remove(weaponComp);
                }
                else
                {
                    //Log.Line($"[Other] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - WeaponName:{weaponComp.Ob.SubtypeId.String} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
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

        private void InitComp(MyCubeBlock cube, bool apply = true)
        {
            if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

            using (cube.Pin())
            {
                if (cube.MarkedForClose) return;
                GridAi gridAi;
                if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                {
                    gridAi = new GridAi(cube.CubeGrid, this, Tick);
                    GridTargetingAIs.TryAdd(cube.CubeGrid, gridAi);
                }
                var weaponComp = new WeaponComponent(gridAi, cube);
                if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp))
                {
                    weaponComp.UpdateCompList(add: true, invoke: apply);
                    if (!gridAi.WeaponCounter.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                        gridAi.WeaponCounter.TryAdd(cube.BlockDefinition.Id.SubtypeId, new GridAi.WeaponCount());

                    CompsToStart.Add(weaponComp);
                    if (apply) CompsToStart.ApplyAdditions();
                }
            }
        }

        internal int LoadAssigner()
        {
            if (_loadCounter + 1 > 119) _loadCounter = 0;
            else ++_loadCounter;

            return _loadCounter;
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
            if (_count++ == 119)
            {
                _count = 0;
                UiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                UiOpacity = MyAPIGateway.Session.Config.UIOpacity;
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
            if (w.TargetBox != null)
            {
                //DsDebugDraw.DrawBox(w.targetBox, Color.Plum);
                DsDebugDraw.DrawLine(w.LimitLine.From, w.LimitLine.To, Color.Orange, 0.05f);
            }

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

        internal void WeaponShootOff(object obj)
        {
            var gridAi = obj as GridAi;
            if (gridAi == null) return;

            for (int i = 0; i < gridAi.Weapons.Count; i++)
            {
                var comp = gridAi.Weapons[i];
                for (int x = 0; x < comp.Platform.Weapons.Length; x++)
                {
                    var w = comp.Platform.Weapons[x];
                    w.StopReloadSound();
                    w.StopShooting();
                }
            }
        }

        internal void DeferedFatMapRemoval(object obj)
        {
            var grid = (MyCubeGrid)obj;
            FatMap fatMap;
            if (GridToFatMap.TryRemove(grid, out fatMap))
            {
                fatMap.MyCubeBocks.ClearImmediate();
                ConcurrentListPool.Return(fatMap.MyCubeBocks);
                fatMap.Trash = true;
                fatMap.MyCubeBocks = null;
                fatMap.Targeting = null;
                FatMapPool.Return(fatMap);
                grid.OnFatBlockAdded -= ToFatMap;
                grid.OnFatBlockRemoved -= FromFatMap;
                grid.OnClose -= RemoveGridFromMap;
                grid.AddedToScene -= GridAddedToScene;
                DirtyGrids.Add(grid);
            }
            else Log.Line($"grid not removed and list not cleaned");
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
                g.Clean();
                _afterGlow.RemoveAtFast(i);
                GlowPool.Return(g);
            }
            _afterGlow.Clear();
            GlowPool.Clean();

            foreach (var map in GridToFatMap.Keys)
                DeferedFatMapRemoval(map);
            
            GridToFatMap.Clear();
            FatMapPool.Clean();

            DirtyGrids.Clear();
            DirtyGridsTmp.Clear();

            foreach (var structure in WeaponPlatforms.Values)
            {
                structure.WeaponSystems.Clear();
                structure.AmmoToWeaponIds.Clear();
            }
            WeaponPlatforms.Clear();

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
            GridToBlockTypeMap.Clear();
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