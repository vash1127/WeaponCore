using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using ParallelTasks;
using Sandbox.Game.GUI.HudViewers;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
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


            foreach (var animationSet in weaponAnimationSets)
            {
                var subpart = parts.NameToEntity[animationSet.SubpartId];
                if (subpart == null) continue;

                foreach (var moves in animationSet.EventMoveSets)
                {
                    if (!allAnimationSet.ContainsKey(moves.Key))
                        allAnimationSet[moves.Key] = new HashSet<PartAnimation>();

                    List<double> moveSet = new List<double>();
                    List<MatrixD> rotationSet = new List<MatrixD>();
                    List<MatrixD> rotCenterSet = new List<MatrixD>();
                    var moveIndexer = new List<int[]>();
                    var rotation = MatrixD.Zero;
                    var rotCenter = MatrixD.Zero;
                    var center = Vector3D.Zero;
                    var dirVectors = new double[][] { new double[] { 0, 0, 0, 0 } };

                    for (int i = 0; i < moves.Value.Length; i++)
                    {
                        var move = moves.Value[i];


                        if (!String.IsNullOrEmpty(move.CenterEmpty) &&
                            (move.rotAroundCenter.x > 0 || move.rotAroundCenter.y > 0 ||
                             move.rotAroundCenter.z > 0 || move.rotAroundCenter.x < 0 ||
                             move.rotAroundCenter.y < 0 || move.rotAroundCenter.z < 0))
                        {
                            //TODO rotate around point
                        }

                        rotCenterSet.Add(rotCenter);

                        if (move.rotation.x > 0 || move.rotation.y > 0 || move.rotation.z > 0 ||
                            move.rotation.x < 0 || move.rotation.y < 0 || move.rotation.z < 0)
                        {
                            rotation = MatrixD.CreateRotationX(MathHelperD.ToRadians(move.rotation.x)) +
                                       MatrixD.CreateRotationY(MathHelperD.ToRadians(move.rotation.y)) +
                                       MatrixD.CreateRotationZ(MathHelperD.ToRadians(move.rotation.z));
                        }

                        rotationSet.Add(rotation);

                        if (move.linearPoints.Length > 0)
                        {

                            dirVectors = new double[move.linearPoints.Length][];
                            double distance = 0;
                            for (int j = 0; j < move.linearPoints.Length; j++)
                            {
                                var point = move.linearPoints[j];

                                var d = Math.Sqrt((point.x * point.x) + (point.y * point.y) +
                                                  (point.z * point.z));

                                distance += d;

                                var dv = new double[4] { d, point.x / d, point.y / d, point.z / d };

                                dirVectors[j] = dv;
                            }
                            try
                            {
                                if (move.MovementType == RelMove.MoveType.ExpoDecay)
                                {

                                    var traveled = 0d;

                                    var check = 1d;
                                    var rate = 0d;
                                    while (check > 0)
                                    {
                                        rate += 0.001;
                                        check = distance * Math.Pow(1 - rate, move.ticksToMove);
                                        if (check < 0.0001) check = 0;

                                    }
                                    for (int j = 0; j < move.ticksToMove; j++)
                                    {
                                        var step = distance * Math.Pow(1 - rate, j + 1);
                                        if (step < 0.0001) step = 0;

                                        var lastTraveled = traveled;
                                        traveled = distance - step;
                                        var changed = traveled - lastTraveled;

                                        moveSet.Add(changed);
                                        moveIndexer.Add(new[] { j, rotationSet.Count - 1, rotCenterSet.Count - 1 });
                                    }
                                }
                                else if (move.MovementType == RelMove.MoveType.ExpoGrowth)
                                {
                                }
                                else
                                {
                                    var distancePerTick = distance / move.ticksToMove;
                                    moveSet.Add(distancePerTick);
                                    for (int j = 0; j < move.ticksToMove; j++)
                                    {
                                        moveIndexer.Add(new[] { moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1 });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Line($"Exception In animation Creation: {ex.Message}   {ex.StackTrace}");
                                allAnimationSet = new Dictionary<EventOptions, HashSet<PartAnimation>>();
                            }

                        }
                        else
                        {
                            //var singleMove = MatrixD.Zero;
                            //moveSet.Add(singleMove);
                            moveSet.Add(0);

                            for (int j = 0; j < move.ticksToMove; j++)
                            {

                                moveIndexer[j] = new[] { 0, rotationSet.Count - 1, rotCenterSet.Count - 1 };
                            }
                        }

                        /*for (int j = 0; j < dirVectors.Length; j++)
                                {
                                    //var singleMove = MatrixD.CreateTranslation(dirVectors[j][1] * distancePerTick,
                                    //    dirVectors[j][2] * distancePerTick, dirVectors[j][3] * distancePerTick);
                                    //moveSet.Add(singleMove);

                                    var numMoves = dirVectors[j][0] / distancePerTick;
                                    for (int k = 0; k < numMoves; k++)
                                    {
                                        moveIndexer.Add(new int[]
                                        {
                                            moveSet.Count - 1,
                                            rotationSet.Count - 1,
                                            rotCenterSet.Count - 1,
                                        });
                                    }
                                }*/

                    }

                    var loop = false;
                    var reverse = false;

                    if (animationSet.Loop != null && animationSet.Loop.Contains(moves.Key))
                        loop = true;

                    if (animationSet.Reverse != null && animationSet.Reverse.Contains(moves.Key))
                        reverse = true;

                    var partAnim = new PartAnimation(moveSet.ToArray(), dirVectors, rotationSet.ToArray(),
                        rotCenterSet.ToArray(), moveIndexer.ToArray(), subpart, parts.Entity, center,
                        animationSet.StartupDelay, rotation, rotCenter, loop, reverse);

                    allAnimationSet[moves.Key].Add(partAnim);
                }
            }

            return allAnimationSet;
        }


        internal void ProcessAnimations()
        {
            PartAnimation animation;
            while (animationsToProcess.TryDequeue(out animation))
            {
                var data = new AnimationParallelData(ref animation);
                if (!animation.MainEnt.MarkedForClose && animation.MainEnt != null)
                {

                    MyAPIGateway.Parallel.Start(AnimateParts, DoAnimation, data);

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
            var realData = data as AnimationParallelData;

            var localMatrix = realData.Animation.Part.PositionComp.LocalMatrix;

            if (realData.Animation.Reverse)
            {
                localMatrix.Translation = localMatrix.Translation - realData.Animation.GetCurrentMove();
                realData.Animation.Previous();
                if (realData.Animation.Previous(false) == realData.Animation.NumberOfMoves - 1)
                {
                    realData.Animation.Reverse = false;
                }
            }
            else
            {
                localMatrix.Translation = localMatrix.Translation + realData.Animation.GetCurrentMove();
                realData.Animation.Next();
                if (realData.Animation.DoesReverse && realData.Animation.Next(false) == 0)
                {
                    realData.Animation.Reverse = true;
                }
            }

            realData.newMatrix = localMatrix;
        }

        internal void DoAnimation(WorkData data)
        {
            var realData = data as AnimationParallelData;

            realData.Animation.Part.PositionComp.SetLocalMatrix(ref realData.newMatrix, realData.Animation.MainEnt, true);

            var Animation = realData.Animation;

            if (Animation.Reverse || !Animation.PauseAnimation && (Animation.DoesLoop || Animation.CurrentMove > 0))
            {
                animationsToQueue.Enqueue(realData.Animation);
            }

        }
    }

    public class AnimationParallelData : WorkData
    {
        public PartAnimation Animation;
        public Matrix newMatrix;

        public AnimationParallelData(ref PartAnimation animation)
        {
            Animation = animation;
        }
    }
}
