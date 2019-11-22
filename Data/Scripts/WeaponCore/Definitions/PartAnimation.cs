using System;
using VRage;
using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support { 
    public class PartAnimation
    {
        internal readonly string AnimationId;
        internal readonly Matrix?[] RotationSet;
        internal readonly Matrix?[] RotCenterSet;
        internal readonly Session.AnimationType[] TypeSet;
        internal readonly int[] CurrentEmissivePart;
        internal readonly int[][] MoveToSetIndexer;
        internal readonly int NumberOfMoves;
        internal readonly uint FireDelay;
        internal readonly uint MotionDelay;
        internal readonly MyEntity MainEnt;
        internal readonly bool DoesLoop;
        internal readonly bool DoesReverse;
        internal readonly string Muzzle;
        internal readonly string SubpartId;

        internal enum indexer
        {
            MoveIndex,
            RotationIndex,
            RotCenterIndex,
            TypeIndex,
            EmissiveIndex,
        }

        internal struct EmissiveState
        {
            internal string[] EmissiveParts;
            internal int CurrentPart;
            internal Color CurrentColor;
            internal float CurrentIntensity;
            internal bool CycleParts;
            internal bool LeavePreviousOn;
        }

        internal WeaponSystem System;
        internal MyEntitySubpart Part;
        internal string[] RotCenterNameSet;
        internal bool Reverse;
        internal bool Looping;
        internal bool PauseAnimation;
        internal uint StartTick;

        private int _currentMove;
        private EmissiveState? LastEmissive;

        internal int CurrentMove
        {
            get { return _currentMove; }
        }

        internal PartAnimation(string animationId, Matrix?[] rotationSet, Matrix?[] rotCeterSet, Session.AnimationType[] typeSet, int[] currentEmissivePart, int[][] moveToSetIndexer, string subpartId, MyEntitySubpart part, MyEntity mainEnt, string muzzle, uint fireDelay, uint motionDelay, WeaponSystem system, bool loop = false, bool reverse = false)
        {
            RotationSet = rotationSet;
            RotCenterSet = rotCeterSet;
            CurrentEmissivePart = currentEmissivePart;
            AnimationId = animationId;

            TypeSet = typeSet;
            Muzzle = muzzle;
            MoveToSetIndexer = moveToSetIndexer;
            NumberOfMoves = MoveToSetIndexer.Length;
            Part = part;
            SubpartId = subpartId;
            System = system;

            MotionDelay = motionDelay;
            FireDelay = fireDelay;

            MainEnt = mainEnt;
            DoesLoop = loop;
            DoesReverse = reverse;
            _currentMove = 0;

        }

        
        internal void GetCurrentMove(out Vector3D translation, out MatrixD? rotation, out MatrixD? rotAroundCenter, out Session.AnimationType type, out EmissiveState? emissiveState)
        {
            type = TypeSet[MoveToSetIndexer[_currentMove][(int)indexer.TypeIndex]];
            var moveSet = System.WeaponLinearMoveSet[AnimationId];

            if (type == Session.AnimationType.Movement)
            {
                if (moveSet[MoveToSetIndexer[_currentMove][(int)indexer.MoveIndex]] != null)
                    translation = moveSet[MoveToSetIndexer[_currentMove][(int)indexer.MoveIndex]].Value.Translation;
                else
                    translation = Vector3D.Zero;

                rotation = RotationSet[MoveToSetIndexer[_currentMove][(int)indexer.RotationIndex]];
                rotAroundCenter = RotCenterSet[MoveToSetIndexer[_currentMove][(int)indexer.RotCenterIndex]];
                emissiveState = null;

                MyTuple<string[], Color, bool, bool, float>? emissive;
                System.WeaponEmissiveSet.TryGetValue(AnimationId + _currentMove, out emissive);

                if (emissive == null || emissive != null && LastEmissive != null && 
                    emissive.Value.Item1[CurrentEmissivePart[MoveToSetIndexer[_currentMove][(int)indexer.EmissiveIndex]]] ==
                    LastEmissive.Value.EmissiveParts[LastEmissive.Value.CurrentPart] &&
                    emissive.Value.Item2 == LastEmissive.Value.CurrentColor &&
                    emissive.Value.Item5 == LastEmissive.Value.CurrentIntensity)
                    emissiveState = null;
                else
                {
                    emissiveState = LastEmissive = new EmissiveState()
                    {
                        EmissiveParts = emissive.Value.Item1,
                        CurrentPart = CurrentEmissivePart[MoveToSetIndexer[_currentMove][(int)indexer.EmissiveIndex]],
                        CurrentColor = emissive.Value.Item2,
                        CurrentIntensity = emissive.Value.Item5,
                        CycleParts = emissive.Value.Item3,
                        LeavePreviousOn = emissive.Value.Item4
                    };
                }

            }
            else
            {
                translation = Vector3D.Zero;
                rotation = null;
                rotAroundCenter = null;

                MyTuple<string[], Color, bool, bool, float>? emissive;
                var key = AnimationId + _currentMove;
                System.WeaponEmissiveSet.TryGetValue(key, out emissive);

                if (emissive == null || emissive != null && LastEmissive != null &&
                    emissive.Value.Item1[CurrentEmissivePart[MoveToSetIndexer[_currentMove][(int)indexer.EmissiveIndex]]] ==
                    LastEmissive.Value.EmissiveParts[LastEmissive.Value.CurrentPart] &&
                    emissive.Value.Item2 == LastEmissive.Value.CurrentColor &&
                    emissive.Value.Item5 == LastEmissive.Value.CurrentIntensity)
                    emissiveState = null;
                else
                {
                    emissiveState = LastEmissive = new EmissiveState()
                    {
                        EmissiveParts = emissive.Value.Item1,
                        CurrentPart = CurrentEmissivePart[MoveToSetIndexer[_currentMove][(int)indexer.EmissiveIndex]],
                        CurrentColor = emissive.Value.Item2,
                        CurrentIntensity = emissive.Value.Item5,
                        CycleParts = emissive.Value.Item3,
                        LeavePreviousOn = emissive.Value.Item4
                    };
                }
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
            return Equals(Part, other.Part) && Equals(AnimationId, other.AnimationId);
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
            return (SubpartId != null ? SubpartId.GetHashCode() + AnimationId.GetHashCode() : 0);
        }
    }
}
