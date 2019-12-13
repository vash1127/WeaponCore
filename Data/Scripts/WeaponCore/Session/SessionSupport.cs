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
                    _weaponDefinitions.Add(wepDef);

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
                    weaponComp.RemoveComp();
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
                    weaponComp.RemoveComp();
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
                    if (!gridAi.WeaponCounter.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                        gridAi.WeaponCounter.TryAdd(cube.BlockDefinition.Id.SubtypeId, new GridAi.WeaponCount());

                    CompsToStart.Add(weaponComp);
                    if (apply) CompsToStart.ApplyAdditions();
                }
            }
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

            if (!w.Target.Expired)
                DsDebugDraw.DrawLine(w.MyShootAlignmentLine, Color.Yellow, 0.05f);
        }

        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
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

        internal void UpdateWeaponHeat(object heatTracker)
        {
            var ht = heatTracker as MyTuple<Weapon, int, bool>?;
            if (ht != null && ht.Value.Item1.Comp.Status == WeaponComponent.Start.Started)
            {
                var w = ht.Value.Item1;
                var currentHeat = w.Comp.State.Value.Weapons[w.WeaponId].Heat;
                currentHeat = currentHeat - ((float)w.HsRate / 3) > 0 ? currentHeat - ((float)w.HsRate / 3) : 0;
                var set = currentHeat - w.LastHeat > 0.001 || (currentHeat - w.LastHeat) * -1 > 0.001;

                if (!DedicatedServer)
                {
                    var heatPercent = currentHeat / w.System.MaxHeat;

                    if (set && heatPercent > .33)
                    {
                        if (heatPercent > 1) heatPercent = 1;

                        heatPercent -= .33f;

                        var intensity = .7f * heatPercent;

                        var color = HeatEmissives[(int)(heatPercent * 100)];

                        w.MuzzlePart.Item1.SetEmissiveParts("Heating", color, intensity);
                    }
                    else if (set)
                        w.MuzzlePart.Item1.SetEmissiveParts("Heating", Color.Transparent, 0);

                    w.LastHeat = currentHeat;
                }

                if (set && w.System.DegRof && w.Comp.State.Value.Weapons[w.WeaponId].Heat >= (w.System.MaxHeat * .8))
                {
                    var systemRate = w.System.RateOfFire * w.Comp.Set.Value.RofModifier;
                    var barrelRate = w.System.BarrelSpinRate * w.Comp.Set.Value.RofModifier;
                    var heatModifier = (int)MathHelper.Lerp(1, .25, w.Comp.State.Value.Weapons[w.WeaponId].Heat / w.System.MaxHeat);

                    systemRate *= heatModifier;

                    if (systemRate < 1)
                        systemRate = 1;

                    w.RateOfFire = (int)systemRate;
                    w.BarrelSpinRate = (int)barrelRate;
                    w.TicksPerShot = (uint)(3600f / w.RateOfFire);
                    w.UpdateBarrelRotation();
                    w.CurrentlyDegrading = true;
                }
                else if (set && w.CurrentlyDegrading)
                {
                    w.CurrentlyDegrading = false;
                    w.RateOfFire = (int)(w.System.RateOfFire * w.Comp.Set.Value.RofModifier);
                    w.BarrelSpinRate = (int)(w.System.BarrelSpinRate * w.Comp.Set.Value.RofModifier);
                    w.TicksPerShot = (uint)(3600f / w.RateOfFire);
                    w.UpdateBarrelRotation();
                }

                var resetFakeTick = false;

                if (ht.Value.Item2 * 30 == 60)
                {
                    var weaponValue = w.Comp.State.Value.Weapons[w.WeaponId];
                    w.Comp.CurrentHeat = w.Comp.CurrentHeat >= w.HsRate ? w.Comp.CurrentHeat - w.HsRate : 0;
                    weaponValue.Heat = weaponValue.Heat >= w.HsRate ? weaponValue.Heat - w.HsRate : 0;

                    w.Comp.TerminalRefresh();
                    if (w.Comp.Overheated && weaponValue.Heat <= (w.System.MaxHeat * w.System.WepCooldown))
                    {
                        w.EventTriggerStateChanged(Weapon.EventTriggers.Overheated, false);
                        w.Comp.Overheated = false;
                    }

                    resetFakeTick = true;
                }

                if (w.Comp.State.Value.Weapons[w.WeaponId].Heat > 0 || ht.Value.Item3)
                {
                    int fakeTick;
                    if (resetFakeTick)
                        fakeTick = 0;
                    else
                        fakeTick = ht.Value.Item2 + 1;

                    FutureEvents.Schedule(UpdateWeaponHeat, MyTuple.Create(w, fakeTick, false), 20);
                }
            }
        }

        internal void WeaponShootOff(object obj)
        {
            var gridAi = obj as GridAi;
            if (gridAi == null) return;

            foreach (var baseValuePair in gridAi.WeaponBase)
            {
                var comp = baseValuePair.Value;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];
                    w.StopReloadSound();
                    w.StopShooting();
                }
            }
        }

        internal void TurnWeaponShootOff(object ai)
        {
            var gridAi = ai as GridAi;
            if(gridAi == null) return;

            foreach (var basePair in gridAi.WeaponBase)
            {
                var comp = basePair.Value;
                if (comp?.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

                for (int i = 0; i < basePair.Value.Platform.Weapons.Length; i++)
                {
                    var wState = comp.State.Value.Weapons[basePair.Value.Platform.Weapons[i].WeaponId];

                    if (wState.ManualShoot == Weapon.TerminalActionState.ShootClick)
                    {
                        wState.ManualShoot = Weapon.TerminalActionState.ShootOff;
                        gridAi.ManualComps = gridAi.ManualComps - 1 > 0 ? gridAi.ManualComps - 1 : 0;
                        comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                    }

                }
            }
        }

        internal void ReturnHome(object o)
        {
            var weapon = o as Weapon;
            if (weapon == null) return;

            weapon.ReturnHome = weapon.Comp.ReturnHome = weapon.Comp.Ai.ReturnHome = true;
        }

        internal void PurgeAll()
        {
            FutureEvents.Purge();
            DsUtil.Purge();
            DsUtil2.Purge();

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

            _effectActive = false;
            RemoveEffectsFromGrid.Clear();
            WeaponAmmoPullQueue.Clear();
            DbsToUpdate.Clear();
            AmmoToPullQueue.Clear();
            Hits.Clear();
            AllArmorBaseDefinitions.Clear();
            HeavyArmorBaseDefinitions.Clear();
            AllArmorBaseDefinitions.Clear();
            LargeBlockSphereDb.Clear();
            SmallBlockSphereDb.Clear();
            GridToBlockTypeMap.Clear();
            AnimationsToProcess.Clear();
            _shrinking.ClearImmediate();
            _afterGlow.ClearImmediate();
            _shrinkPool.Clean();
            _subTypeIdToWeaponDefs.Clear();
            _weaponDefinitions.Clear();
            _slimsSortedList.Clear();
            _destroyedSlims.Clear();
            _slimsSet.Clear();
            _turretDefinitions.Clear();
            CompsToStart.ClearImmediate();
            GridEffectPool.Clean();
            GridEffectsPool.Clean();
            BlockTypePool.Clean();
            ConcurrentListPool.Clean();
            GridToFatMap.Clear();

            foreach (var structure in WeaponPlatforms.Values)
            {
                structure.WeaponSystems.Clear();
                structure.AmmoToWeaponIds.Clear();
            }
            WeaponPlatforms.Clear();
            weaponCoreBlockDefs.Clear();
            weaponCoreFixedBlockDefs.Clear();
            weaponCoreTurretBlockDefs.Clear();
            Projectiles.CheckPool.Clean();
            Projectiles.ShrapnelToSpawn.Clear();
            Projectiles.ShrapnelPool.Clean();
            Projectiles.FragmentPool.Clean();
            Projectiles.CheckPool.Clean();
            Projectiles.ProjectilePool.DeallocateAll();
            Projectiles.HitEntityPool.Clean();
            Projectiles.DrawProjectiles.Clear();
            Projectiles.CleanUp.Clear();
            Projectiles.TrajectilePool.DeallocateAll();

            _weaponDefinitions = null;
            Projectiles = null;
            TrackingAi = null;
            UiInput = null;
            TargetUi = null;
            Placer = null;
            WheelUi = null;
            DbsToUpdate = null;
            TargetGps = null;
            SApi.Unload();
            SApi = null;
            Api = null;
            ApiServer = null;
            AnimationsToProcess = null;

            ProjectileTree.Clear();
            ProjectileTree = null;
            GridTargetingAIs.Clear();
            AllDefinitions = null;
            SoundDefinitions = null;
            ActiveCockPit = null;
            ControlledEntity = null;
        }
    }
}