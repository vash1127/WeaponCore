using System.Runtime.InteropServices;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support { 
    public class PartAnimation
    {
        internal readonly double[] MoveSet;
        internal readonly double[] DistTravel;
        internal readonly double[] RevDistTravel;
        internal readonly double[][] DirVetors;
        internal readonly MatrixD[] RotationSet;
        internal readonly MatrixD[] RotCenterSet;
        internal readonly MatrixD Rotation;
        internal readonly MatrixD RotCenter;
        internal readonly int[][] MoveToSetIndexer;
        internal readonly int NumberOfMoves;
        internal readonly uint FireDelay;
        internal readonly MyEntity Part;
        internal readonly MyEntity MainEnt;
        internal readonly Vector3D CenterPoint;
        internal readonly bool DoesLoop;
        internal readonly bool DoesReverse;

        internal bool Reverse;
        internal bool PauseAnimation;

        private int currentMove;
        private double addToNext;

        internal int CurrentMove
        {
            get { return currentMove; }
        }

        internal PartAnimation(double[] moveSet, double[][] dirVectors, MatrixD[] rotationSet, MatrixD[] rotCeterSet, int[][] moveToSetIndexer, MyEntity part, MyEntity mainEnt, Vector3D centerPoint, uint fireDelay, MatrixD rotation, MatrixD rotCenter, bool loop = false, bool reverse = false)
        {
            MoveSet = moveSet;
            DirVetors = dirVectors;
            RotationSet = rotationSet;
            RotCenterSet = rotCeterSet;

            MoveToSetIndexer = moveToSetIndexer;
            NumberOfMoves = MoveToSetIndexer.Length;
            Part = part;
            MainEnt = mainEnt;
            CenterPoint = centerPoint;
            FireDelay = fireDelay;
            DoesLoop = loop;
            DoesReverse = reverse;
            currentMove = 0;
            Rotation = rotation;
            RotCenter = rotCenter;

            DistTravel = new double[NumberOfMoves];
            RevDistTravel = new double[NumberOfMoves];

            var traveled = 0d;
            for (int i = 0; i < NumberOfMoves; i++)
            {
                DistTravel[i] = traveled;
                traveled += moveSet[moveToSetIndexer[i][0]];
            }
            traveled = 0d;
            var count = 0;
            for (int i = NumberOfMoves - 1; i >= 0; i--)
            {
                RevDistTravel[count] = traveled;
                traveled += moveSet[moveToSetIndexer[i][0]];
                count++;
            }
        }

        internal Vector3D GetCurrentMove()
        {
            var currentVector = new double[]{0,0,0,0};
            double distRemaining;
            double totVectorLength = 0;
            double travel;

            if (Reverse)
            {
                travel = RevDistTravel[MoveToSetIndexer[currentMove][0]] + addToNext;
                for (int i = DirVetors.Length - 1; i >= 0; i--)
                {
                    currentVector = DirVetors[i];
                    
                    if (totVectorLength >= travel) break;
                    totVectorLength += DirVetors[i][0];
                }
            }
            else
            {
                travel = DistTravel[MoveToSetIndexer[currentMove][0]] + addToNext;
                for (int i = 0; i < DirVetors.Length; i++)
                {
                    currentVector = DirVetors[i];
                    if (totVectorLength >= travel) break;
                    totVectorLength += DirVetors[i][0];
                }
            }

            distRemaining = totVectorLength - travel;

            var change = MoveSet[MoveToSetIndexer[currentMove][0]] + addToNext;
            if (distRemaining < 0)
            {
                change = change + distRemaining;
                addToNext = MoveSet[MoveToSetIndexer[currentMove][0]] - change;
            }
            else
            {
                addToNext = 0;
            }


            var move = MatrixD.CreateTranslation(currentVector[1] * change, currentVector[2] * change, currentVector[3] * change) + RotationSet[MoveToSetIndexer[currentMove][1]] + RotCenterSet[MoveToSetIndexer[currentMove][2]];
            return move.Translation;
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
