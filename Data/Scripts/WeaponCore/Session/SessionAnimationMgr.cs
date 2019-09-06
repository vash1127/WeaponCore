using System;
using System.CodeDom;
using System.Collections.Generic;
using Havok;
using ParallelTasks;
using Sandbox.Common.ObjectBuilders.Definitions;
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
        internal Dictionary<EventOptions, HashSet<PartAnimation>> CreateAnimationSets(PartAnimationSetDef[] weaponAnimationSets, RecursiveSubparts parts)
        {
            var allAnimationSet = new Dictionary<EventOptions, HashSet<PartAnimation>>();

            if (weaponAnimationSets == null) return new Dictionary<EventOptions, HashSet<PartAnimation>>();
            foreach (var animationSet in weaponAnimationSets)
            {
                var subpart = parts.NameToEntity[animationSet.SubpartId] as MyEntitySubpart;
                if (subpart == null) continue;

                foreach (var moves in animationSet.EventMoveSets)
                {
                    if (!allAnimationSet.ContainsKey(moves.Key))
                        allAnimationSet[moves.Key] = new HashSet<PartAnimation>();

                    List<Vector3?> moveSet = new List<Vector3?>();
                    List<MatrixD?> rotationSet = new List<MatrixD?>();
                    List<MatrixD?> rotCenterSet = new List<MatrixD?>();
                    AnimationType[] typeSet = new[]
                    {
                        AnimationType.Movement,
                        AnimationType.ShowInstant,
                        AnimationType.HideInstant,
                        AnimationType.ShowFade,
                        AnimationType.HideFade,
                        AnimationType.Delay
                    };

                    var moveIndexer = new List<int[]>();
                    var rotCenter = MatrixD.Zero;

                    for (int i = 0; i < moves.Value.Length; i++)
                    {
                        var move = moves.Value[i];

                        if (move.MovementType == RelMove.MoveType.Delay || move.MovementType == RelMove.MoveType.Show || move.MovementType == RelMove.MoveType.Hide)
                        {
                            moveSet.Add(null);
                            rotationSet.Add(null);
                            rotCenterSet.Add(null);
                            for (var j = 0; j < move.TicksToMove; j++)
                            {
                                var type = 5;

                                switch (move.MovementType)
                                {
                                    case RelMove.MoveType.Delay:
                                        break;

                                    case RelMove.MoveType.Show:
                                        type = move.Fade ? 3 : 1;
                                        break;

                                    case RelMove.MoveType.Hide:
                                        type = move.Fade ? 4 : 2;
                                        break;
                                    
                                }
                                moveIndexer.Add(new[]
                                    {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, type});
                            }
                        }
                        else
                        {
                            if (!String.IsNullOrEmpty(move.CenterEmpty) &&
                                (move.RotAroundCenter.x > 0 || move.RotAroundCenter.y > 0 ||
                                 move.RotAroundCenter.z > 0 || move.RotAroundCenter.x < 0 ||
                                 move.RotAroundCenter.y < 0 || move.RotAroundCenter.z < 0))
                            {
                                var partCenter = getPartLocation(move.CenterEmpty, (IMyModel) subpart.Parent.Model);
                                var emptyCenter = getPartLocation(move.CenterEmpty, (IMyModel)subpart.Model);
                                if (partCenter != null && emptyCenter != null)
                                {
                                    var center = (Vector3)emptyCenter + (Vector3)partCenter;

                                    rotCenterSet.Add(CreateRotation(move.RotAroundCenter.x / move.TicksToMove,
                                        move.RotAroundCenter.y / move.TicksToMove,
                                        move.RotAroundCenter.z / move.TicksToMove, center));
                                }
                                else
                                    rotCenterSet.Add(null);
                            }
                            else
                                rotCenterSet.Add(null);

                            if (move.Rotation.x > 0 || move.Rotation.y > 0 || move.Rotation.z > 0 ||
                                move.Rotation.x < 0 || move.Rotation.y < 0 || move.Rotation.z < 0)
                            {

                                var partCenter = getPartLocation("subpart_" + animationSet.SubpartId, (IMyModel)subpart.Parent.Model);
                                if (partCenter != null)
                                    rotationSet.Add(CreateRotation(move.Rotation.x / move.TicksToMove,
                                        move.Rotation.y / move.TicksToMove, move.Rotation.z / move.TicksToMove, (Vector3)partCenter));
                                else
                                    rotationSet.Add(null);


                            }
                            else
                                rotationSet.Add(null);

                            if (move.LinearPoints != null && move.LinearPoints.Length > 0)
                            {
                                double distance = 0;
                                var tmpDirVec = new double[move.LinearPoints.Length][];

                                for (int j = 0; j < move.LinearPoints.Length; j++)
                                {
                                    var point = move.LinearPoints[j];

                                    var d = Math.Sqrt((point.x * point.x) + (point.y * point.y) +
                                                      (point.z * point.z));

                                    distance += d;

                                    var dv = new[] { d, point.x / d, point.y / d, point.z / d };

                                    tmpDirVec[j] = dv;
                                }

                                if (move.MovementType == RelMove.MoveType.ExpoDecay)
                                {
                                    var traveled = 0d;

                                    var check = 1d;
                                    var rate = 0d;
                                    while (check > 0)
                                    {
                                        rate += 0.001;
                                        check = distance * Math.Pow(1 - rate, move.TicksToMove);
                                        if (check < 0.01) check = 0;

                                    }

                                    var vectorCount = 0;
                                    var remaining = 0d;
                                    var vecTotalMoved = 0d;
                                    rate = 1 - rate;

                                    for (int j = 0; j < move.TicksToMove; j++)
                                    {
                                        var step = distance * Math.Pow(rate, j + 1);
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


                                        var vector = new Vector3(tmpDirVec[vectorCount][1] * changed,
                                            tmpDirVec[vectorCount][2] * changed, tmpDirVec[vectorCount][3] * changed);

                                        moveSet.Add(vector);
                                        moveIndexer.Add(new[]
                                            {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0});

                                        if (remaining > 0)
                                            vectorCount++;

                                    }
                                }
                                else if (move.MovementType == RelMove.MoveType.ExpoGrowth)
                                {
                                    var traveled = 0d;

                                    var rateFound = false;
                                    var rate = 0d;
                                    var check = 0d;
                                    while (check < distance)
                                    {
                                        rate += 0.001;
                                        check = 0.01 * Math.Pow(1 + rate, move.TicksToMove);
                                    }

                                    var vectorCount = 0;
                                    var remaining = 0d;
                                    var vecTotalMoved = 0d;
                                    rate += 1;

                                    for (int j = 0; j < move.TicksToMove; j++)
                                    {
                                        var step = 0.01 * Math.Pow(rate, j + 1);
                                        if (step > distance) step = distance;

                                        var lastTraveled = traveled;
                                        traveled = step;
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


                                        var vector = new Vector3(tmpDirVec[vectorCount][1] * changed,
                                            tmpDirVec[vectorCount][2] * changed, tmpDirVec[vectorCount][3] * changed);

                                        moveSet.Add(vector);
                                        moveIndexer.Add(new[]
                                            {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0});

                                        if (remaining > 0)
                                            vectorCount++;
                                    }
                                }
                                else if (move.MovementType == RelMove.MoveType.Linear)
                                {
                                    var distancePerTick = distance / move.TicksToMove;
                                    var vectorCount = 0;
                                    var remaining = 0d;
                                    var vecTotalMoved = 0d;
                                    for (int j = 0; j < move.TicksToMove; j++)
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

                                        var vector = new Vector3(tmpDirVec[vectorCount][1] * changed,tmpDirVec[vectorCount][2] * changed, tmpDirVec[vectorCount][3] * changed);

                                        moveSet.Add(vector);
                                        moveIndexer.Add(new[]
                                            {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0});

                                        if (remaining > 0)
                                            vectorCount++;
                                    }
                                }
                                else
                                {
                                    moveSet.Add(null);

                                    for (int j = 0; j < move.TicksToMove; j++)
                                        moveIndexer.Add(new[] { moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0 });
                                }

                            }
                            else
                            {
                                moveSet.Add(null);

                                for (int j = 0; j < move.TicksToMove; j++)
                                    moveIndexer.Add(new[] { moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0 });
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
                        rotCenterSet.ToArray(), typeSet, moveIndexer.ToArray(), subpart, parts.Entity, animationSet.BarrelId,
                        animationSet.StartupDelay, animationSet.AnimationDelays[moves.Key], loop, reverse);

                    allAnimationSet[moves.Key].Add(partAnim);
                }
            }

            return allAnimationSet;
        }

        internal Matrix CreateRotation(double x, double y, double z, Vector3 center)
        {

            var rotation = MatrixD.Zero;

            if (x > 0 || x < 0)
                rotation = MatrixD.CreateRotationX(MathHelperD.ToRadians(x));

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


            rotation = Matrix.CreateTranslation(-center) * (Matrix)rotation *
                       Matrix.CreateTranslation(center);



            return rotation;



        }

        internal Vector3? getPartLocation(string partName, IMyModel model)
        {
            Dictionary<string, IMyModelDummy> dummyList = new Dictionary<string, IMyModelDummy>();
            model.GetDummies(dummyList);

            IMyModelDummy dummy;
            if (dummyList.TryGetValue(partName, out dummy))
            {
                return dummy.Matrix.Translation;

            }

            return null;
        }

        internal void ProcessAnimations()
        {
            PartAnimation animation;
            while (animationsToProcess.TryDequeue(out animation))
            {
                //var data = new AnimationParallelData(ref animation);
                if (!animation.MainEnt.MarkedForClose && animation.MainEnt != null)
                {
                    if ((animation.DoesLoop && animation.Looping && !animation.PauseAnimation) || animation.MotionDelay <= 0 || animation.CurrentMove > 0 || (animation.MotionDelay > 0 && animation.StartTick <= Tick && animation.StartTick > 0 && animation.CurrentMove == 0))
                    {
                        //MyAPIGateway.Parallel.StartBackground(AnimateParts, DoAnimation, data);
                        AnimateParts(animation);
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

        internal void AnimateParts(PartAnimation animation)
        {
            var localMatrix = animation.Part.PositionComp.LocalMatrix;
            MatrixD? rotation;
            Vector3D translation;
            AnimationType animationType;
            animation.GetCurrentMove(out translation, out rotation, out animationType);


            if (animation.Reverse)
            {
                localMatrix.Translation = localMatrix.Translation - translation;

                animation.Previous();
                if (animation.Previous(false) == animation.NumberOfMoves - 1)
                {
                    animation.Reverse = false;
                }
            }
            else
            {
                localMatrix.Translation = localMatrix.Translation + translation;

                animation.Next();
                if (animation.DoesReverse && animation.Next(false) == 0)
                {
                    animation.Reverse = true;
                }
            }

            if (rotation != null)
            {
                localMatrix *= (Matrix)rotation;
            }

            if (animationType == AnimationType.Movement)
            {
                animation.Part.PositionComp.SetLocalMatrix(ref localMatrix,
                    animation.MainEnt, true);
            }

            else if (animationType == AnimationType.ShowInstant || animationType == AnimationType.ShowFade)
            {
                animation.Part.Render.FadeIn = animationType == AnimationType.ShowFade;
                animation.Part.Render.AddRenderObjects();
            }
            else if (animationType == AnimationType.HideInstant || animationType == AnimationType.HideFade)
            {
                animation.Part.Render.FadeOut = animationType == AnimationType.HideFade;
                animation.Part.Render.RemoveRenderObjects();
            }

            var Animation = animation;

            if (Animation.Reverse || Animation.DoesLoop || Animation.CurrentMove > 0)
            {
                animationsToQueue.Enqueue(animation);
            }
        }

        #region Threaded animation code
        
        internal void AnimateParts(WorkData data)
        {
            var animationData = data as AnimationParallelData;

            var localMatrix = animationData.Animation.Part.PositionComp.LocalMatrix;
            MatrixD? rotation;
            Vector3D translation;
            AnimationType animationType;
            animationData.Animation.GetCurrentMove(out translation, out rotation, out animationType);


            if (animationData.Animation.Reverse)
            {
                localMatrix.Translation = localMatrix.Translation - translation;

                animationData.Animation.Previous();
                if (animationData.Animation.Previous(false) == animationData.Animation.NumberOfMoves - 1)
                {
                    animationData.Animation.Reverse = false;
                }
            }
            else
            {
                localMatrix.Translation = localMatrix.Translation + translation;

                animationData.Animation.Next();
                if (animationData.Animation.DoesReverse && animationData.Animation.Next(false) == 0)
                {
                    animationData.Animation.Reverse = true;
                }
            }

            if (rotation != null)
            {
                localMatrix *= (Matrix)rotation;
            }


            animationData.NewMatrix = localMatrix;
            animationData.Type = animationType;

        }

        internal void DoAnimation(WorkData data)
        {
            var animationData = data as AnimationParallelData;
            var animationType = animationData.Type;

            if (animationType == AnimationType.Movement)
            {
                animationData.Animation.Part.PositionComp.SetLocalMatrix(ref animationData.NewMatrix,
                    animationData.Animation.MainEnt, true);
                Log.Line("Movement");
            }

            else if (animationType == AnimationType.ShowInstant || animationType == AnimationType.ShowFade)
            {
                animationData.Animation.Part.Render.FadeIn = animationType == AnimationType.ShowFade;
                animationData.Animation.Part.Render.AddRenderObjects();
            }
            else if (animationType == AnimationType.HideInstant || animationType == AnimationType.HideFade)
            {
                animationData.Animation.Part.Render.FadeOut = animationType == AnimationType.HideFade;
                animationData.Animation.Part.Render.RemoveRenderObjects();
            }

            var Animation = animationData.Animation;

            if (Animation.Reverse || Animation.DoesLoop || Animation.CurrentMove > 0)
            {
                animationsToQueue.Enqueue(animationData.Animation);
            }

            //animationData.timer.Complete();
        }
        #endregion
    }

    public class AnimationParallelData : WorkData
    {
        internal PartAnimation Animation;
        internal Matrix NewMatrix;
        internal Session.AnimationType Type;

        public AnimationParallelData(ref PartAnimation animation)
        {
            Animation = animation;
        }
    }
}
