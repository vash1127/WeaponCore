using CoreSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRageMath;
using static CoreSystems.Support.SupportDefinition.SupportEffect;
using static CoreSystems.Support.SupportDefinition.SupportEffect.AffectedBlocks;
namespace CoreSystems.Platform
{
    public partial class SupportSys
    {
        internal void RefreshBlocks()
        {
            Comp.Session.DsUtil2.Start("");

            if (Box.Intersects(ref Comp.Ai.BlockChangeArea) && CubesInRange(true, System.Values.Effect.Affected))
            {
                ProcessBlockChanges(false, true);
            }
            else if (Box == BoundingBox.Invalid)
            {
                foreach (var block in _updatedBlocks)
                    _lostBlocks.Add(block);

                CubesInRange(false, System.Values.Effect.Affected);
                DetectBlockChanges();
            }

            LastBlockRefreshTick = CoreSystem.Session.Tick;
            Comp.Session.DsUtil2.Complete("", false, true);
        }

        public void DetectBlockChanges()
        {
            if (_updatedBlocks.Count == 0) return;

            _newBlocks.Clear();
            foreach (var block in _updatedBlocks)
            {
                _newBlocks.Add(block);
                _agedBlocks.Add(block);
            }

            _agedBlocks.IntersectWith(_lostBlocks);
            _lostBlocks.ExceptWith(_newBlocks);
            _newBlocks.ExceptWith(_agedBlocks);
            _agedBlocks.Clear();

            if (_newBlocks.Count != 0 || _lostBlocks.Count != 0)
                ProcessBlockChanges(false, true);
        }

        public void ProcessBlockChanges(bool clean = false, bool dupCheck = false)
        {
            foreach (var block in _newBlocks)
            {
                SuppotedBlocks.Add(block);
                
                BlockSupports blockSupports;
                if (!System.Session.ActiveSupports.TryGetValue(block, out blockSupports)) {
                    blockSupports = System.Session.BlockSupportsPool.Get();
                    blockSupports.AddSupport(this, block);
                }
                else blockSupports.AddSupport(this);

                if (ShowAffectedBlocks) {

                    MyCube myCube;
                    Comp.Cube.CubeGrid.TryGetCube(block.Position, out myCube);
                    BlockColorBackup.Add(block, new BlockBackup { MyCube = myCube, OriginalColor = block.ColorMaskHSV, OriginalSkin = block.SkinSubtypeId });
                }

            }
            _newBlocks.Clear();

            foreach (var block in _lostBlocks)
            {
                SuppotedBlocks.Remove(block);

                BlockSupports blockSupports;
                if (System.Session.ActiveSupports.TryGetValue(block, out blockSupports))
                {
                   if (!blockSupports.RemoveSupport(this))
                       Log.Line($"failed to remove support");
                }
                else Log.Line($"support manager failed to get blockSupport");

                if (ShowAffectedBlocks)
                {
                    BlockBackup backup;
                    if (!block.IsDestroyed && BlockColorBackup.TryGetValue(block, out backup)) {
                        BlockColorBackup.Remove(block);
                        Comp.Cube.CubeGrid.ChangeColorAndSkin(backup.MyCube.CubeBlock, backup.OriginalColor, backup.OriginalSkin);
                    }
                }
            }
            if (ShowAffectedBlocks && BlockColorBackup.Count == 0)
            {
                CoreSystem.Session.DisplayAffectedArmor.Remove(this);
                ShowAffectedBlocks = false;
            }

            _lostBlocks.Clear();

        }

        public bool CubesInRange(bool update, AffectedBlocks types = Both)
        {
            var cube = Comp.Cube;
            var next = cube.Position;
            var grid = cube.CubeGrid;

            var cubeDistance = System.Values.Effect.BlockRange;
            var min = cube.Min - cubeDistance;
            var max = cube.Max + cubeDistance;
            var gridMin = grid.Min;
            var gridMax = grid.Max;

            Vector3I.Max(ref min, ref gridMin, out min);
            Vector3I.Min(ref max, ref gridMax, out max);

            Min = min;
            Max = max;

            Box = new BoundingBox(min, max);
            Box.Min *= grid.GridSize;
            Box.Max *= grid.GridSize;

            var addedBlocks = Comp.Ai.AddedBlockPositions;
            var removedBlocks = Comp.Ai.RemovedBlockPositions;

            var iter = new Vector3I_RangeIterator(ref Min, ref Max);
            while (iter.IsValid())
            {
                if (update)
                {
                    IMySlimBlock slim;
                    if (addedBlocks.TryGetValue(next, out slim) && !slim.IsDestroyed)
                    {
                        if (types == NonLogic && slim.FatBlock == null)
                            _newBlocks.Add(slim);
                        else if (types == Logic && slim.FatBlock != null)
                            _newBlocks.Add(slim);
                        else if (types == Both)
                            _newBlocks.Add(slim);
                    }
                    else if (removedBlocks.TryGetValue(next, out slim))
                    {
                        if (types == NonLogic && slim.FatBlock == null)
                            _lostBlocks.Add(slim);
                        else if (types == Logic && slim.FatBlock != null)
                            _lostBlocks.Add(slim);
                        else if (types == Both)
                            _lostBlocks.Add(slim);

                    }
                }
                else
                {
                    MyCube myCube;
                    if (grid.TryGetCube(next, out myCube) && myCube.CubeBlock != cube.SlimBlock)
                    {

                        var slim = (IMySlimBlock)myCube.CubeBlock;

                        if (next == slim.Position && !slim.IsDestroyed)
                        {

                            if (types == NonLogic && slim.FatBlock == null)
                                _updatedBlocks.Add(slim);
                            else if (types == Logic && !slim.IsDestroyed)
                                _updatedBlocks.Add(slim);
                            else if (types == Both)
                                _updatedBlocks.Add(slim);
                        }
                    }
                }

                iter.GetNext(out next);
            }

            if (!update)
                Log.Line($"new adds: {_updatedBlocks.Count}");
            else Log.Line($"update: adds:{_newBlocks.Count} - removes:{_lostBlocks.Count}");


            return !update && _updatedBlocks.Count > 0 || _newBlocks.Count > 0 || _lostBlocks.Count > 0;
        }
    }
}
