using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            Av.Glows.Clear();
            Av.AvShotPool.Clean();

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

            CompsToStart.ClearImmediate();
            CompsDelayed.Clear();
            CompReAdds.Clear();
            GridAiPool.Clean();

            foreach (var av in Av.AvShots)
            {
                av.GlowSteps.Clear();
                Av.AvShotPool.Return(av);
            }
            Av.AvShotPool.Clean();

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
