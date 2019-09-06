using System.Runtime.InteropServices;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support { 
    public class PartAnimation
    {
        internal readonly Vector3?[] MoveSet;
        internal readonly MatrixD?[] RotationSet;
        internal readonly MatrixD?[] RotCenterSet;
        internal readonly int[][] MoveToSetIndexer;
        internal readonly int NumberOfMoves;
        internal readonly uint FireDelay;
        internal readonly uint MotionDelay;
        internal readonly MyEntitySubpart Part;
        internal readonly MyEntity MainEnt;
        internal readonly Vector3D CenterPoint;
        internal readonly bool DoesLoop;
        internal readonly bool DoesReverse;
        internal readonly string Muzzle;

        internal bool Reverse;
        internal bool Looping;
        internal bool PauseAnimation;
        internal uint StartTick;

        private int _currentMove;

        internal int CurrentMove
        {
            get { return _currentMove; }
        }

        internal PartAnimation(Vector3?[] moveSet, MatrixD?[] rotationSet, MatrixD?[] rotCeterSet, int[][] moveToSetIndexer, MyEntitySubpart part, MyEntity mainEnt, Vector3D centerPoint, string muzzle, uint fireDelay, uint motionDelay, bool loop = false, bool reverse = false)
        {
            MoveSet = moveSet;
            RotationSet = rotationSet;
            RotCenterSet = rotCeterSet;
            Muzzle = muzzle;
            MoveToSetIndexer = moveToSetIndexer;
            NumberOfMoves = MoveToSetIndexer.Length;
            Part = part;

            MainEnt = mainEnt;
            CenterPoint = centerPoint;
            FireDelay = fireDelay;
            MotionDelay = motionDelay;
            DoesLoop = loop;
            DoesReverse = reverse;
            _currentMove = 0;
        }
        
        internal void GetCurrentMove(out Vector3D translation, out MatrixD? rotation, out bool delay)
        {
            if (MoveSet[MoveToSetIndexer[_currentMove][0]] != null || RotationSet[MoveToSetIndexer[_currentMove][1]] != null)
            {
                var move = MatrixD.CreateTranslation((Vector3)MoveSet[MoveToSetIndexer[_currentMove][0]]);
                translation = move.Translation;
                rotation = RotationSet[MoveToSetIndexer[_currentMove][1]];
                delay = false;
            }
            else
            {
                translation = Vector3D.Zero;
                rotation = MatrixD.Zero;
                delay = true;
            }

        }

        internal int Next(bool inc = true)
        {
            if (inc)
            {
                _currentMove = _currentMove + 1 <= NumberOfMoves - 1 ? _currentMove + 1 : 0;
                return _currentMove;
            }

            return _currentMove + 1 <= NumberOfMoves - 1 ? _currentMove + 1 : 0;
        }

        internal int Previous(bool dec = true)
        {
            if (dec)
            {
                _currentMove = _currentMove - 1 >= 0 ? _currentMove - 1 : NumberOfMoves - 1;
                return _currentMove;
            }

            return _currentMove - 1 >= 0 ? _currentMove - 1 : NumberOfMoves - 1; 
        }

        protected bool Equals(PartAnimation other)
        {
            return Equals(Part, other.Part);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PartAnimation)obj);
        }

        public override int GetHashCode()
        {
            return (Part != null ? Part.GetHashCode() : 0);
        }
    }
}
