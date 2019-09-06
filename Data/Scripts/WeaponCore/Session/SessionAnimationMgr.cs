using System;
using System.CodeDom;
using System.Collections.Generic;
using ParallelTasks;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using VRageRender.Import;
using WeaponCore.Support;
using static WeaponCore.Support.PartAnimationSetDef;

namespace WeaponCore
{
    public partial class Session
    {

        //TODO Move to fields when done
        internal MyConcurrentQueue<PartAnimation> animationsToProcess = new MyConcurrentQueue<PartAnimation>();
        internal MyConcurrentQueue<PartAnimation> animationsToQueue = new MyConcurrentQueue<PartAnimation>();


        internal Dictionary<EventOptions, HashSet<PartAnimation>> CreateAnimationSets(PartAnimationSetDef[] weaponAnimationSets, RecursiveSubparts parts)
        {
            var allAnimationSet = new Dictionary<EventOptions, HashSet<PartAnimation>>();

            if(weaponAnimationSets == null) return new Dictionary<EventOptions, HashSet<PartAnimation>>();
            foreach (var animationSet in weaponAnimationSets)
            {
                MyEntitySubpart subpart = parts.NameToEntity[animationSet.SubpartId] as MyEntitySubpart;
                if (subpart == null) continue;
                Vector3 center = subpart.PositionComp.GetPosition() - subpart.Parent.PositionComp.GetPosition();


                foreach (var moves in animationSet.EventMoveSets)
                {
                    if (!allAnimationSet.ContainsKey(moves.Key))
                        allAnimationSet[moves.Key] = new HashSet<PartAnimation>();

                    List<Vector3?> moveSet = new List<Vector3?>();
                    List<MatrixD?> rotationSet = new List<MatrixD?>();
                    List<MatrixD?> rotCenterSet = new List<MatrixD?>();
                    var moveIndexer = new List<int[]>();
                    var rotCenter = MatrixD.Zero;

                    for (int i = 0; i < moves.Value.Length; i++)
                    {
                        var move = moves.Value[i];


                        if (!String.IsNullOrEmpty(move.CenterEmpty) &&
                            (move.rotAroundCenter.x > 0 || move.rotAroundCenter.y > 0 ||
                             move.rotAroundCenter.z > 0 || move.rotAroundCenter.x < 0 ||
                             move.rotAroundCenter.y < 0 || move.rotAroundCenter.z < 0))
                        {
                            Dictionary<string, IMyModelDummy> _dummyList = new Dictionary<string, IMyModelDummy>();
                            ((IMyModel) subpart.Model).GetDummies(_dummyList);

                            IMyModelDummy dummy;
                            if (_dummyList.TryGetValue(move.CenterEmpty, out dummy))
                            {
                                var dummyCenter = Vector3D.Transform(MatrixD.Normalize(dummy.Matrix).Translation, subpart.WorldMatrix);

                                rotCenterSet.Add(CreateRotation(move.rotAroundCenter.x / move.ticksToMove,
                                    move.rotAroundCenter.y / move.ticksToMove, move.rotAroundCenter.z / move.ticksToMove, dummyCenter));
                            }
                            else
                                rotCenterSet.Add(null);
                        }
                        else
                            rotCenterSet.Add(null);

                        if (move.rotation.x > 0 || move.rotation.y > 0 || move.rotation.z > 0 ||
                            move.rotation.x < 0 || move.rotation.y < 0 || move.rotation.z < 0)
                            rotationSet.Add(CreateRotation(move.rotation.x / move.ticksToMove, move.rotation.y / move.ticksToMove, move.rotation.z / move.ticksToMove, center));
                        else
                            rotationSet.Add(null);

                        if (move.linearPoints != null && move.linearPoints.Length > 0 || move.MovementType == RelMove.MoveType.Delay)
                        {
                            double[][] tmpDirVec = new double[0][];
                            double distance = 0;

                            if (move.linearPoints != null)
                            {
                                 tmpDirVec = new double[move.linearPoints.Length][];
                                
                                for (int j = 0; j < move.linearPoints.Length; j++)
                                {
                                    var point = move.linearPoints[j];

                                    var d = Math.Sqrt((point.x * point.x) + (point.y * point.y) +
                                                      (point.z * point.z));

                                    distance += d;

                                    var dv = new double[4] {d, point.x / d, point.y / d, point.z / d};

                                    tmpDirVec[j] = dv;
                                }
                            }

                            if (move.MovementType == RelMove.MoveType.ExpoDecay && tmpDirVec != null)
                            {
                                var traveled = 0d;

                                var check = 1d;
                                var rate = 0d;
                                while (check > 0)
                                {
                                    rate += 0.001;
                                    check = distance * Math.Pow(1 - rate, move.ticksToMove);
                                    if (check < 0.01) check = 0;

                                }

                                var vectorCount = 0;
                                var remaining = 0d;
                                var vecTotalMoved = 0d;
                                for (int j = 0; j < move.ticksToMove; j++)
                                {
                                    var step = distance * Math.Pow(1 - rate, j + 1);
                                    if (step < 0.01) step = 0;

                                    var lastTraveled = traveled;
                                    traveled = distance - step;
                                    var changed = traveled - lastTraveled;

                                    changed += remaining;
                                    if ( changed > tmpDirVec[vectorCount][0] - vecTotalMoved)
                                    {
                                        var origMove = changed;
                                        changed = changed - tmpDirVec[vectorCount][0] - vecTotalMoved;
                                        remaining = origMove - changed;
                                        vecTotalMoved = 0;
                                    }
                                    else
                                    {
                                        vecTotalMoved += changed;
                                        remaining = 0;
                                    }


                                    var vector = new Vector3(tmpDirVec[vectorCount][1] * changed, tmpDirVec[vectorCount][2] * changed, tmpDirVec[vectorCount][3] * changed);

                                    moveSet.Add(vector);
                                    moveIndexer.Add(new[] { moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1 });

                                    if(remaining > 0)
                                        vectorCount++;
                                }
                            }
                            else if (move.MovementType == RelMove.MoveType.ExpoGrowth && tmpDirVec != null)
                            {
                                var traveled = 0d;

                                var check = 0d;
                                var rate = 0d;
                                while (check > 0)
                                {
                                    rate += 0.001;
                                    check = distance * Math.Pow(1 - rate, move.ticksToMove);
                                    if (check <= 0.01) check = 0;

                                }

                                var vectorCount = 0;
                                var remaining = 0d;
                                var vecTotalMoved = 0d;
                                for (int j = 0; j < move.ticksToMove; j++)
                                {
                                    var step = distance * Math.Pow(1 - rate, j + 1);
                                    if (step < 0.01) step = 0;

                                    var lastTraveled = traveled;
                                    traveled = distance - step;
                                    var changed = traveled - lastTraveled;

                                    changed += remaining;
                                    if (changed > tmpDirVec[vectorCount][0] - vecTotalMoved)
                                    {
                                        var origMove = changed;
                                        changed = changed - tmpDirVec[vectorCount][0] - vecTotalMoved;
                                        remaining = origMove - changed;
                                        vecTotalMoved = 0;
                                    }
                                    else
                                    {
                                        vecTotalMoved += changed;
                                        remaining = 0;
                                    }


                                    var vector = new Vector3(tmpDirVec[vectorCount][1] * changed, tmpDirVec[vectorCount][2] * changed, tmpDirVec[vectorCount][3] * changed);

                                    moveSet.Add(vector);
                                    moveIndexer.Add(new[] { moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1 });

                                    if (remaining > 0)
                                        vectorCount++;
                                }
                            }
                            else if (move.MovementType == RelMove.MoveType.Delay)
                            {
                                moveSet.Add(null);
                                for (int j = 0; j < move.ticksToMove; j++)
                                {
                                    moveIndexer.Add(new[]
                                        {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1});
                                }
                            }
                            else if (move.MovementType == RelMove.MoveType.Linear && tmpDirVec != null)
                            {
                                var distancePerTick = distance / move.ticksToMove;
                                var vectorCount = 0;
                                var remaining = 0d;
                                var vecTotalMoved = 0d;
                                for (int j = 0; j < move.ticksToMove; j++)
                                {
                                    var changed = distancePerTick + remaining;
                                    if (changed > tmpDirVec[vectorCount][0] - vecTotalMoved)
                                    {
                                        var origMove = changed;
                                        changed = changed - tmpDirVec[vectorCount][0] - vecTotalMoved;
                                        remaining = origMove - changed;
                                        vecTotalMoved = 0;
                                    }
                                    else
                                    {
                                        vecTotalMoved += changed;
                                        remaining = 0;
                                    }

                                    var vector = new Vector3(tmpDirVec[vectorCount][1] * changed, tmpDirVec[vectorCount][2] * changed, tmpDirVec[vectorCount][3] * changed);

                                    moveSet.Add(vector);
                                    moveIndexer.Add(new[] { moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1 });

                                    if (remaining > 0)
                                        vectorCount++;
                                }
                            }
                            else
                            {
                                moveSet.Add(new Vector3(0, 0, 0));

                                for (int j = 0; j < move.ticksToMove; j++)
                                {

                                    moveIndexer.Add(new[] { moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1 });
                                }
                            }

                        }
                        else
                        {
                            moveSet.Add(new Vector3(0,0,0));

                            for (int j = 0; j < move.ticksToMove; j++)
                            {

                                moveIndexer.Add(new[] { moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1 });
                            }
                        }
                    }

                    var loop = false;
                    var reverse = false;

                    if (animationSet.Loop != null && animationSet.Loop.Contains(moves.Key))
                        loop = true;

                    if (animationSet.Reverse != null && animationSet.Reverse.Contains(moves.Key))
                        reverse = true;

                    var partAnim = new PartAnimation(moveSet.ToArray(), rotationSet.ToArray(),
                        rotCenterSet.ToArray(), moveIndexer.ToArray(), subpart, parts.Entity, center, animationSet.muzzle,
                        animationSet.StartupDelay, animationSet.motionDelay, loop, reverse);

                    allAnimationSet[moves.Key].Add(partAnim);
                }
            }

            return allAnimationSet;
        }

