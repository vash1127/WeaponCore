using System;
using System.Collections.Generic;
using CoreSystems.Support;
using VRage.Game;
using VRageMath;
using static CoreSystems.Support.PartAnimation;
using static CoreSystems.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace CoreSystems.Platform
{
    public partial class Weapon
    {
        public void PlayEmissives(PartAnimation animation)
        {
            EmissiveState LastEmissive = new EmissiveState();
            for (int i = 0; i < animation.MoveToSetIndexer.Length; i++)
            {
                EmissiveState currentEmissive;
                if (System.PartEmissiveSet.TryGetValue(animation.EmissiveIds[animation.MoveToSetIndexer[i][(int)Indexer.EmissiveIndex]], out currentEmissive))
                {
                    currentEmissive.CurrentPart = animation.CurrentEmissivePart[animation.MoveToSetIndexer[i][(int)Indexer.EmissivePartIndex]];

                    if (currentEmissive.EmissiveParts != null && LastEmissive.EmissiveParts != null && currentEmissive.CurrentPart == LastEmissive.CurrentPart && currentEmissive.CurrentColor == LastEmissive.CurrentColor && Math.Abs(currentEmissive.CurrentIntensity - LastEmissive.CurrentIntensity) < 0.001)
                        currentEmissive = new EmissiveState();

                    LastEmissive = currentEmissive;


                    if (currentEmissive.EmissiveParts != null && currentEmissive.EmissiveParts.Length > 0)
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
                                    0);
                            }
                        }
                        else
                        {
                            for (int j = 0; j < currentEmissive.EmissiveParts.Length; j++)
                                animation.Part.SetEmissiveParts(currentEmissive.EmissiveParts[j], currentEmissive.CurrentColor, currentEmissive.CurrentIntensity);
                        }
                    }
                }
            }
        }

        public void StopShootingAv(bool power)
        {
            if (System.Values.HardPoint.Audio.FireSoundEndDelay > 0)
                Comp.Session.FutureEvents.Schedule(StopFiringSound, null, System.Values.HardPoint.Audio.FireSoundEndDelay);
            else StopFiringSound(false);

            StopPreFiringSound();
            if (AvCapable && RotateEmitter != null && RotateEmitter.IsPlaying) StopRotateSound();
            if (!power) StopRotateSound();
            
            StopBarrelAvTick = Comp.Session.Tick;

            for (int i = 0; i < Muzzles.Length; i++) {
                var muzzle = Muzzles[i];
                MyParticleEffect effect;
                if (System.Session.Av.BeamEffects.TryGetValue(muzzle.UniqueId, out effect)) {
                    effect.Stop();
                    System.Session.Av.BeamEffects.Remove(muzzle.UniqueId);
                }
            }
            IsShooting = false;
        }


        internal void PlayParticleEvent(EventTriggers eventTrigger, bool active, double distance, HashSet<string> muzzles)
        {
            if (ParticleEvents.ContainsKey(eventTrigger))
            {
                for (int i = 0; i < ParticleEvents[eventTrigger].Length; i++)
                {
                    var particle = ParticleEvents[eventTrigger][i];

                    if (active && particle.Restart && particle.Triggered) continue;

                    var obb = particle.MyDummy.Entity.PositionComp.WorldAABB;
                    //var inView = BaseComp.Session.Camera.IsInFrustum(ref obb);

                    var canPlay = true;
                    if (muzzles != null)
                    {
                        for (int j = 0; j < particle.MuzzleNames.Length; j++)
                        {
                            if (particle.MuzzleNames[j] == "Any" || muzzles.Contains(particle.MuzzleNames[j]))
                            {
                                canPlay = true;
                                break;
                            }
                        }
                    }
                    else
                        canPlay = true;

                    if (!canPlay) return;

                    if (active && !particle.Playing && distance <= particle.Distance)
                    {
                        particle.PlayTick = Comp.Session.Tick + particle.StartDelay;
                        Comp.Session.Av.ParticlesToProcess.Add(particle);
                        particle.Playing = true;
                        particle.Triggered = true;
                    }
                    else if (!active)
                    {
                        if (particle.Playing)
                            particle.Stop = true;

                        particle.Triggered = false;
                    }
                }
            }
        }


        internal void DelayedStart(object o)
        {
            EventTriggerStateChanged(EventTriggers.TurnOff, true);
        }

        internal void EventTriggerStateChanged(EventTriggers state, bool active, HashSet<string> muzzles = null)
        {
            if (Comp?.Data.Repo == null || Comp.Cube == null || Comp.Cube.MarkedForClose || Comp.Ai == null || Comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            try
            {
                var session = Comp.Session;
                var distance = Vector3D.DistanceSquared(session.CameraPos, MyPivotPos);
                var canPlay = !session.DedicatedServer && 64000000 >= distance; //8km max range, will play regardless of range if it moves PivotPos and is loaded

                if (canPlay)
                    PlayParticleEvent(state, active, distance, muzzles);

                if (!AnimationsSet.ContainsKey(state)) return;
                if (AnimationDelayTick < Comp.Session.Tick)
                    AnimationDelayTick = Comp.Session.Tick;

                var set = false;
                uint startDelay = 0;

                switch (state)
                {
                    case EventTriggers.StopFiring:
                    case EventTriggers.PreFire:
                    case EventTriggers.Firing:
                        {
                            var addToFiring = AnimationsSet.ContainsKey(EventTriggers.StopFiring) && state == EventTriggers.Firing;

                            for (int i = 0; i < AnimationsSet[state].Length; i++)
                            {
                                var animation = AnimationsSet[state][i];

                                if (active && !animation.Running && (animation.Muzzle == "Any" || (muzzles != null && muzzles.Contains(animation.Muzzle))))
                                {
                                    if (animation.TriggerOnce && animation.Triggered) continue;
                                    animation.Triggered = true;

                                    set = true;

                                    if (animation.Muzzle != "Any" && addToFiring) _muzzlesFiring.Add(animation.Muzzle);

                                    animation.StartTick = session.Tick + animation.MotionDelay;
                                    if (state == EventTriggers.StopFiring)
                                    {
                                        startDelay = AnimationDelayTick - session.Tick;
                                        animation.StartTick += startDelay;
                                    }

                                    Comp.Session.AnimationsToProcess.Add(animation);
                                    animation.Running = true;
                                    animation.CanPlay = canPlay;

                                    if (animation.DoesLoop)
                                        animation.Looping = true;
                                }
                                else if (active && animation.DoesLoop)
                                    animation.Looping = true;
                                else if (!active)
                                {
                                    animation.Looping = false;
                                    animation.Triggered = false;
                                }
                            }
                            if (active && state == EventTriggers.StopFiring)
                                _muzzlesFiring.Clear();
                            break;
                        }
                    case EventTriggers.StopTracking:
                    case EventTriggers.Tracking:
                        {
                            for (int i = 0; i < AnimationsSet[state].Length; i++)
                            {
                                var animation = AnimationsSet[state][i];
                                if (active && !animation.Running)
                                {
                                    if (animation.TriggerOnce && animation.Triggered) continue;

                                    set = true;
                                    animation.Triggered = true;
                                    animation.Running = true;
                                    animation.CanPlay = canPlay;

                                    Comp.Session.AnimationsToProcess.Add(animation);
                                    animation.StartTick = session.Tick + animation.MotionDelay;

                                    if (LastEvent == EventTriggers.StopTracking || LastEvent == EventTriggers.Tracking)
                                    {
                                        startDelay = (AnimationDelayTick - session.Tick);
                                        animation.StartTick += startDelay;
                                    }

                                    if (animation.DoesLoop)
                                        animation.Looping = true;
                                }
                                else if (active && animation.DoesLoop)
                                    animation.Looping = true;
                                else if (!active)
                                {
                                    animation.Looping = false;
                                    animation.Triggered = false;
                                }
                            }
                            break;
                        }
                    case EventTriggers.TurnOn:
                    case EventTriggers.TurnOff:
                        if (active)
                        {
                            for (int i = 0; i < AnimationsSet[state].Length; i++)
                            {
                                var animation = AnimationsSet[state][i];
                                if (!animation.Running)
                                {
                                    animation.Running = true;
                                    animation.CanPlay = true;

                                    animation.StartTick = session.Tick + animation.MotionDelay + (AnimationDelayTick - session.Tick);
                                    if (state == EventTriggers.TurnOff)
                                    {
                                        startDelay = OffDelay;
                                        animation.StartTick += startDelay;
                                    }

                                    session.ThreadedAnimations.Enqueue(animation);
                                }
                                else
                                    animation.Reverse = false;
                            }
                        }
                        break;
                    case EventTriggers.EmptyOnGameLoad:
                    case EventTriggers.Overheated:
                    case EventTriggers.NoMagsToLoad:
                    case EventTriggers.BurstReload:
                    case EventTriggers.Reloading:
                        {
                            for (int i = 0; i < AnimationsSet[state].Length; i++)
                            {
                                var animation = AnimationsSet[state][i];
                                if (animation == null) continue;

                                if (active && !animation.Running)
                                {
                                    if (animation.TriggerOnce && animation.Triggered) continue;
                                    animation.Triggered = true;

                                    set = true;

                                    animation.StartTick = session.Tick + animation.MotionDelay;
                                    session.ThreadedAnimations.Enqueue(animation);

                                    animation.Running = true;
                                    animation.CanPlay = canPlay;

                                    if (animation.DoesLoop)
                                        animation.Looping = true;
                                }
                                else if (active && animation.DoesLoop)
                                    animation.Looping = true;
                                else if (!active)
                                {
                                    animation.Looping = false;
                                    animation.Triggered = false;
                                }
                            }

                            break;
                        }
                }
                if (active && set)
                {
                    var animationLength = 0u;

                    LastEvent = state;
                    LastEventCanDelay = state == EventTriggers.Reloading || state == EventTriggers.StopFiring || state == EventTriggers.TurnOff || state == EventTriggers.TurnOn;

                    if (System.PartAnimationLengths.TryGetValue(state, out animationLength))
                    {
                        var delay = session.Tick + animationLength + startDelay;
                        if (delay > AnimationDelayTick)
                            AnimationDelayTick = delay;
                    }

                }
            }
            catch (Exception e)
            {
                Log.Line($"Exception in Event Triggered: {e}");
            }
        }
        public void StartPreFiringSound()
        {
            if (PreFiringEmitter == null)
                return;

            PreFiringEmitter.PlaySound(PreFiringSound);
        }

        public void StopPreFiringSound()
        {
            if (PreFiringEmitter == null)
                return;

            PreFiringEmitter.StopSound(true);
        }

        public void StartFiringSound()
        {
            if (FiringEmitter == null)
                return;

            FiringEmitter.PlaySound(FiringSound);
        }

        public void StopFiringSound(object o = null)
        {
            if (FiringEmitter == null)
                return;

            if (FiringEmitter.Loop)
			{
                FiringEmitter.StopSound(true);
				FiringEmitter.PlaySound(FiringSound, stopPrevious: false, skipIntro: true, force2D: false, alwaysHearOnRealistic: false, skipToEnd: true);
			}
            else
                FiringEmitter.StopSound(false);
        }

        public void StopReloadSound()
        {
            if (ReloadEmitter == null)
                return;

            ReloadEmitter.StopSound(true);
        }

        public void StopRotateSound()
        {
            if (RotateEmitter == null)
                return;

            if (RotateEmitter.Loop)
			{
				RotateEmitter.StopSound(true);
				RotateEmitter.PlaySound(RotateSound, stopPrevious: false, skipIntro: true, force2D: false, alwaysHearOnRealistic: false, skipToEnd: true);
			}
            else
                RotateEmitter.StopSound(true);
        }

    }
}
