using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.SupportDefinition.SupportEffect;
namespace CoreSystems.Platform
{
    internal class BlockSupports
    {
        internal readonly Dictionary<MyStringHash, SupportSys> Supports = new Dictionary<MyStringHash, SupportSys>(MyStringHash.Comparer);
        internal SupportInfo Info;
        internal IMySlimBlock Block;

        internal bool AddSupport(SupportSys supportSys, IMySlimBlock slim = null)
        {
            if (slim != null && Block != null)
                Log.Line("AddSupport already had block");

            var validInit = true;
            if (slim != null) {
                Block = slim;
                validInit = supportSys.System.Session.ActiveSupports.TryAdd(Block, this);
            }
            Supports.Add(supportSys.PartHash, supportSys);
            var success = validInit;

            if (!success)
                Log.Line($"AddSupport failed");
            else
                UpdateBlockSupportState(supportSys, true);

            return success;
        }

        internal bool RemoveSupport(SupportSys supportSys)
        {
            var removed = Supports.Remove(supportSys.PartHash);
            if (removed)
            {
                if (Supports.Count == 0)
                    Clean(supportSys, true);
                else UpdateBlockSupportState(supportSys, false);
            }

            return removed;
        }

        internal void RecomputeFullState()
        {
            var reset = true;
            foreach (var s in Supports.Values)
            {
                UpdateBlockSupportState(s, true, reset);
                reset = false;
            }
        }

        private void UpdateBlockSupportState(SupportSys supportSys, bool add, bool reset = false)
        {
            if (reset)
                Info.Clear();

            if (add)
            {
                Info.KineticProt += supportSys.Info.KineticProt;
                Info.EnergyProt += supportSys.Info.EnergyProt;
            }
            else
            {
                Info.KineticProt -= supportSys.Info.KineticProt;
                Info.EnergyProt -= supportSys.Info.EnergyProt;
            }
        }

        private void Clean(SupportSys support, bool skipRemove = false)
        {
            support.System.Session.BlockSupportsPool.Return(this);
            Supports.Clear();
            BlockSupports oldSupport;
            var removed = skipRemove || support.System.Session.ActiveSupports.TryRemove(Block, out oldSupport);
            Block = null;
            if (!removed) Log.Line($"cleaning up BlockSupport failed");
        }
    }

    internal struct BlockBackup
    {
        internal MyCube MyCube;
        internal Vector3 OriginalColor;
        internal MyStringHash OriginalSkin;
    }

    internal class SupportInfo
    {
        internal readonly Protections Type;
        internal readonly bool NonLogic;
        internal readonly bool Logic;
        private readonly int _maxPoints;
        private readonly int _pointsPerCharge;
        private readonly int[] _runningTotal = new int[60];

        internal bool Idle;
        internal float KineticProt;
        internal float EnergyProt;
        internal int CurrentPoints;

        private int _usedThisSecond;
        private int _usedLastMinute;
        private int _timeStep;
        private int _lastStep = 59;
        private int _idleTime;

        internal SupportInfo(SupportSys supportSys)
        {
            Type = supportSys.System.Values.Effect.Protection;
            _pointsPerCharge = supportSys.System.Values.Effect.PointsPerCharge;
            _maxPoints = supportSys.System.Values.Effect.MaxPoints;

            switch (supportSys.System.Values.Effect.Affected)
            {
                case AffectedBlocks.Logic:
                    Logic = true;
                    break;
                case AffectedBlocks.NonLogic:
                    NonLogic = true;
                    break; 
                default:
                    NonLogic = true;
                    Logic = true;
                    break;
            }
        }

        internal void Update(int charges)
        {
            CurrentPoints = MathHelper.Clamp(CurrentPoints + (charges * _pointsPerCharge), 0, _maxPoints);
            _usedLastMinute += _usedThisSecond;

            if (_usedThisSecond > 0 || CurrentPoints < _maxPoints) {
                _idleTime = 0;
                Idle = false;
            }
            else if (++_idleTime > 59) {
                Idle = true;
            }

            if (_timeStep > _lastStep) {
                _lastStep = _timeStep;
                _usedLastMinute -= _runningTotal[_lastStep];
            }

            if (_timeStep < 59) {
                _runningTotal[_timeStep++] = _usedThisSecond;
            }
            else {
                _timeStep = 0;
                _runningTotal[_timeStep] = _usedThisSecond;
            }

            _usedThisSecond = 0;
        }

        internal void Clear()
        {

        }
    }
}