        internal Matrix CreateRotation(double x, double y, double z, Vector3 center)
        {

            var rotation = MatrixD.Zero;

            if (x > 0 || x < 0)
                rotation = MatrixD.CreateRotationX(MathHelperD.ToRadians(x)); ;

            if (y > 0 || y < 0)
                if (x > 0 || x < 0)
                    rotation *= MatrixD.CreateRotationY(MathHelperD.ToRadians(y));
                else
                    rotation = MatrixD.CreateRotationY(MathHelperD.ToRadians(y));

            if (z > 0 || z < 0)
                if (x > 0 || x < 0 || y > 0 || y < 0)
                    rotation *= MatrixD.CreateRotationZ(MathHelperD.ToRadians(z));
                else
                    rotation = MatrixD.CreateRotationZ(MathHelperD.ToRadians(z));


            return Matrix.CreateTranslation(-center) * (Matrix)rotation *
                       Matrix.CreateTranslation(center);
        }


        internal void ProcessAnimations()
        {
            PartAnimation animation;
            while (animationsToProcess.TryDequeue(out animation))
            {
                var data = new AnimationParallelData(ref animation);
                if (!animation.MainEnt.MarkedForClose && animation.MainEnt != null)
                {
                    if ((animation.DoesLoop && animation.Looping && !animation.PauseAnimation) && animation.MotionDelay <= 0 || animation.CurrentMove > 0 || (animation.MotionDelay > 0 && animation.StartTick <= Tick && animation.StartTick > 0 && animation.CurrentMove == 0))
                    {
                        MyAPIGateway.Parallel.Start(AnimateParts, DoAnimation, data);
                        animation.StartTick = 0;
                    }
                    else if (animation.MotionDelay > 0 && animation.StartTick == 0)
                    {
                        animation.StartTick = Tick + animation.MotionDelay;
                        animationsToQueue.Enqueue(animation);
                    }
                    else
                    {
                        animationsToQueue.Enqueue(animation);
                    }

                }
            }

        }

