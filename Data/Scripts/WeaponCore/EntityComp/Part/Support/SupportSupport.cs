using System.Threading;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class SupportSys : Part
    {
        internal void RefreshBlocks()
        {
            foreach (var block in _updatedBlocks)
                _lostBlocks.Add(block);

            Session.GetCubesInRange(BaseComp.Cube.CubeGrid, BaseComp.Cube, 2, _updatedBlocks, out Min, out Max, Session.CubeTypes.Slims);
            Box = new BoundingBoxI(Min, Max);

            LastBlockRefreshTick = System.Session.Tick;
            DetectBlockChanges();
        }

        internal void ToggleAreaEffectDisplay()
        {
            var grid = BaseComp.Cube.CubeGrid;
            if (!ShowAffectedBlocks) {

                ShowAffectedBlocks = true;
                foreach (var myCube in SuppotedBlocks)
                    _blockColorBackup.Add(myCube, ((IMySlimBlock)myCube.CubeBlock).ColorMaskHSV);

                System.Session.DisplayAffectedArmor.Add(this);
            }
            else {

                foreach (var pair in _blockColorBackup)
                    grid.ChangeColorAndSkin(pair.Key.CubeBlock, pair.Value);

                _blockColorBackup.Clear();
                System.Session.DisplayAffectedArmor.Remove(this);
                ShowAffectedBlocks = false;
            }
        }

        public void DetectBlockChanges()
        {
            Comp.Session.DsUtil2.Start("");
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
            Comp.Session.DsUtil2.Complete("", false, true);
        }

        public void ProcessBlockChanges(bool clean = false, bool dupCheck = false)
        {
            foreach (var block in _newBlocks)
            {
                var slim = (IMySlimBlock)block.CubeBlock;
                SuppotedBlocks.Add(block);
            }
            _newBlocks.Clear();

            foreach (var block in _lostBlocks)
            {
                var slim = (IMySlimBlock)block.CubeBlock;
                SuppotedBlocks.Remove(block);
            }
            _lostBlocks.Clear();

        }
    }
}
