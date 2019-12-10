using System;
using System.Collections.Generic;
using ParallelTasks;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.PartAnimation;

namespace WeaponCore
{
    public partial class Session
    {
        internal void CreateAnimationSets(AnimationDefinition animations, WeaponSystem system, out Dictionary<Weapon.EventTriggers, HashSet<PartAnimation>> weaponAnimationSets, out Dictionary<string, EmissiveState> weaponEmissivesSet, out Dictionary<string, Matrix[]> weaponLinearMoveSet, out HashSet<string> animationIdLookup, out uint onDelay)
        {

            var allAnimationSet = new Dictionary<Weapon.EventTriggers, HashSet<PartAnimation>>();
            var allEmissivesSet = new Dictionary<string, EmissiveState>();
            animationIdLookup = new HashSet<string>();

            onDelay = 0;

            var wepAnimationSets = animations.WeaponAnimationSets;
            var wepEmissivesSet = animations.Emissives;

            weaponLinearMoveSet = new Dictionary<string, Matrix[]>();

            var emissiveLookup = new Dictionary<string, WeaponEmissive>();

            if (wepEmissivesSet != null)
            {
                foreach (var emissive in wepEmissivesSet)
                    emissiveLookup.Add(emissive.EmissiveName, emissive);
            }

            if (wepAnimationSets == null)
            {
                weaponAnimationSets = allAnimationSet;
                weaponEmissivesSet = allEmissivesSet;
                return;
            }
            foreach (var animationSet in wepAnimationSets)
            {
                for (int t = 0; t < animationSet.SubpartId.Length; t++)
                {
                    foreach (var moves in animationSet.EventMoveSets)
                    {
                        if (!allAnimationSet.ContainsKey(moves.Key))
                            allAnimationSet[moves.Key] = new HashSet<PartAnimation>();

                        List<Matrix> moveSet = new List<Matrix>();
                        List<Matrix> rotationSet = new List<Matrix>();
                        List<Matrix> rotCenterSet = new List<Matrix>();
                        List<string> rotCenterNameSet = new List<string>();

                        var id = $"{moves.Key}{animationSet.SubpartId[t]}";
                        animationIdLookup.Add(id);
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
                        var currentEmissivePart = new List<int>();

                        for (int i = 0; i < moves.Value.Length; i++)
                        {
                            var move = moves.Value[i];

                            if (moves.Key == Weapon.EventTriggers.TurnOn)
                                onDelay += move.TicksToMove;

                            var hasEmissive = !string.IsNullOrEmpty(move.EmissiveName);

                            if (move.MovementType == RelMove.MoveType.Delay ||
                            move.MovementType == RelMove.MoveType.Show ||
                            move.MovementType == RelMove.MoveType.Hide)
                            {
                                moveSet.Add(Matrix.Zero);
                                rotationSet.Add(Matrix.Zero);
                                rotCenterSet.Add(Matrix.Zero);
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

                                    WeaponEmissive emissive;
                                    if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                    {
                                        createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                    }
                                    else
                                    {
                                        allEmissivesSet.Add(id + moveIndexer.Count, new EmissiveState());
                                        currentEmissivePart.Add(-1);
                                    }

                                    moveIndexer.Add(new[]
                                        {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, type, currentEmissivePart.Count - 1});
                                }
                            }
                            else
                            {
                                if (!String.IsNullOrEmpty(move.CenterEmpty) &&
                                    (move.RotAroundCenter.x > 0 || move.RotAroundCenter.y > 0 ||
                                     move.RotAroundCenter.z > 0 || move.RotAroundCenter.x < 0 ||
                                     move.RotAroundCenter.y < 0 || move.RotAroundCenter.z < 0))
                                {
                                    rotCenterNameSet.Add(move.CenterEmpty);
                                    rotCenterSet.Add(CreateRotation(move.RotAroundCenter.x / move.TicksToMove,
                                        move.RotAroundCenter.y / move.TicksToMove,
                                        move.RotAroundCenter.z / move.TicksToMove));
                                }
                                else
                                {
                                    rotCenterNameSet.Add(null);
                                    rotCenterSet.Add(Matrix.Zero);
                                }

                                if (move.Rotation.x > 0 || move.Rotation.y > 0 || move.Rotation.z > 0 ||
                                    move.Rotation.x < 0 || move.Rotation.y < 0 || move.Rotation.z < 0)
                                {
                                    rotationSet.Add(CreateRotation(move.Rotation.x / move.TicksToMove, move.Rotation.y / move.TicksToMove, move.Rotation.z / move.TicksToMove));
                                }
                                else
                                    rotationSet.Add(Matrix.Zero);

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
                                            if (check < 0.001) check = 0;

                                        }

                                        var vectorCount = 0;
                                        var remaining = 0d;
                                        var vecTotalMoved = 0d;
                                        rate = 1 - rate;

                                        for (int j = 0; j < move.TicksToMove; j++)
                                        {
                                            var step = distance * Math.Pow(rate, j + 1);
                                            if (step < 0.001) step = 0;

                                            var lastTraveled = traveled;
                                            traveled = distance - step;
                                            var changed = traveled - lastTraveled;

                                            changed += remaining;
                                            if (changed > tmpDirVec[vectorCount][0] - vecTotalMoved)
                                            {
                                                var origMove = changed;
                                                changed = changed - (tmpDirVec[vectorCount][0] - vecTotalMoved);
                                                remaining = origMove - changed;
                                                vecTotalMoved = 0;
                                            }
                                            else
                                            {
                                                vecTotalMoved += changed;
                                                remaining = 0;
                                            }


                                            var vector = new Vector3(tmpDirVec[vectorCount][1] * changed,
                                                tmpDirVec[vectorCount][2] * changed,
                                                tmpDirVec[vectorCount][3] * changed);

                                            var matrix = Matrix.CreateTranslation(vector);

                                            moveSet.Add(matrix);

                                            WeaponEmissive emissive;
                                            if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                            {
                                                createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                            }
                                            else
                                            {
                                                allEmissivesSet.Add(id + moveIndexer.Count, new EmissiveState());
                                                currentEmissivePart.Add(-1);
                                            }

                                            moveIndexer.Add(new[]
                                                {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, currentEmissivePart.Count - 1});

                                            if (remaining > 0)
                                                vectorCount++;

                                        }
                                    }
                                    else if (move.MovementType == RelMove.MoveType.ExpoGrowth)
                                    {
                                        var traveled = 0d;

                                        var rate = 0d;
                                        var check = 0d;
                                        while (check < distance)
                                        {
                                            rate += 0.001;
                                            check = 0.001 * Math.Pow(1 + rate, move.TicksToMove);
                                        }

                                        var vectorCount = 0;
                                        var remaining = 0d;
                                        var vecTotalMoved = 0d;
                                        rate += 1;

                                        for (int j = 0; j < move.TicksToMove; j++)
                                        {
                                            var step = 0.001 * Math.Pow(rate, j + 1);
                                            if (step > distance) step = distance;

                                            var lastTraveled = traveled;
                                            traveled = step;
                                            var changed = traveled - lastTraveled;

                                            changed += remaining;
                                            if (changed > tmpDirVec[vectorCount][0] - vecTotalMoved)
                                            {
                                                var origMove = changed;
                                                changed = changed - (tmpDirVec[vectorCount][0] - vecTotalMoved);
                                                remaining = origMove - changed;
                                                vecTotalMoved = 0;
                                            }
                                            else
                                            {
                                                vecTotalMoved += changed;
                                                remaining = 0;
                                            }


                                            var vector = new Vector3(tmpDirVec[vectorCount][1] * changed,
                                                tmpDirVec[vectorCount][2] * changed,
                                                tmpDirVec[vectorCount][3] * changed);

                                            var matrix = Matrix.CreateTranslation(vector);

                                            moveSet.Add(matrix);

                                            WeaponEmissive emissive;
                                            if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                            {
                                                createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                            }
                                            else
                                            {
                                                allEmissivesSet.Add(id + moveIndexer.Count, new EmissiveState());
                                                currentEmissivePart.Add(-1);
                                            }

                                            moveIndexer.Add(new[]
                                                {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, currentEmissivePart.Count - 1});

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
                                                changed = changed - (tmpDirVec[vectorCount][0] - vecTotalMoved);
                                                remaining = origMove - changed;
                                                vecTotalMoved = 0;
                                            }
                                            else
                                            {
                                                vecTotalMoved += changed;
                                                remaining = 0;
                                            }

                                            var vector = new Vector3(tmpDirVec[vectorCount][1] * changed,
                                                tmpDirVec[vectorCount][2] * changed,
                                                tmpDirVec[vectorCount][3] * changed);

                                            var matrix = Matrix.CreateTranslation(vector);

                                            moveSet.Add(matrix);

                                            WeaponEmissive emissive;
                                            if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                            {
                                                createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                            }
                                            else
                                            {
                                                allEmissivesSet.Add(id + moveIndexer.Count, new EmissiveState());
                                                currentEmissivePart.Add(-1);
                                            }

                                            moveIndexer.Add(new[]
                                                {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, currentEmissivePart.Count - 1});

                                            if (remaining > 0)
                                                vectorCount++;
                                        }
                                    }
                                    else
                                    {
                                        moveSet.Add(Matrix.Zero);

                                        for (int j = 0; j < move.TicksToMove; j++)
                                        {
                                            WeaponEmissive emissive;
                                            if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                            {
                                                createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                            }
                                            else
                                            {
                                                allEmissivesSet.Add(id + moveIndexer.Count, new EmissiveState());
                                                currentEmissivePart.Add(-1);
                                            }

                                            moveIndexer.Add(new[]
                                                {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, allEmissivesSet.Count - 1});
                                        }
                                    }

                                }
                                else
                                {
                                    moveSet.Add(Matrix.Zero);

                                    for (int j = 0; j < move.TicksToMove; j++)
                                    {
                                        WeaponEmissive emissive;
                                        if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                        {
                                            createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                        }
                                        else
                                        {
                                            allEmissivesSet.Add(id + moveIndexer.Count, new EmissiveState());
                                            currentEmissivePart.Add(-1);
                                        }

                                        moveIndexer.Add(new[]
                                            {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, currentEmissivePart.Count - 1});
                                    }
                                }
                            }

                        }

