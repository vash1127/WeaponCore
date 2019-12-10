﻿using System;
using VRage;
using VRage.Game.Entity;
using VRageMath;
using static WeaponCore.Session;

namespace WeaponCore.Support { 
    public class PartAnimation
    {
        internal readonly string AnimationId;
        internal readonly Matrix[] RotationSet;
        internal readonly Matrix[] RotCenterSet;
        internal readonly Matrix FinalPos;
        internal readonly Matrix HomePos;
        internal readonly AnimationType[] TypeSet;
        internal readonly int[] CurrentEmissivePart;
        internal readonly int[][] MoveToSetIndexer;
        internal readonly int NumberOfMoves;
        internal readonly uint FireDelay;
        internal readonly uint MotionDelay;
        internal readonly MyEntity MainEnt;
        internal readonly bool DoesLoop;
        internal readonly bool DoesReverse;
        internal readonly bool TriggerOnce;
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

        public struct EmissiveState
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
        internal bool Running;
        internal bool Triggered;
        internal uint StartTick;

        private int _currentMove;
        private EmissiveState LastEmissive;
        private string _uid;

        internal int CurrentMove
        {
            get { return _currentMove; }
        }

        internal PartAnimation(string animationId, Matrix[] rotationSet, Matrix[] rotCeterSet, AnimationType[] typeSet, int[] currentEmissivePart, int[][] moveToSetIndexer, string subpartId, MyEntitySubpart part, MyEntity mainEnt, string muzzle, uint fireDelay, uint motionDelay, WeaponSystem system, bool loop = false, bool reverse = false, bool triggerOnce = false)
        {
            RotationSet = rotationSet;
            RotCenterSet = rotCeterSet;
            CurrentEmissivePart = currentEmissivePart;
            AnimationId = animationId;

            //Unique Animation ID
            Guid guid = Guid.NewGuid();
            _uid = Convert.ToBase64String(guid.ToByteArray());

            TypeSet = typeSet;
            Muzzle = muzzle;
            MoveToSetIndexer = moveToSetIndexer;
            NumberOfMoves = MoveToSetIndexer.Length;
            Part = part;
            System = system;
            SubpartId = subpartId;

            MotionDelay = motionDelay;
            FireDelay = fireDelay;

            MainEnt = mainEnt;
            DoesLoop = loop;
            DoesReverse = reverse;
            TriggerOnce = triggerOnce;
            _currentMove = 0;

            if (part != null)
            {                
                FinalPos = HomePos = part.PositionComp.LocalMatrix;

                for (int i = 0; i < NumberOfMoves; i++)
                {
                    Matrix rotation;
                    Matrix rotAroundCenter;
                    Vector3D translation;
                    AnimationType animationType;
                    EmissiveState currentEmissive;
                    GetCurrentMove(out translation, out rotation, out rotAroundCenter, out animationType, out currentEmissive);

                    if (animationType == AnimationType.Movement) FinalPos.Translation  += translation;

                    if (rotation != Matrix.Zero) FinalPos *= rotation;

                    if (rotAroundCenter != Matrix.Zero) FinalPos *= rotAroundCenter;

                    Next();
                }
                Reset();
            }

        }

        
        internal void GetCurrentMove(out Vector3D translation, out Matrix rotation, out Matrix rotAroundCenter, out AnimationType type, out EmissiveState emissiveState)
        {
            type = TypeSet[MoveToSetIndexer[_currentMove][(int)indexer.TypeIndex]];
            var moveSet = System.WeaponLinearMoveSet[AnimationId];

            if (type == AnimationType.Movement)
            {
                if (moveSet[MoveToSetIndexer[_currentMove][(int)indexer.MoveIndex]] != Matrix.Zero)
                    translation = moveSet[MoveToSetIndexer[_currentMove][(int)indexer.MoveIndex]].Translation;
                else
                    translation = Vector3D.Zero;

                rotation = RotationSet[MoveToSetIndexer[_currentMove][(int)indexer.RotationIndex]];
                rotAroundCenter = RotCenterSet[MoveToSetIndexer[_currentMove][(int)indexer.RotCenterIndex]];

            }
            else
            {
                translation = Vector3D.Zero;
                rotation = Matrix.Zero;
                rotAroundCenter = Matrix.Zero;
            }

            if (System.WeaponEmissiveSet.TryGetValue(AnimationId + _currentMove, out emissiveState))
            {
                emissiveState.CurrentPart = CurrentEmissivePart[MoveToSetIndexer[_currentMove][(int)indexer.EmissiveIndex]];

                if (emissiveState.EmissiveParts != null && LastEmissive.EmissiveParts != null && emissiveState.CurrentPart == LastEmissive.CurrentPart && emissiveState.CurrentColor == LastEmissive.CurrentColor && Math.Abs(emissiveState.CurrentIntensity - LastEmissive.CurrentIntensity) < 0.001)
                    emissiveState = new EmissiveState();

                LastEmissive = emissiveState;

            }
            else
                emissiveState = LastEmissive = new EmissiveState();

        }

        internal int Next(bool inc = true)
        {
            if (inc)
            {
                _currentMove = _currentMove + 1 < NumberOfMoves ? _currentMove + 1 : 0;
                return _currentMove;
            }

            return _currentMove + 1 < NumberOfMoves ? _currentMove + 1 : 0;
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

        internal void Reset(bool reverse = false, bool resetPos = true, bool resetMove = true)
        {
            Looping = false;
            Reverse = reverse;
            LastEmissive = new EmissiveState();
            PauseAnimation = false;

            if (resetMove) _currentMove = 0;
            if (resetPos) Part.PositionComp.LocalMatrix = HomePos;
            
        }

        protected bool Equals(PartAnimation other)
        {
            return Equals(_uid, other._uid);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PartAnimation)obj);
        }

        public override int GetHashCode()
        {
            return _uid.GetHashCode();
        }
    }
}
