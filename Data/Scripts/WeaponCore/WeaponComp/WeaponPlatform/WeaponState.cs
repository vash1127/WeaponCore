using System;
using System.Linq;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public void PositionChanged(MyPositionComponentBase pComp)
        {
            if (Comp.LastPivotUpdateTick != Session.Instance.Tick && !Target.Expired)
                Comp.UpdatePivotPos(this);

            _posChangedTick = Session.Instance.Tick;
        }

        internal void UpdatePartPos(MyPositionComponentBase pComp)
        {
            var tick = Session.Instance.Tick;

            if  (Comp.LastPivotUpdateTick != Session.Instance.Tick && !Target.Expired)
                PositionChanged(pComp);

            if (Comp.PositionUpdateTick <= tick && Comp.LastPivotUpdateTick != tick)
            {
                if (EntityPart == null || EntityPart.MarkedForClose) return;
                var parentMatrix = EntityPart.Parent.PositionComp.WorldMatrix;
                EntityPart.PositionComp.UpdateWorldMatrix(ref parentMatrix);
                Comp.PositionUpdateTick = tick + 1;
            }
        }

        public class Muzzle
        {
            public Muzzle(int id)
            {
                MuzzleId = id;
            }

            public Vector3D Position;
            public Vector3D Direction;
            public Vector3D DeviatedDir;
            public uint LastShot;
            public uint LastUpdateTick;
            public int MuzzleId;
        }

        internal void EventTriggerStateChanged(EventTriggers state, bool active, bool pause = false, string muzzle = "Any")
        {
            switch (state)
            {
                case EventTriggers.Firing:
                    if (AvCapable && !pause)
                    {
                        var stages = System.Values.Graphics.Emissive.Firing.Stages;
                        var stageSize = TicksPerShot / 6;
                        if (stageSize < 2) stageSize = 2;
                        var timeToShoot = TicksPerShot - ShotCounter;
                        var stage = timeToShoot / stageSize - 1;
                        if (stage == 0) stage = 1;
                        var fIntensity = 1 / 6;
                        var firingColor = System.Values.Graphics.Emissive.Firing.Color;
                        for (int i = 0; i < stages; i++)
                        {
                            if (stage < 0 || !active)
                                EntityPart.SetEmissiveParts(FiringStrings[i], Color.Transparent, 0);
                            else if (stage >= i)
                                EntityPart.SetEmissiveParts(FiringStrings[i], firingColor, fIntensity * i);
                        }
                    }

                    if (AnimationsSet.ContainsKey(EventTriggers.Firing))
                    {
                        foreach (var animation in AnimationsSet[EventTriggers.Firing])
                        {
                            if (active && animation.Looping != true && !pause)
                            {
                                if (animation.Muzzle == "Any" || animation.Muzzle == muzzle)
                                {
                                    Session.Instance.animationsToProcess.Enqueue(animation);
                                    if (animation.DoesLoop)
                                        animation.Looping = true;
                                }
                            }
                            else if (active && animation.Looping && pause)
                                animation.PauseAnimation = true;

                            else if (active && animation.Looping)
                                animation.PauseAnimation = false;

                            else
                            {
                                animation.PauseAnimation = false;
                                animation.Looping = false;
                            }
                        }
                    }


                    break;
                case EventTriggers.Reloading:
                    if (AvCapable)
                    {
                        var reloadColor = System.Values.Graphics.Emissive.Reloading.Color;
                        var rIntensity = active ? 1 : reloadColor.W;
                        EntityPart.SetEmissiveParts("Reloading", reloadColor, rIntensity);
                    }

                    var canReload = true;

                    if (AnimationsSet.ContainsKey(EventTriggers.TurnOn))
                    {
                        foreach (var animation in AnimationsSet[EventTriggers.TurnOn])
                        {
                            if (Session.Instance.animationsToProcess.Contains(animation) ||
                                Session.Instance.animationsToQueue.Contains(animation))
                                canReload = false;
                        }
                    }

                    if (AnimationsSet.ContainsKey(EventTriggers.TurnOff))
                    {
                        foreach (var animation in AnimationsSet[EventTriggers.TurnOff])
                        {
                            if (Session.Instance.animationsToProcess.Contains(animation) ||
                                Session.Instance.animationsToQueue.Contains(animation))
                                canReload = false;
                        }
                    }


                    if (canReload && AnimationsSet.ContainsKey(EventTriggers.Reloading))
                    {
                        foreach (var animation in AnimationsSet[
                            EventTriggers.Reloading])
                        {
                            if (active && animation.Looping != true && !pause)
                            {
                                Session.Instance.animationsToProcess.Enqueue(animation);
                                if (animation.DoesLoop)
                                    animation.Looping = true;
                            }
                            else if (active && animation.Looping && pause)
                                animation.PauseAnimation = true;

                            else if (active && animation.Looping)
                                animation.PauseAnimation = false;

                            else
                            {
                                animation.PauseAnimation = false;
                                animation.Looping = false;
                            }
                        }
                    }

                    break;
                case EventTriggers.Tracking:
                    var trackingColor = System.Values.Graphics.Emissive.Tracking.Color;
                    var tIntensity = active ? 1 : trackingColor.W;
                    EntityPart.SetEmissiveParts("Tracking", trackingColor, tIntensity);
                    TargetWasExpired = Target.Expired;

                    if (AnimationsSet.ContainsKey(Weapon.EventTriggers.Tracking))
                    {
                        foreach (var animation in AnimationsSet[Weapon.EventTriggers.Tracking])
                        {
                            if (active)
                            {
                                if (animation.CurrentMove == 0 && !animation.Looping)
                                    Session.Instance.animationsToProcess.Enqueue(animation);

                                if (animation.DoesLoop)
                                    animation.Looping = true;
                            }
                            else
                                animation.Looping = false;
                        }
                    }

                    break;
                case EventTriggers.Overheated:
                    if (AvCapable)
                    {
                        var hIntensity = active ? 1 : 0.1f;
                        EntityPart.SetEmissiveParts("Heating", Color.Red, hIntensity);
                    }

                    if (AnimationsSet.ContainsKey(EventTriggers.Overheated))
                    {
                        foreach (var animation in AnimationsSet[EventTriggers.Overheated])
                        {
                            if (active && animation.Looping != true)
                            {
                                Session.Instance.animationsToProcess.Enqueue(animation);
                                if (animation.DoesLoop)
                                    animation.Looping = true;
                            }
                            else if (!active)
                                animation.Looping = false;
                        }
                    }

                    break;

                case EventTriggers.TurnOn:
                    if (active && AnimationsSet.ContainsKey(Weapon.EventTriggers.TurnOn))
                    {
                        foreach (var animation in AnimationsSet[Weapon.EventTriggers.TurnOn])
                        {
                            Session.Instance.animationsToProcess.Enqueue(animation);
                                if (animation.DoesLoop)
                                    animation.Looping = true;
                        }
                    }

                    break;

                case EventTriggers.TurnOff:
                    if (active && AnimationsSet.ContainsKey(Weapon.EventTriggers.TurnOff))
                    {
                        foreach (var animation in AnimationsSet[Weapon.EventTriggers.TurnOff])
                        {
                            Session.Instance.animationsToProcess.Enqueue(animation);
                            foreach (var set in AnimationsSet)
                            {
                                foreach (var anim in set.Value)
                                {
                                    anim.PauseAnimation = false;
                                    anim.Looping = false;
                                }
                            }
                        }
                    }
                    break;

                case EventTriggers.OutOfAmmo:
                    break;

                case EventTriggers.EmptyOnGameLoad:
                    if (AnimationsSet.ContainsKey(EventTriggers.EmptyOnGameLoad))
                    {
                        foreach (var animation in AnimationsSet[EventTriggers.EmptyOnGameLoad])
                        {
                            if (active)
                            {
                                Session.Instance.animationsToProcess.Enqueue(animation);
                            }
                        }
                    }

                    break;

                case EventTriggers.BurstReload:
                case EventTriggers.PreFire:
                    if (AnimationsSet.ContainsKey(state))
                    {
                        foreach (var animation in AnimationsSet[state])
                        {
                            if (active && animation.Looping != true)
                            {
                                Session.Instance.animationsToProcess.Enqueue(animation);
                                if (animation.DoesLoop)
                                    animation.Looping = true;
                            }
                            else
                                animation.Looping = false;
                        }
                    }
                    break;
            }
        }

        public void ShootGraphics()
        {
            if (System.BarrelEffect1 || System.BarrelEffect2)
            {
                var removal = false;    
                foreach (var barrelPair in BarrelAvUpdater)
                {
                    var lastUpdateTick = barrelPair.Value;
                    var muzzle = barrelPair.Key;
                    var id = muzzle.MuzzleId;
                    var dummy = Dummies[id];
                    var tick = Session.Instance.Tick;
                    var ticksAgo = tick - lastUpdateTick;

                    var particles = System.Values.Graphics.Particles;
                    var vel = Comp.Physics.LinearVelocity;
                    var pos = dummy.Info.Position;
                    var entityExists = EntityPart?.Parent != null && !EntityPart.MarkedForClose;
                    var matrix = MatrixD.Zero;
                    if (entityExists) matrix = MatrixD.CreateWorld(pos, EntityPart.WorldMatrix.Forward, EntityPart.Parent.WorldMatrix.Up);

                    if (System.BarrelEffect1)
                    {
                        if (entityExists && ticksAgo <= System.Barrel1AvTicks)
                        {
                            if (BarrelEffects1[id] == null)
                            {
                                var matrix1 = matrix;
                                matrix1.Translation += particles.Barrel1.Offset;
                                MyParticlesManager.TryCreateParticleEffect(particles.Barrel1.Name, ref matrix1, ref pos, uint.MaxValue, out BarrelEffects1[id]);
                                if (BarrelEffects1[id] != null)
                                {
                                    BarrelEffects1[id].UserColorMultiplier = particles.Barrel1.Color;
                                    BarrelEffects1[id].UserRadiusMultiplier = particles.Barrel1.Extras.Scale;
                                    BarrelEffects1[id].DistanceMax = particles.Barrel1.Extras.MaxDistance;
                                    BarrelEffects1[id].Loop = particles.Barrel1.Extras.Loop;
                                }
                            }
                            else if (particles.Barrel1.Extras.Restart && BarrelEffects1[id].IsEmittingStopped)
                                BarrelEffects1[id].Play();

                            if (BarrelEffects1[id] != null)
                            {
                                BarrelEffects1[id].WorldMatrix = matrix;
                                BarrelEffects1[id].Velocity = vel;
                            }
                        }
                        else if (BarrelEffects1[id] != null)
                        {
                            BarrelEffects1[id].Stop(true);
                            BarrelEffects1[id] = null;
                        }
                    }

                    if (System.BarrelEffect2)
                    {
                        if (entityExists && ticksAgo <= System.Barrel2AvTicks)
                        {
                            if (BarrelEffects2[id] == null)
                            {
                                var matrix2 = matrix;
                                matrix2.Translation += particles.Barrel2.Offset;
                                MyParticlesManager.TryCreateParticleEffect(particles.Barrel2.Name, ref matrix, ref pos, uint.MaxValue, out BarrelEffects2[id]);
                                if (BarrelEffects2[id] != null)
                                {
                                    BarrelEffects2[id].UserColorMultiplier = particles.Barrel2.Color;
                                    BarrelEffects2[id].UserRadiusMultiplier = particles.Barrel2.Extras.Scale;
                                    BarrelEffects2[id].DistanceMax = particles.Barrel2.Extras.MaxDistance;
                                    BarrelEffects2[id].Loop = particles.Barrel2.Extras.Loop;
                                }
                            }
                            else if (particles.Barrel2.Extras.Restart && BarrelEffects2[id].IsEmittingStopped)
                                BarrelEffects2[id].Play();

                            if (BarrelEffects2[id] != null)
                            {
                                BarrelEffects2[id].WorldMatrix = matrix;
                                BarrelEffects2[id].Velocity = vel;
                            }
                        }
                        else if (BarrelEffects2[id] != null)
                        {
                            BarrelEffects2[id].Stop(true);
                            BarrelEffects2[id] = null;
                        }
                    }

                    if (ticksAgo > System.Barrel1AvTicks && ticksAgo > System.Barrel2AvTicks)
                    {
                        removal = true;
                        BarrelAvUpdater.Remove(muzzle);
                    }
                }
                if (removal) BarrelAvUpdater.ApplyRemovals();
            }
        }

        public void StartShooting()
        {
            if (FiringEmitter != null) StartFiringSound();
            if (ShotEnergyCost > 0 && !IsShooting)
            {
                Comp.CurrentDPS += DPS;
                Comp.SinkPower += RequiredPower;
                Comp.CurrentSinkPowerRequested += RequiredPower;
                Comp.Sink.Update();
                Comp.TerminalRefresh();
            }
            IsShooting = true;
        }

        public void StopShooting(bool avOnly = false)
        {
            //Log.Line("stop shooting");
            StopFiringSound(false);
            StopRotateSound();
            ShootGraphics();
            if (!avOnly)
            {
                _ticksUntilShoot = 0;
                if (IsShooting)
                {
                    Comp.CurrentDPS -= DPS;
                    Comp.SinkPower = Comp.SinkPower - RequiredPower < Comp.IdlePower ? Comp.IdlePower : Comp.SinkPower - RequiredPower;
                    Comp.CurrentSinkPowerRequested = Comp.CurrentSinkPowerRequested - RequiredPower < Comp.IdlePower ? Comp.IdlePower : Comp.CurrentSinkPowerRequested - RequiredPower;
                    Comp.Sink.Update();
                    Comp.TerminalRefresh();
                }
                IsShooting = false;
            }
        }

        public void StartFiringSound()
        {
            FiringEmitter?.PlaySound(FiringSound, false);
        }

        public void StopFiringSound(bool force)
        {
            FiringEmitter?.StopSound(force, true);
        }

        public void StartReloadSound()
        {
            if (ReloadEmitter == null|| ReloadEmitter.IsPlaying) return;
            ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);
        }

        public void StopReloadSound()
        {
            ReloadEmitter?.StopSound(true, true);
        }

        public void StartRotateSound()
        {
            RotateEmitter?.PlaySound(RotateSound, true, false, false, false, false, false);
        }

        public void StopRotateSound()
        {
            RotateEmitter?.StopSound(true, true);
        }
    }
}
