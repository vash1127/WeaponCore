using System;
using VRage.Collections;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {

        private void DeferedUpBlockTypeCleanUp(bool force = false)
        {
            foreach (var clean in BlockTypeCleanUp)
            {
                if (force || Tick - clean.RequestTick > 120)
                {
                    foreach (var item in clean.Collection)
                        ConcurrentListPool.Return(item.Value);
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
            PurgeTerminalSystem();
            HudUi.Purge();
            TerminalMon.Purge();
            foreach (var reports in Reporter.ReportData.Values)
            {
                foreach (var report in reports)
                {
                    report.Clean();
                    Reporter.ReportPool.Return(report);
                }
                reports.Clear();
            }
            Reporter.ReportData.Clear();
            Reporter.ReportPool.Clean();

            PacketsToClient.Clear();
            PacketsToServer.Clear();

            foreach (var suit in (PacketType[]) Enum.GetValues(typeof(PacketType)))
            {
                foreach (var pool in PacketPools.Values)
                    pool.Clean();
                PacketPools.Clear();
            }

            AcqManager.Clean();

            foreach (var e in Emitters)
                e.Cleanup();
            Emitters.Clear();

            SoundPairs.Clear();

            foreach (var item in EffectedCubes)
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
                EffectedCubes.Remove(_effectPurge.Dequeue());
            }

            Av.Glows.Clear();
            Av.AvShotPool.Clean();

            DeferedUpBlockTypeCleanUp(true);
            BlockTypeCleanUp.Clear();

            foreach (var map in GridToFatMap.Keys)
                RemoveGridFromMap(map);

            GridToFatMap.Clear();
            FatMapPool.Clean();

            DirtyGridsTmp.Clear();

            foreach (var structure in WeaponPlatforms.Values)
            {
                foreach (var system in structure.WeaponSystems)
                    foreach (var ammo in system.Value.AmmoTypes)
                        ammo.AmmoDef.Const.PrimeEntityPool?.Clean();

                structure.WeaponSystems.Clear();
            }
            WeaponPlatforms.Clear();

            foreach (var gridToMap in GridToBlockTypeMap)
            {
                foreach (var map in gridToMap.Value)
                {
                    ConcurrentListPool.Return(map.Value);
                }
                gridToMap.Value.Clear();
                BlockTypePool.Return(gridToMap.Value);
            }
            GridToBlockTypeMap.Clear();

            foreach(var playerGrids in PlayerEntityIdInRange)
                playerGrids.Value.Clear();

            PlayerEntityIdInRange.Clear();
            DirtyGrids.Clear();

            DsUtil.Purge();
            DsUtil2.Purge();

            _effectActive = false;
            ShootingWeapons.Clear();
            RemoveEffectsFromGrid.Clear();
            WeaponToPullAmmo.Clear();
            WeaponToPullAmmoIndexer.Clear();
            AmmoToPullQueue.ClearList();
            AmmoToPullQueue.ApplyChanges();
            ChargingWeaponsIndexer.Clear();
            WeaponToPullAmmoIndexer.Clear();
            WeaponsToRemoveAmmoIndexer.Clear();
            ChargingWeapons.Clear();
            WeaponsToRemoveAmmo.Clear();
            Hits.Clear();
            Emitters.Clear();
            SoundPairs.Clear();
            GridToMasterAi.Clear();
            Players.Clear();

            AllArmorBaseDefinitions.Clear();
            HeavyArmorBaseDefinitions.Clear();
            AllArmorBaseDefinitions.Clear();
            AcquireTargets.Clear();
            LargeBlockSphereDb.Clear();
            SmallBlockSphereDb.Clear();
            AnimationsToProcess.Clear();
            _subTypeIdToWeaponDefs.Clear();
            WeaponDefinitions.Clear();
            SlimsSortedList.Clear();
            _destroyedSlims.Clear();
            _destroyedSlimsClient.Clear();
            _slimHealthClient.Clear();
            _slimsSet.Clear();
            _turretDefinitions.Clear();

            foreach (var comp in CompsToStart)
                if (comp?.Platform != null)
                    CloseComps(comp.MyCube);

            foreach (var readd in CompReAdds)
            {
                if (!readd.Ai.Closed) readd.Ai.GridForceClose();
                if (readd.Comp?.Platform != null) {
                    CloseComps(readd.Comp.MyCube);
                }
            }

            foreach (var comp in CompsDelayed) {
                if (comp?.Platform != null)
                    CloseComps(comp.MyCube);
            }

            foreach (var gridAi in DelayedGridAiClean) {
                if (!gridAi.Closed)
                    gridAi.GridForceClose();
            }

            PlatFormPool.Clean();
            CompsToStart.ClearImmediate();
            DelayedGridAiClean.ClearImmediate();

            CompsDelayed.Clear();
            CompReAdds.Clear();
            GridAiPool.Clean();
            
            Av.RipMap.Clear();
            foreach (var mess in Av.KeensBrokenParticles)
                Av.KeenMessPool.Return(mess);
            
            Av.KeensBrokenParticles.Clear();

            foreach (var av in Av.AvShots) {
                av.GlowSteps.Clear();
                Av.AvShotPool.Return(av);
            }
            Av.AvShotPool.Clean();
            Av.AvBarrels1.Clear();
            Av.AvBarrels2.Clear();
            Av.AvShots.Clear();
            Av.HitSounds.Clear();

            foreach (var errorpkt in ClientSideErrorPkt)
                errorpkt.Packet.CleanUp();
            ClientSideErrorPkt.Clear();

            GridEffectPool.Clean();
            GridEffectsPool.Clean();
            BlockTypePool.Clean();
            ConcurrentListPool.Clean();

            GroupInfoPool.Clean();
            TargetInfoPool.Clean();
            PacketObjPool.Clean();

            InventoryMoveRequestPool.Clean();
            WeaponCoreBlockDefs.Clear();
            VanillaIds.Clear();
            VanillaCoreIds.Clear();
            WeaponCoreFixedBlockDefs.Clear();
            WeaponCoreTurretBlockDefs.Clear();
            VoxelCaches.Clear();
            ArmorCubes.Clear();

            foreach (var p in Projectiles.ProjectilePool)
                p.Info?.AvShot?.AmmoEffect?.Stop();

            Projectiles.ShrapnelToSpawn.Clear();
            Projectiles.ShrapnelPool.Clean();
            Projectiles.FragmentPool.Clean();
            Projectiles.ActiveProjetiles.Clear();
            Projectiles.ProjectilePool.Clear();
            Projectiles.HitEntityPool.Clean();
            Projectiles.VirtInfoPool.Clean();

            DbsToUpdate.Clear();
            GridTargetingAIs.Clear();
            GridToMasterAi.Clear();

            DsUtil = null;
            DsUtil2 = null;
            SlimsSortedList = null;
            Enforced = null;
            StallReporter = null;
            TerminalMon = null;
            Proccessor = null;
            Physics = null;
            Camera = null;
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
            Reporter = null;
            WeaponDefinitions = null;
            AnimationsToProcess = null;
            ProjectileTree.Clear();
            ProjectileTree = null;
            Av = null;
            HudUi = null;
            AllDefinitions = null;
            SoundDefinitions = null;
            ActiveCockPit = null;
            ActiveControlBlock = null;
            ControlledEntity = null;
            TmpStorage = null;
        }
    }
}
