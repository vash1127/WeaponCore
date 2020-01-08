using System;
using System.Collections.Generic;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Session;

namespace WeaponCore.Support { 
    public class PartAnimation
    {
        internal readonly string AnimationId;
        internal readonly EventTriggers EventTrigger;
        internal readonly Matrix[] RotationSet;
        internal readonly Matrix[] RotCenterSet;
        internal readonly Matrix FinalPos;
        internal readonly Matrix HomePos;
        internal readonly AnimationType[] TypeSet;
        internal readonly Dictionary<EventTriggers, string> EventIdLookup = new Dictionary<EventTriggers, string>();
        internal readonly WeaponSystem System;
        internal readonly int[] CurrentEmissivePart;
        internal readonly int[][] MoveToSetIndexer;
        internal readonly int NumberOfMoves;
        internal readonly uint MotionDelay;
        internal readonly bool DoesLoop;
        internal readonly bool DoesReverse;
        internal readonly bool TriggerOnce;
        internal readonly bool HasMovement;
        internal readonly bool MovesPivotPos;
        internal readonly bool ResetEmissives;
        internal readonly string Muzzle;
        internal readonly string SubpartId;
        internal readonly string[] EmissiveIds;
        internal readonly string[] EmissiveParts;

        internal enum indexer
        {
            MoveIndex,
            RotationIndex,
            RotCenterIndex,
            TypeIndex,
            EmissiveIndex,
            EmissivePartIndex,
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

        internal MyEntity MainEnt;
        internal MyEntitySubpart Part;
        internal string[] RotCenterNameSet;
        internal bool Reverse;
        internal bool Looping;
        internal bool Running;
        internal bool Triggered;
        internal bool CanPlay;
        internal uint StartTick;

        private int _currentMove;
        private EmissiveState LastEmissive;
        private string _uid;

        internal int CurrentMove
        {
            get { return _currentMove; }
        }

        internal PartAnimation(EventTriggers eventTrigger, string animationId, Matrix[] rotationSet, Matrix[] rotCeterSet, AnimationType[] typeSet,string[] emissiveIds, int[] currentEmissivePart, int[][] moveToSetIndexer, string subpartId, MyEntitySubpart part, MyEntity mainEnt, string muzzle, uint motionDelay, WeaponSystem system, bool loop = false, bool reverse = false, bool triggerOnce = false, bool resetEmissives = false)
        {
            EventTrigger = eventTrigger;
            RotationSet = rotationSet;
            RotCenterSet = rotCeterSet;
            CurrentEmissivePart = currentEmissivePart;
            AnimationId = animationId;
            ResetEmissives = resetEmissives;
            EmissiveIds = emissiveIds;

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
            MainEnt = mainEnt;
            DoesLoop = loop;
            DoesReverse = reverse;
            TriggerOnce = triggerOnce;
            _currentMove = 0;

            if (part != null)
            {                
                FinalPos = HomePos = part.PositionComp.LocalMatrix;
                var emissivePartCheck = new HashSet<string>();
                var emissiveParts = new List<string>();
                for (int i = 0; i < NumberOfMoves; i++)
                {
                    Matrix rotation;
                    Matrix rotAroundCenter;
                    Vector3D translation;
                    AnimationType animationType;
                    EmissiveState currentEmissive;
                    GetCurrentMove(out translation, out rotation, out rotAroundCenter, out animationType, out currentEmissive);

                    if (animationType == AnimationType.Movement)
                    {
                        HasMovement = true;
                        FinalPos.Translation += translation;
                    }

                    if (rotation != Matrix.Zero)
                    {
                        HasMovement = true;
                        FinalPos *= rotation;
                    }

                    if (rotAroundCenter != Matrix.Zero)
                    {
                        HasMovement = true;
                        FinalPos *= rotAroundCenter;
                    }

                    if (currentEmissive.EmissiveParts != null)
                    {
                        for (int j = 0; j < currentEmissive.EmissiveParts.Length; j++)
                        {
                            var currEmissive = currentEmissive.EmissiveParts[j];

                            if (emissivePartCheck.Contains(currEmissive)) continue;

                            emissivePartCheck.Add(currEmissive);
                            emissiveParts.Add(currEmissive);
                        }
                    }

                    Next();
                }
                EmissiveParts = emissiveParts.ToArray();
                Reset();

                foreach (var evnt in Enum.GetNames(typeof(EventTriggers)))
                {
                    EventTriggers trigger;
                    Enum.TryParse(evnt, out trigger);
                    EventIdLookup.Add(trigger, evnt + SubpartId);
                }

                CheckAffectPivot(part, out MovesPivotPos);
            }

        }

        internal PartAnimation(PartAnimation copyFromAnimation)
        {
            EventTrigger = copyFromAnimation.EventTrigger;
            RotationSet = copyFromAnimation.RotationSet;
            RotCenterSet = copyFromAnimation.RotCenterSet;
            CurrentEmissivePart = copyFromAnimation.CurrentEmissivePart;
            AnimationId = copyFromAnimation.AnimationId;
            ResetEmissives = copyFromAnimation.ResetEmissives;
            EmissiveIds = copyFromAnimation.EmissiveIds;

            //Unique Animation ID
            Guid guid = Guid.NewGuid();
            _uid = Convert.ToBase64String(guid.ToByteArray());

            TypeSet = copyFromAnimation.TypeSet;
            Muzzle = copyFromAnimation.Muzzle;
            MoveToSetIndexer = copyFromAnimation.MoveToSetIndexer;
            NumberOfMoves = copyFromAnimation.NumberOfMoves;
            System = copyFromAnimation.System;
            SubpartId = copyFromAnimation.SubpartId;
            MotionDelay = copyFromAnimation.MotionDelay;
            DoesLoop = copyFromAnimation.DoesLoop;
            DoesReverse = copyFromAnimation.DoesReverse;
            TriggerOnce = copyFromAnimation.TriggerOnce;
            _currentMove = 0;
            MovesPivotPos = copyFromAnimation.MovesPivotPos;
            FinalPos = copyFromAnimation.FinalPos;
            HomePos = copyFromAnimation.HomePos;
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

            if (System.WeaponEmissiveSet.TryGetValue(EmissiveIds[MoveToSetIndexer[_currentMove][(int)indexer.EmissiveIndex]], out emissiveState))
            {
                emissiveState.CurrentPart = CurrentEmissivePart[MoveToSetIndexer[_currentMove][(int)indexer.EmissivePartIndex]];

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

            if (resetMove) _currentMove = 0;
            if (resetPos) Part.PositionComp.LocalMatrix = HomePos;
            
        }

        private void CheckAffectPivot(MyEntity part, out bool movesPivotPos)
        {
            var head = -1;
            var tmp = new Dictionary<string, IMyModelDummy>();
            var subparts = new List<MyEntity>();
            movesPivotPos = false;

            while (head < subparts.Count)
            {
                var query = head == -1 ? part : subparts[head];
                head++;
                if (query.Model == null)
                    continue;
                tmp.Clear();
                ((IMyEntity)query).Model.GetDummies(tmp);
                foreach (var kv in tmp)
                {
                    if (kv.Key.StartsWith("subpart_", StringComparison.Ordinal))
                    {
                        if (kv.Key.Contains(System.AzimuthPartName.String) || kv.Key.Contains(System.ElevationPartName.String))
                            movesPivotPos = true;

                        var name = kv.Key.Substring("subpart_".Length);
                        MyEntitySubpart res;
                        if (query.TryGetSubpart(name, out res))
                            subparts.Add(res);
                    }
                }
            }
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
