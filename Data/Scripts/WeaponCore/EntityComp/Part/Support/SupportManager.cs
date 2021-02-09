using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Utils;
using VRage.Game.VisualScripting;
using VRageMath;
using WeaponCore.Support;
using MyVisualScriptLogicProvider = Sandbox.Game.MyVisualScriptLogicProvider;

namespace WeaponCore.Platform
{
    public partial class SupportSys : Part
    {
        internal void RefreshBlocks(bool fullUpdate = false)
        {
            if (Box2 == BoundingBox.Invalid) {
                fullUpdate = true;
                PrepArea();
            }

            if (fullUpdate || Box2.Intersects(ref Comp.Ai.BlockChangeArea))
            {
                Comp.Session.DsUtil2.Start("");

                if (fullUpdate) 
                {
                    foreach (var block in _updatedBlocks)
                        _lostBlocks.Add(block);

                    GetCubesInRange(Session.CubeTypes.Slims);
                    DetectBlockChanges();
                }
                else if (UpdateCubesInRange(Session.CubeTypes.Slims))
                    ProcessBlockChanges(false, true);

                Comp.Session.DsUtil2.Complete("", false, true);
                Log.Line($"FullUpdate? {fullUpdate} - {SuppotedBlocks.Count}");

            }
            LastBlockRefreshTick = CoreSystem.Session.Tick;
            
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

        public void GetCubesInRange(Session.CubeTypes types = Session.CubeTypes.All)
        {
            _updatedBlocks.Clear();

            var cube = Comp.Cube;
            var next = cube.Position;
            var grid = cube.CubeGrid;
            var iter = new Vector3I_RangeIterator(ref Min, ref Max);
            while (iter.IsValid()) {

                MyCube myCube;
                if (grid.TryGetCube(next, out myCube) && myCube.CubeBlock != cube.SlimBlock) {

                    var slim = (IMySlimBlock)myCube.CubeBlock;

                    if (next == slim.Position) {

                        if (types == Session.CubeTypes.Slims && !slim.IsDestroyed && slim.FatBlock == null)
                            _updatedBlocks.Add(slim);
                        else if (types == Session.CubeTypes.Fats && slim.FatBlock != null && !slim.IsDestroyed)
                            _updatedBlocks.Add(slim);
                        else if (types == Session.CubeTypes.All && !slim.IsDestroyed)
                            _updatedBlocks.Add(slim);
                    }
                }
                iter.GetNext(out next);
            }
        }

        public bool UpdateCubesInRange(Session.CubeTypes types = Session.CubeTypes.All)
        {
            var cube = Comp.Cube;
            var next = cube.Position;
            var iter = new Vector3I_RangeIterator(ref Min, ref Max);
            var addedBlocks = Comp.Ai.AddedBlockPositions;
            var removedBlocks = Comp.Ai.RemovedBlockPositions;

            while (iter.IsValid())
            {
                IMySlimBlock slim;
                if (addedBlocks.TryGetValue(next, out slim) && !slim.IsDestroyed)
                {
                    if (types == Session.CubeTypes.Slims && slim.FatBlock == null)
                        _newBlocks.Add(slim);
                    else if (types == Session.CubeTypes.Fats && slim.FatBlock != null)
                        _newBlocks.Add(slim);
                    else if (types == Session.CubeTypes.All)
                        _newBlocks.Add(slim);
                }
                else if (removedBlocks.TryGetValue(next, out slim))
                {
                    if (types == Session.CubeTypes.Slims && slim.FatBlock == null)
                        _lostBlocks.Add(slim);
                    else if (types == Session.CubeTypes.Fats && slim.FatBlock != null)
                        _lostBlocks.Add(slim);
                    else if (types == Session.CubeTypes.All)
                        _lostBlocks.Add(slim);

                }
                iter.GetNext(out next);
            }
            return _newBlocks.Count > 0 || _lostBlocks.Count > 0;
        }

        private void PrepArea()
        {
            var min = Comp.Cube.Min - CubeDistance;
            var max = Comp.Cube.Max + CubeDistance;
            var gridMin = Comp.Cube.CubeGrid.Min;
            var gridMax = Comp.Cube.CubeGrid.Max;

            Vector3I.Max(ref min, ref gridMin, out min);
            Vector3I.Min(ref max, ref gridMax, out max);

            Min = min;
            Max = max;

            Box2 = new BoundingBox(min, max);
            Box2.Min *= Comp.Cube.CubeGrid.GridSize;
            Box2.Max *= Comp.Cube.CubeGrid.GridSize;
        }

    }
}