        internal void ProcessAnimationQueue()
        {
            PartAnimation animation;
            while (animationsToQueue.TryDequeue(out animation))
            {
                if (!animation.MainEnt.MarkedForClose && animation.MainEnt != null)
                    animationsToProcess.Enqueue(animation);
            }
        }

        internal void AnimateParts(WorkData data)
        {
            var AnimationData = data as AnimationParallelData;

            var localMatrix = AnimationData.Animation.Part.PositionComp.LocalMatrix;
            MatrixD? rotation;
            Vector3D translation;
            bool delay;

            AnimationData.Animation.GetCurrentMove(out translation, out rotation, out delay);
            if (AnimationData.Animation.Reverse)
            {
                if (!delay)
                    localMatrix.Translation = localMatrix.Translation - translation;

                AnimationData.Animation.Previous();
                if (AnimationData.Animation.Previous(false) == AnimationData.Animation.NumberOfMoves - 1)
                {
                    AnimationData.Animation.Reverse = false;
                }
            }
            else
            {
                if (!delay)
                    localMatrix.Translation = localMatrix.Translation + translation;

                AnimationData.Animation.Next();
                if (AnimationData.Animation.DoesReverse && AnimationData.Animation.Next(false) == 0)
                {
                    AnimationData.Animation.Reverse = true;
                }
            }

            if (rotation != null)
            {
                //AnimationData.Animation.Part.Render.FadeOut = true;
                //AnimationData.Animation.Part.Render.RemoveRenderObjects();
                localMatrix *= (Matrix) rotation;
            }

            AnimationData.newMatrix = localMatrix;
            AnimationData.delay = delay;
        }

        internal void DoAnimation(WorkData data)
        {
            var AnimationData = data as AnimationParallelData;

            if(!AnimationData.delay)
                AnimationData.Animation.Part.PositionComp.SetLocalMatrix(ref AnimationData.newMatrix, AnimationData.Animation.MainEnt, true);

            var Animation = AnimationData.Animation;

            if (Animation.Reverse || Animation.DoesLoop || Animation.CurrentMove > 0)
            {
                animationsToQueue.Enqueue(AnimationData.Animation);
            }

        }
    }

    public class AnimationParallelData : WorkData
    {
        public PartAnimation Animation;
        public Matrix newMatrix;
        public bool delay;

        public AnimationParallelData(ref PartAnimation animation)
        {
            Animation = animation;
        }
    }
}
