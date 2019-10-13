using System.Collections.Generic;
using System.Linq;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public void PositionChanged(MyPositionComponentBase pComp)
        {
            if (_posChangedTick != Session.Instance.Tick)
                UpdatePivotPos();

            _posChangedTick = Session.Instance.Tick;
        }

        internal void UpdatePartPos(MyPositionComponentBase pComp)
        {
            var tick = Session.Instance.Tick;

            if (_posChangedTick != Session.Instance.Tick)
                UpdatePivotPos();

            if (Comp.PositionUpdateTick <= tick && _posChangedTick != tick)
            {
                if (BarrelPart == null || BarrelPart.MarkedForClose) return;
                var parentMatrix = BarrelPart.Parent.PositionComp.WorldMatrix;
                BarrelPart.PositionComp.UpdateWorldMatrix(ref parentMatrix);
                Comp.PositionUpdateTick = tick + 1;
            }
        }

        internal void EntPartClose(MyEntity obj)
        {
            obj.PositionComp.OnPositionChanged -= PositionChanged;
            obj.OnMarkForClose -= EntPartClose;
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

        //todo client side only
        internal void EventTriggerStateChanged(EventTriggers state, bool active, bool pause = false, HashSet<string> muzzles = null)
        {
            if (!Session.Instance.DedicatedServer)
            {
                switch (state)
                {
                    case EventTriggers.Firing:
                        if (AnimationsSet.ContainsKey(EventTriggers.Firing))
                        {
                            foreach (var animation in AnimationsSet[EventTriggers.Firing])
                            {
                                if (active && animation.Looping != true && !pause)
                                {
                                    if (!Session.Instance.animationsToProcess.Contains(animation) && (animation.Muzzle == "Any" || muzzles.Contains(animation.Muzzle)))
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
                                if (active && animation.Looping != true && !pause && !Session.Instance.animationsToProcess.Contains(animation))
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

                        if (AnimationsSet.ContainsKey(EventTriggers.Tracking))
                        {
                            foreach (var animation in AnimationsSet[EventTriggers.Tracking])
                            {
                                if (active)
                                {
                                    if (animation.CurrentMove == 0 && !animation.Looping)
                                    {
                                        if (!Session.Instance.animationsToProcess.Contains(animation))
                                            Session.Instance.animationsToProcess.Enqueue(animation);
                                        else
                                            animation.Looping = true;
                                    }

                                    if (animation.DoesLoop)
                                        animation.Looping = true;
                                }
                                else
                                    animation.Looping = false;
                            }
                        }

                        break;
                    case EventTriggers.Overheated:
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
                        Session.ComputeStorage(this);
                        if (active && AnimationsSet.ContainsKey(EventTriggers.TurnOn))
                        {
                            var OnAnimations = true;

                            if (AnimationsSet.ContainsKey(EventTriggers.TurnOff))
                            {
                                foreach (var animation in AnimationsSet[EventTriggers.TurnOff])
                                {
                                    if (Session.Instance.animationsToProcess.Contains(animation))
                                    {
                                        OnAnimations = false;
                                        animation.Reverse = true;
                                    }
                                }
                            }

                            if (OnAnimations)
                            {
                                foreach (var animation in AnimationsSet[EventTriggers.TurnOn])
                                {
                                    Session.Instance.animationsToProcess.Enqueue(animation);
                                    if (animation.DoesLoop)
                                        animation.Looping = true;
                                }
                            }
                        }

                        break;

                    case EventTriggers.TurnOff:
                        if (active && AnimationsSet.ContainsKey(EventTriggers.TurnOff))
                        {
                            var OffAnimations = true;

                            if (AnimationsSet.ContainsKey(EventTriggers.TurnOn))
                            {

                                foreach (var animation in AnimationsSet[EventTriggers.TurnOn])
                                {
                                    if (Session.Instance.animationsToProcess.Contains(animation))
                                    {
                                        OffAnimations = false;
                                        animation.Reverse = true;
                                    }
                                }
                            }
                            if (OffAnimations)
                            {

                                foreach (var animation in AnimationsSet[EventTriggers.TurnOff])
                                {
                                    animation.StartTick = OffDelay > 0
                                        ? Session.Instance.Tick + animation.MotionDelay + OffDelay
                                        : 0;

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
                        }

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

                    case EventTriggers.OutOfAmmo:
                    case EventTriggers.BurstReload:
                    case EventTriggers.PreFire:
                        if (AnimationsSet.ContainsKey(state))
                        {
                            foreach (var animation in AnimationsSet[state])
                            {
                                if (active && animation.Looping != true)
                                {
                                    if (!Session.Instance.animationsToProcess.Contains(animation))
                                        Session.Instance.animationsToProcess.Enqueue(animation);
                                    else
                                        animation.Looping = true;
                                }
                                else
                                    animation.Looping = false;
                            }
                        }
                        break;
                }
            }
        }

        internal void UpdateRequiredPower()
        {
            if (System.EnergyAmmo || System.IsHybrid)
                RequiredPower = ((ShotEnergyCost * (RateOfFire * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * System.Values.HardPoint.Loading.BarrelsPerShot) * System.Values.HardPoint.Loading.TrajectilesPerBarrel;
            else
                RequiredPower = Comp.IdlePower;

            Comp.MaxRequiredPower += RequiredPower;
        }

        internal void UpdateShotEnergy()
        {
            var ewar = (int)System.Values.Ammo.AreaEffect.AreaEffect > 3;
            ShotEnergyCost = ewar ? System.Values.HardPoint.EnergyCost * areaEffectDmg : System.Values.HardPoint.EnergyCost * BaseDamage;
        }

        internal void UpdateBarrelRotation()
        {
            if (!Comp.MyCube.MarkedForClose && Comp.MyCube != null)
            {
                var rof = RateOfFire < 3599 ? RateOfFire : 3599;

                var angle = MathHelper.ToRadians((360f / System.Barrels.Length) / (3600f / rof));


                var axis = System.Values.HardPoint.RotateBarrelAxis;
                if (axis != 0 && BarrelPart != Comp.MyCube)
                {
                    var partPos = (Vector3)Session.Instance.GetPartLocation("subpart_" + System.MuzzlePartName.String,
                        ((MyEntitySubpart)BarrelPart).Parent.Model);

                    var to = Matrix.CreateTranslation(-partPos);
                    var from = Matrix.CreateTranslation(partPos);

                    Matrix rotationMatrix = Matrix.Zero;
                    switch (axis)
                    {
                        case 1:
                            rotationMatrix = to * Matrix.CreateRotationX(angle) * from;
                            break;
                        case 2:
                            rotationMatrix = to * Matrix.CreateRotationY(angle) * from;
                            break;
                        case 3:
                            rotationMatrix = to * Matrix.CreateRotationZ(angle) * from;
                            break;
                    }

                    BarrelRotationPerShot = rotationMatrix;
                }
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
                    if (Comp.Ai.VelocityUpdateTick != tick)
                    {
                        Comp.Ai.GridVel = Comp.Ai.MyGrid.Physics.LinearVelocity;
                        Comp.Ai.VelocityUpdateTick = tick;
                    }

                    var pos = dummy.Info.Position;
                    var entityExists = BarrelPart?.Parent != null && !BarrelPart.MarkedForClose;
                    var matrix = MatrixD.Zero;
                    if (entityExists) matrix = MatrixD.CreateWorld(pos, BarrelPart.WorldMatrix.Forward, BarrelPart.Parent.WorldMatrix.Up);

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
                                BarrelEffects1[id].Velocity = Comp.Ai.GridVel;
                            }
                        }
                        else if (BarrelEffects1[id] != null)
                        {
                            BarrelEffects1[id].Stop();
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
                                BarrelEffects2[id].Velocity = Comp.Ai.GridVel;
                            }
                        }
                        else if (BarrelEffects2[id] != null)
                        {
                            BarrelEffects2[id].Stop();
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
            EventTriggerStateChanged(EventTriggers.Firing, false);
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

        public void StartReload()
        {
            EventTriggerStateChanged(EventTriggers.Reloading, true);
            EventTriggerStateChanged(EventTriggers.OutOfAmmo, false);
            LoadAmmoMag = true;

            if (ReloadEmitter == null || ReloadEmitter.IsPlaying) return;
            ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);
        }

        public void StartFiringSound()
        {
            FiringEmitter?.PlaySound(FiringSound);
        }

        public void StopFiringSound(bool force)
        {
            FiringEmitter?.StopSound(force);
        }

        public void StopReloadSound()
        {
            ReloadEmitter?.StopSound(true);
        }

        public void StartRotateSound()
        {
            RotateEmitter?.PlaySound(RotateSound, true, false, false, false, false, false);
        }

        public void StopRotateSound()
        {
            RotateEmitter?.StopSound(true);
        }

        internal void WakeTargets()
        {
            SleepTargets = false;
            SleepingTargets.Clear();
            LastTargetTick = Session.Instance.Tick;
        }
    }
}
