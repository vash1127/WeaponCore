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

        internal bool Reverse;
        internal bool PauseAnimation;
        internal uint StartTick;

        private int currentMove;
        private double addToNext;

        internal int CurrentMove
        {
            get { return currentMove; }
        }

        internal PartAnimation(Vector3?[] moveSet, MatrixD?[] rotationSet, MatrixD?[] rotCeterSet, int[][] moveToSetIndexer, MyEntitySubpart part, MyEntity mainEnt, Vector3D centerPoint, uint fireDelay, uint motionDelay, bool loop = false, bool reverse = false)
        {
            MoveSet = moveSet;
            RotationSet = rotationSet;
            RotCenterSet = rotCeterSet;

            MoveToSetIndexer = moveToSetIndexer;
            NumberOfMoves = MoveToSetIndexer.Length;
            Part = part;

            MainEnt = mainEnt;
            CenterPoint = centerPoint;
            FireDelay = fireDelay;
            MotionDelay = motionDelay;
            DoesLoop = loop;
            DoesReverse = reverse;
            currentMove = 0;
        }
        
        internal void GetCurrentMove(out Vector3D translation, out MatrixD? rotation, out bool delay)
        {
            if (MoveSet[MoveToSetIndexer[currentMove][0]] != null || RotationSet[MoveToSetIndexer[currentMove][1]] != null)
            {
                var move = MatrixD.CreateTranslation((Vector3)MoveSet[MoveToSetIndexer[currentMove][0]]);
                translation = move.Translation;
                rotation = RotationSet[MoveToSetIndexer[currentMove][1]];
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
                currentMove = currentMove + 1 <= NumberOfMoves - 1 ? currentMove + 1 : 0;
                return currentMove;
            }

            return currentMove + 1 <= NumberOfMoves - 1 ? currentMove + 1 : 0;
        }

        internal int Previous(bool dec = true)
        {
            if (dec)
            {
                currentMove = currentMove - 1 >= 0 ? currentMove - 1 : NumberOfMoves - 1;
                return currentMove;
            }

            return currentMove - 1 >= 0 ? currentMove - 1 : NumberOfMoves - 1; 
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