                        var loop = false;
                        var reverse = false;
                        var triggerOnce = false;

                        if (animationSet.Loop != null && animationSet.Loop.Contains(moves.Key))
                            loop = true;

                        if (animationSet.Reverse != null && animationSet.Reverse.Contains(moves.Key))
                            reverse = true;

                        if (animationSet.TriggerOnce != null && animationSet.TriggerOnce.Contains(moves.Key))
                            triggerOnce = true;

                        var partAnim = new PartAnimation(id, rotationSet.ToArray(),
                            rotCenterSet.ToArray(), typeSet, currentEmissivePart.ToArray(), moveIndexer.ToArray(), animationSet.SubpartId[t], null, null,
                            animationSet.BarrelId, animationSet.StartupFireDelay, animationSet.AnimationDelays[moves.Key], system, loop, reverse, triggerOnce);

                        weaponLinearMoveSet.Add(id, moveSet.ToArray());

                        partAnim.RotCenterNameSet = rotCenterNameSet.ToArray();
                        allAnimationSet[moves.Key].Add(partAnim);
                    }
                }
            }

            weaponAnimationSets = allAnimationSet;
            weaponEmissivesSet = allEmissivesSet;

        }

        internal Dictionary<Weapon.EventTriggers, PartAnimation[]> CreateWeaponAnimationSet(Dictionary<Weapon.EventTriggers, HashSet<PartAnimation>> systemAnimations, RecursiveSubparts parts)
        {
            var allAnimationSet = new Dictionary<Weapon.EventTriggers, HashSet<PartAnimation>>();
            WeaponSystem system = null;
            foreach (var animationSet in systemAnimations)
            {
                allAnimationSet.Add(animationSet.Key, new HashSet<PartAnimation>());
                foreach (var animation in animationSet.Value)
                {

                    if (system == null) system = animation.System;

                    MyEntity part;
                    parts.NameToEntity.TryGetValue(animation.SubpartId, out part);
                    var subpart = part as MyEntitySubpart;
                    if (subpart == null) continue;

                    var rotations = new Matrix[animation.RotationSet.Length];
                    var rotCenters = new Matrix[animation.RotCenterSet.Length];
                    animation.RotationSet.CopyTo(rotations, 0);
                    animation.RotCenterSet.CopyTo(rotCenters, 0);

                    var rotCenterNames = animation.RotCenterNameSet;

                    var partCenter = GetPartLocation("subpart_" + animation.SubpartId, subpart.Parent.Model);



                    if (partCenter != null)
                    {
                        for (int i = 0; i < rotations.Length; i++)
                        {
                            if (rotations[i] != Matrix.Zero)
                                rotations[i] = Matrix.CreateTranslation(-(Vector3)partCenter) * rotations[i] *
                                               Matrix.CreateTranslation((Vector3)partCenter);
                        }

                        for (int i = 0; i < rotCenters.Length; i++)
                        {
                            if (rotCenters[i] != Matrix.Zero && rotCenterNames != null)
                            {
                                var dummyCenter = GetPartLocation(rotCenterNames[i], subpart.Model);
                                if (dummyCenter != null)
                                    rotCenters[i] = Matrix.CreateTranslation(-(Vector3)(partCenter + dummyCenter)) * rotCenters[i] * Matrix.CreateTranslation((Vector3)(partCenter + dummyCenter));
                            }


                        }
                    }

                    allAnimationSet[animationSet.Key].Add(new PartAnimation(animation.AnimationId, rotations, rotCenters,
                        animation.TypeSet, animation.CurrentEmissivePart, animation.MoveToSetIndexer, animation.SubpartId, subpart, parts.Entity,
                        animation.Muzzle, animation.FireDelay, animation.MotionDelay, system, animation.DoesLoop,
                        animation.DoesReverse, animation.TriggerOnce));
                }
            }

            try
            {
                foreach (var emissive in system.WeaponEmissiveSet)
                {
                    if (emissive.Value.EmissiveParts == null) continue;

                    foreach (var part in emissive.Value.EmissiveParts)
                    {
                        parts.SetEmissiveParts(part, Color.Transparent, 0);
                    }
                }
            }
            catch (Exception e)
            {
                //cant check for emissives so may be null ref
            }
            var returnAnimations = new Dictionary<Weapon.EventTriggers, PartAnimation[]>();

            foreach (var animationKV in allAnimationSet)
            {
                var set = animationKV.Value;
                returnAnimations[animationKV.Key] = new PartAnimation[set.Count];
                set.CopyTo(returnAnimations[animationKV.Key]);
            }

            return returnAnimations;
        }

        internal Matrix CreateRotation(double x, double y, double z)
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

            return rotation;
        }

        internal void createEmissiveStep(WeaponEmissive emissive, string id, float progress, ref Dictionary<string, EmissiveState> allEmissivesSet, ref List<int> currentEmissivePart)
        {
            var setColor = (Color)emissive.Colors[0];
            if (emissive.Colors.Length > 1)
            {
                if (progress < 1)
                {
                    float scaledTime = progress * (float)(emissive.Colors.Length - 1);
                    Color lastColor = emissive.Colors[(int)scaledTime];
                    Color nextColor = emissive.Colors[(int)(scaledTime + 1f)];
                    float scaledProgress = (float)(scaledTime * progress);
                    setColor = Color.Lerp(lastColor, nextColor, scaledProgress);
                }
                else
                    setColor = emissive.Colors[emissive.Colors.Length - 1];
            }

            var intensity = MathHelper.Lerp(emissive.IntensityRange[0],
                emissive.IntensityRange[1], progress);

            var currPart = (int)Math.Round(MathHelper.Lerp(0, emissive.EmissivePartNames.Length - 1, progress));

            allEmissivesSet.Add(id, new EmissiveState { CurrentColor = setColor, CurrentIntensity = intensity, EmissiveParts = emissive.EmissivePartNames, CycleParts = emissive.CycleEmissivesParts, LeavePreviousOn = emissive.LeavePreviousOn });
            currentEmissivePart.Add(currPart);
        }

        internal static Color[] CreateHeatEmissive()
        {
            var colors = new[]
            {
                new Color(10, 0, 0, 150),
                new Color(30, 0, 0, 150),
                new Color(250, .01f, 0, 180),
                new Color(240, .02f, 0, 200f),
                new Color(240, .03f, 0, 210f),
                new Color(220, .04f, 0, 230f),
                new Color(210, .05f, .01f, 240f),
                new Color(210, .05f, .02f, 255f),
                new Color(210, .05f, .03f, 255f),
                new Color(210, .04f, .04f, 255f),
                new Color(210, .03f, .05f, 255f)
            };

            var setColors = new Color[68];

            for (int i = 0; i <= 67; i++)
            {
                var progress = (float)i / 67;

                if (progress < 1)
                {
                    float scaledTime = progress * (colors.Length - 1);
                    Color lastColor = colors[(int)scaledTime];
                    Color nextColor = colors[(int)(scaledTime + 1f)];
                    float scaledProgress = scaledTime * progress;
                    setColors[i] = Color.Lerp(lastColor, nextColor, scaledProgress);
                }
                else
                    setColors[i] = colors[colors.Length - 1];
            }

            return setColors;
        }

        internal Vector3? GetPartLocation(string partName, IMyModel model)
        {
            Dictionary<string, IMyModelDummy> dummyList = new Dictionary<string, IMyModelDummy>();
            model.GetDummies(dummyList);

            IMyModelDummy dummy;
            if (dummyList.TryGetValue(partName, out dummy))
                return dummy.Matrix.Translation;

            return null;
        }

        internal IMyModelDummy GetPartDummy(string partName, IMyModel model)
        {
            Dictionary<string, IMyModelDummy> dummyList = new Dictionary<string, IMyModelDummy>();
            model.GetDummies(dummyList);

            IMyModelDummy dummy;
            if (dummyList.TryGetValue(partName, out dummy))
                return dummy;

            return null;
        }

        internal void ProcessAnimations()
        {
            PartAnimation anim;
            while (ThreadedAnimations.TryDequeue(out anim))
                AnimationsToProcess.Add(anim);

            for (int i = AnimationsToProcess.Count - 1; i >= 0; i--)
            {
                var animation = AnimationsToProcess[i];
                //var data = new AnimationParallelData(ref animation);
                if (!animation.MainEnt.MarkedForClose && animation.MainEnt != null)
                {
                    if (!animation.PauseAnimation && (animation.MotionDelay == 0 || animation.CurrentMove > 0 || (animation.MotionDelay > 0 && animation.StartTick <= Tick && animation.StartTick > 0)))
                    {
                        AnimateParts(animation);
                        animation.StartTick = 0;
                    }
                    else if (animation.MotionDelay > 0 && animation.StartTick == 0)
                        animation.StartTick = Tick + animation.MotionDelay;

                }
            }
        }

        internal void AnimateParts(PartAnimation animation)
        {
            var localMatrix = animation.Part.PositionComp.LocalMatrix;
            Matrix rotation;
            Matrix rotAroundCenter;
            Vector3D translation;
            AnimationType animationType;
            EmissiveState currentEmissive;

            animation.GetCurrentMove(out translation, out rotation, out rotAroundCenter, out animationType, out currentEmissive);


            if (animation.Reverse)
            {
                if (animationType == AnimationType.Movement) localMatrix.Translation = localMatrix.Translation - translation;

                animation.Previous();
                if (animation.Previous(false) == animation.NumberOfMoves - 1)
                {
                    animation.Reverse = false;
                }
            }
            else
            {
                if (animationType == AnimationType.Movement) localMatrix.Translation = localMatrix.Translation + translation;

                animation.Next();
                if (animation.DoesReverse && animation.Next(false) == 0)
                {
                    animation.Reverse = true;
                }
            }

            if (rotation != Matrix.Zero)
            {
                localMatrix *= animation.Reverse ? Matrix.Invert(rotation) : rotation;
            }

            if (rotAroundCenter != Matrix.Zero)
            {
                localMatrix *= animation.Reverse ? Matrix.Invert(rotAroundCenter) : rotAroundCenter;
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

            if (currentEmissive.EmissiveParts != null)
            {
                if (currentEmissive.CycleParts)
                {
                    animation.Part.SetEmissiveParts(currentEmissive.EmissiveParts[currentEmissive.CurrentPart], currentEmissive.CurrentColor,
                        currentEmissive.CurrentIntensity);
                    if (!currentEmissive.LeavePreviousOn)
                    {
                        var prev = currentEmissive.CurrentPart - 1 >= 0 ? currentEmissive.CurrentPart - 1 : currentEmissive.EmissiveParts
                            .Length - 1;
                        animation.Part.SetEmissiveParts(currentEmissive.EmissiveParts[prev],
                            Color.Transparent,
                            currentEmissive.CurrentIntensity);
                    }
                }
                else
                {
                    for (int i = 0; i < currentEmissive.EmissiveParts.Length; i++)
                    {
                        animation.Part.SetEmissiveParts(currentEmissive.EmissiveParts[i], currentEmissive.CurrentColor, currentEmissive.CurrentIntensity);
                    }
                }
            }

            if (!animation.Reverse && !animation.Looping && animation.CurrentMove == 0)
            {
                AnimationsToProcess.Remove(animation);
                animation.Running = false;
            }
        }
    }
}
