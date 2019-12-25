using System;
using System.Collections.Generic;
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
            if (_posChangedTick != Comp.Ai.Session.Tick)
                UpdatePivotPos();

            _posChangedTick = Comp.Ai.Session.Tick;
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

        internal void EventTriggerStateChanged(EventTriggers state, bool active, bool pause = false, HashSet<string> muzzles = null)
        {
            try
            {
                if (!Comp.Ai.Session.DedicatedServer)
                {
                    switch (state)
                    {
                        case EventTriggers.Firing:
                            if (AnimationsSet.ContainsKey(EventTriggers.Firing))
                            {
                                for (int i = 0; i < AnimationsSet[EventTriggers.Firing].Length; i++)
                                {
                                    var animation = AnimationsSet[EventTriggers.Firing][i];
                                    if (active && animation.Looping != true && !pause)
                                    {
                                        if (!animation.Running &&
                                            (animation.Muzzle == "Any" ||
                                             muzzles != null && muzzles.Contains(animation.Muzzle)))
                                        {
                                            if (animation.TriggerOnce && animation.Triggered) continue;

                                            if (animation.Muzzle != "Any") _muzzlesFiring.Add(animation.Muzzle);
                                            PartAnimation animCheck;
                                            if (AnimationLookup.TryGetValue(
                                                EventTriggers.StopFiring + animation.SubpartId, out animCheck))
                                            {
                                                if (animCheck.Running)
                                                    animCheck.Reverse = true;
                                                else
                                                {
                                                    /*if (animation.Part == AzimuthPart.Item1 || animation.Part == ElevationPart.Item1)
                                                    {
                                                        var matrix = animation.Part.PositionComp.LocalMatrix;
                                                        matrix.Translation = animation.HomePos.Translation;
                                                        animation.Part.PositionComp.LocalMatrix = matrix;
                                                    }
                                                    else
                                                        animation.Part.PositionComp.LocalMatrix = animation.HomePos;
                                                    */
                                                    Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                    animation.Running = true;
                                                }

                                                animation.Triggered = true;
                                            }
                                            else
                                            {
                                                Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                animation.Running = true;
                                                animation.Triggered = true;
                                            }

                                            if (animation.DoesLoop && !animation.TriggerOnce)
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
                                        animation.Triggered = false;
                                    }
                                }
                            }

                            break;
                        case EventTriggers.StopFiring:
                            if (AnimationsSet.ContainsKey(EventTriggers.StopFiring))
                            {
                                for (int i = 0; i < AnimationsSet[EventTriggers.StopFiring].Length; i++)
                                {
                                    var animation = AnimationsSet[EventTriggers.StopFiring][i];
                                    if (active && animation.Looping != true && !pause)
                                    {
                                        if (!animation.Running &&
                                            (animation.Muzzle == "Any" || _muzzlesFiring.Contains(animation.Muzzle)))
                                        {
                                            if (animation.TriggerOnce && animation.Triggered) continue;
                                            PartAnimation animCheck;
                                            if (AnimationLookup.TryGetValue(EventTriggers.Firing + animation.SubpartId,
                                                out animCheck))
                                            {
                                                if (animCheck.Running && animCheck.HasMovement)
                                                    animCheck.Reverse = true;
                                                else
                                                {
                                                    animCheck.Reset(false, false);
                                                    animCheck.Running = false;
                                                    Comp.Ai.Session.AnimationsToProcess.Remove(animCheck);
                                                    /*if (animation.Part == AzimuthPart.Item1 || animation.Part == ElevationPart.Item1)
                                                    {
                                                        var matrix = animation.Part.PositionComp.LocalMatrix;
                                                        matrix.Translation = animCheck.FinalPos.Translation;
                                                        animation.Part.PositionComp.LocalMatrix = matrix;
                                                    }
                                                    else
                                                        animation.Part.PositionComp.LocalMatrix = animCheck.FinalPos;
                                                        */
                                                    Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                    animation.Running = true;
                                                }

                                                animation.Triggered = true;
                                            }
                                            else
                                            {
                                                Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                animation.Running = true;
                                                animation.Triggered = true;
                                            }

                                            if (animation.DoesLoop && !animation.TriggerOnce)
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
                                        animation.Triggered = false;
                                    }
                                }

                                if (active) _muzzlesFiring.Clear();
                            }

                            break;
                        case EventTriggers.Reloading:
                            //possible Threaded event
                            var canReload = true;

                            if (AnimationsSet.ContainsKey(EventTriggers.Reloading))
                            {
                                if (AnimationsSet.ContainsKey(EventTriggers.TurnOn))
                                {
                                    for (int i = 0; i < AnimationsSet[EventTriggers.TurnOn].Length; i++)
                                    {
                                        var animation = AnimationsSet[EventTriggers.TurnOn][i];
                                        if (animation.Running)
                                            canReload = false;
                                    }
                                }

                                if (AnimationsSet.ContainsKey(EventTriggers.TurnOff))
                                {
                                    for (int i = 0; i < AnimationsSet[EventTriggers.TurnOff].Length; i++)
                                    {
                                        var animation = AnimationsSet[EventTriggers.TurnOff][i];
                                        if (animation.Running)
                                            canReload = false;
                                    }
                                }

                                if (canReload)
                                {
                                    for (int i = 0; i < AnimationsSet[EventTriggers.Reloading].Length; i++)
                                    {
                                        var animation = AnimationsSet[EventTriggers.Reloading][i];
                                        if (active && !animation.Running)
                                        {
                                            if (animation.TriggerOnce && animation.Triggered) continue;
                                            Comp.Ai.Session.ThreadedAnimations.Enqueue(animation);
                                            //Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                            animation.Running = true;
                                            animation.Triggered = true;
                                            if (animation.DoesLoop && !animation.TriggerOnce)
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
                            }

                            break;
                        case EventTriggers.StopTracking:
                            if (AnimationsSet.ContainsKey(EventTriggers.StopTracking))
                            {
                                for (int i = 0; i < AnimationsSet[EventTriggers.StopTracking].Length; i++)
                                {
                                    var animation = AnimationsSet[EventTriggers.StopTracking][i];
                                    if (active)
                                    {
                                        if (!animation.Running)
                                        {
                                            if (animation.TriggerOnce && animation.Triggered) continue;

                                            PartAnimation animCheck;
                                            if (AnimationLookup.TryGetValue(
                                                EventTriggers.Tracking + animation.SubpartId, out animCheck))
                                            {
                                                if (animCheck.Running)
                                                    animCheck.Reverse = true;
                                                else
                                                {
                                                    if (animation.Part == AzimuthPart.Item1 ||
                                                        animation.Part == ElevationPart.Item1)
                                                    {
                                                        var matrix = animation.Part.PositionComp.LocalMatrix;
                                                        matrix.Translation = animCheck.FinalPos.Translation;
                                                        animation.Part.PositionComp.LocalMatrix = matrix;
                                                    }
                                                    else
                                                        animation.Part.PositionComp.LocalMatrix = animCheck.FinalPos;

                                                    Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                    animation.Running = true;
                                                }

                                                animation.Triggered = true;
                                            }
                                            else
                                            {
                                                Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                animation.Running = true;
                                                animation.Triggered = true;
                                            }

                                            if (animation.DoesLoop && !animation.TriggerOnce)
                                                animation.Looping = true;
                                        }
                                        else if (animation.DoesLoop && !animation.TriggerOnce)
                                            animation.Looping = true;
                                    }
                                    else
                                    {
                                        animation.Looping = false;
                                        animation.Triggered = false;
                                    }

                                }
                            }

                            break;
                        case EventTriggers.Tracking:
                            if (AnimationsSet.ContainsKey(EventTriggers.Tracking))
                            {
                                for (int i = 0; i < AnimationsSet[EventTriggers.Tracking].Length; i++)
                                {
                                    var animation = AnimationsSet[EventTriggers.Tracking][i];
                                    if (active)
                                    {
                                        if (!animation.Running)
                                        {
                                            if (animation.TriggerOnce && animation.Triggered) continue;

                                            PartAnimation animCheck;
                                            if (AnimationLookup.TryGetValue(
                                                EventTriggers.StopTracking + animation.SubpartId, out animCheck))
                                            {
                                                if (animCheck.Running)
                                                    animCheck.Reverse = true;
                                                else
                                                {
                                                    if (animation.Part == AzimuthPart.Item1 ||
                                                        animation.Part == ElevationPart.Item1)
                                                    {
                                                        var matrix = animation.Part.PositionComp.LocalMatrix;
                                                        matrix.Translation = animation.HomePos.Translation;
                                                        animation.Part.PositionComp.LocalMatrix = matrix;
                                                    }
                                                    else
                                                        animation.Part.PositionComp.LocalMatrix = animation.HomePos;

                                                    Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                    animation.Running = true;
                                                }

                                                animation.Triggered = true;
                                            }
                                            else
                                            {
                                                Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                animation.Running = true;
                                                animation.Triggered = true;
                                            }

                                            if (animation.DoesLoop && !animation.TriggerOnce)
                                                animation.Looping = true;
                                        }
                                        else if (animation.DoesLoop && !animation.TriggerOnce)
                                            animation.Looping = true;
                                    }
                                    else
                                    {
                                        animation.Looping = false;
                                        animation.Triggered = false;
                                    }

                                }
                            }

                            break;
                        case EventTriggers.Overheated:
                            if (AnimationsSet.ContainsKey(EventTriggers.Overheated))
                            {
                                for (int i = 0; i < AnimationsSet[EventTriggers.Overheated].Length; i++)
                                {
                                    var animation = AnimationsSet[EventTriggers.Overheated][i];
                                    if (active && !animation.Running && animation.Looping != true)
                                    {
                                        if (animation.TriggerOnce && animation.Triggered) continue;

                                        Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                        animation.Running = true;
                                        animation.Triggered = true;
                                        if (animation.DoesLoop)
                                            animation.Looping = true;
                                    }
                                    else if (!active)
                                    {
                                        animation.Looping = false;
                                        animation.Triggered = false;
                                    }
                                }
                            }

                            break;

                        case EventTriggers.TurnOn:
                            //Threaded event
                            Session.ComputeStorage(this);
                            if (active && AnimationsSet.ContainsKey(EventTriggers.TurnOn))
                            {
                                for (int i = 0; i < AnimationsSet[EventTriggers.TurnOn].Length; i++)
                                {
                                    var animation = AnimationsSet[EventTriggers.TurnOn][i];

                                    if (!animation.Running)
                                    {
                                        if (animation.TriggerOnce && animation.Triggered) continue;

                                        PartAnimation animCheck;
                                        if (AnimationLookup.TryGetValue(EventTriggers.TurnOff + animation.SubpartId,
                                            out animCheck))
                                        {
                                            if (animCheck.Running)
                                                animCheck.Reverse = true;
                                            else
                                            {
                                                animation.Part.PositionComp.LocalMatrix = animCheck.FinalPos;
                                                Comp.Ai.Session.ThreadedAnimations.Enqueue(animation);
                                                //Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                animation.Running = true;
                                            }

                                            animation.Triggered = true;
                                        }
                                        else
                                        {
                                            Comp.Ai.Session.ThreadedAnimations.Enqueue(animation);
                                            //Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                            animation.Running = true;
                                            animation.Triggered = true;
                                        }
                                    }
                                    else
                                        animation.Reverse = false;
                                }

                                foreach (var set in AnimationsSet)
                                {
                                    for (int j = 0; j < set.Value.Length; j++)
                                    {
                                        var animation = set.Value[j];

                                        Azimuth = Elevation = 0;

                                        if (!AiOnlyWeapon) Comp.MissileBase.Azimuth = Comp.MissileBase.Elevation = 0;

                                        if (animation.Running && set.Key != EventTriggers.TurnOff &&
                                            set.Key != EventTriggers.TurnOn)
                                        {
                                            Comp.Ai.Session.AnimationsToProcess.Remove(animation);
                                            animation.Running = false;
                                            if (System.AnimationIdLookup.Contains(
                                                    EventTriggers.TurnOff + animation.SubpartId) ||
                                                System.AnimationIdLookup.Contains(
                                                    EventTriggers.TurnOn + animation.SubpartId))
                                                animation.Reset(false, false);
                                            else
                                                animation.Reset();
                                        }
                                        else if (animation.Running)
                                            animation.Looping = false;
                                    }
                                }
                            }

                            break;

                        case EventTriggers.TurnOff:
                            //Threaded event
                            if (active && AnimationsSet.ContainsKey(EventTriggers.TurnOff))
                            {
                                for (int i = 0; i < AnimationsSet[EventTriggers.TurnOff].Length; i++)
                                {
                                    var animation = AnimationsSet[EventTriggers.TurnOff][i];

                                    if (!animation.Running)
                                    {
                                        if (animation.TriggerOnce && animation.Triggered) continue;

                                        PartAnimation animCheck;
                                        if (AnimationLookup.TryGetValue(EventTriggers.TurnOn + animation.SubpartId,
                                            out animCheck))
                                        {
                                            if (animCheck.Running)
                                                animCheck.Reverse = true;
                                            else
                                            {
                                                animation.Part.PositionComp.LocalMatrix = animation.HomePos;
                                                Comp.Ai.Session.ThreadedAnimations.Enqueue(animation);
                                                //Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                                animation.Running = true;
                                            }

                                            animation.Triggered = true;
                                        }
                                        else
                                        {
                                            Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                            animation.Running = true;
                                            animation.Triggered = true;
                                        }
                                    }
                                    else
                                        animation.Reverse = false;
                                }

                                foreach (var set in AnimationsSet)
                                {
                                    for (int j = 0; j < set.Value.Length; j++)
                                    {
                                        var animation = set.Value[j];

                                        Azimuth = Elevation = 0;

                                        if (!AiOnlyWeapon) Comp.MissileBase.Azimuth = Comp.MissileBase.Elevation = 0;

                                        if (animation.Running && set.Key != EventTriggers.TurnOff &&
                                            set.Key != EventTriggers.TurnOn)
                                        {
                                            Comp.Ai.Session.AnimationsToProcess.Remove(animation);
                                            animation.Running = false;
                                            if (System.AnimationIdLookup.Contains(
                                                    EventTriggers.TurnOff + animation.SubpartId) ||
                                                System.AnimationIdLookup.Contains(
                                                    EventTriggers.TurnOn + animation.SubpartId))
                                                animation.Reset(false, false);
                                            else
                                                animation.Reset();
                                        }
                                        else if (animation.Running)
                                            animation.Looping = false;
                                    }
                                }
                            }

                            break;

                        case EventTriggers.EmptyOnGameLoad:
                            //Threaded event
                            if (AnimationsSet.ContainsKey(EventTriggers.EmptyOnGameLoad))
                            {
                                for (int i = 0; i < AnimationsSet[EventTriggers.EmptyOnGameLoad].Length; i++)
                                {
                                    var animation = AnimationsSet[EventTriggers.EmptyOnGameLoad][i];
                                    if (active && !animation.Running)
                                    {
                                        //Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                        Comp.Ai.Session.ThreadedAnimations.Enqueue(animation);
                                        animation.Running = true;
                                    }
                                }
                            }

                            break;

                        case EventTriggers.OutOfAmmo:
                        case EventTriggers.BurstReload:
                            if (AnimationsSet.ContainsKey(state))
                            {
                                for (int i = 0; i < AnimationsSet[state].Length; i++)
                                {
                                    var animation = AnimationsSet[state][i];
                                    if (active && !animation.Running)
                                    {
                                        if (animation.TriggerOnce && animation.Triggered) continue;

                                        Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                        animation.Running = true;
                                        animation.Triggered = true;
                                        if (animation.DoesLoop)
                                            animation.Looping = true;
                                    }
                                    else
                                    {
                                        animation.Looping = false;
                                        animation.Triggered = false;
                                    }
                                }
                            }

                            break;
                        case EventTriggers.PreFire:
                            if (AnimationsSet.ContainsKey(state))
                            {
                                for (int i = 0; i < AnimationsSet[state].Length; i++)
                                {
                                    var animation = AnimationsSet[state][i];
                                    if (active && !animation.Running &&
                                        (animation.Muzzle == "Any" || muzzles.Contains(animation.Muzzle)))
                                    {
                                        if (animation.TriggerOnce && animation.Triggered) continue;

                                        Comp.Ai.Session.AnimationsToProcess.Add(animation);
                                        animation.Running = true;
                                        animation.Triggered = true;
                                        if (animation.DoesLoop)
                                            animation.Looping = true;
                                    }
                                    else
                                    {
                                        animation.Looping = false;
                                        animation.Triggered = false;
                                    }
                                }
                            }

                            break;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in EventTriggerStateChanged: {ex}"); }
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
            ShotEnergyCost = ewar ? System.Values.HardPoint.EnergyCost * AreaEffectDmg : System.Values.HardPoint.EnergyCost * BaseDamage;
        }

        internal void UpdateBarrelRotation()
        {
            if (Comp.MyCube != null && !Comp.MyCube.MarkedForClose)
            {
                var rof = System.HasBarrelRate ? BarrelSpinRate < 3599 ? BarrelSpinRate : 3599 : RateOfFire < 3599 ? RateOfFire : 3599;

                var axis = System.Values.HardPoint.RotateBarrelAxis;
                if (axis != 0 && MuzzlePart.Item1 != Comp.MyCube)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        var multi = ((float)(i + 1))/10;

                        var angle = MathHelper.ToRadians(((360f / System.Barrels.Length) / (3600f / rof)) * multi);

                        switch (axis)
                        {
                            case 1:
                                BarrelRotationPerShot[i] = MuzzlePart.Item2 * Matrix.CreateRotationX(angle) * MuzzlePart.Item3;
                                break;
                            case 2:
                                BarrelRotationPerShot[i] = MuzzlePart.Item2 * Matrix.CreateRotationY(angle) * MuzzlePart.Item3;
                                break;
                            case 3:
                                BarrelRotationPerShot[i] = MuzzlePart.Item2 * Matrix.CreateRotationZ(angle) * MuzzlePart.Item3;
                                break;
                        }
                    }
                }
            }
        }

        public void ShootGraphics(bool stop = false)
        {
            if (System.BarrelEffect1 || System.BarrelEffect2)
            {
                var removal = false;
                var tick = Comp.Ai.Session.Tick;
                if (Comp.Ai.VelocityUpdateTick != tick)
                {
                    Comp.Ai.GridVel = Comp.Ai.MyGrid.Physics.LinearVelocity;
                    Comp.Ai.VelocityUpdateTick = tick;
                }
                foreach (var barrelPair in BarrelAvUpdater)
                {
                    var lastUpdateTick = barrelPair.Value;
                    var muzzle = barrelPair.Key;
                    var id = muzzle.MuzzleId;
                    var dummy = Dummies[id];
                    var ticksAgo = tick - lastUpdateTick;

                    var particles = System.Values.Graphics.Particles;

                    var pos = dummy.Info.Position;
                    var entityExists = MuzzlePart.Item1?.Parent != null && !MuzzlePart.Item1.MarkedForClose;
                    var matrix = MatrixD.Zero;
                    if (entityExists) matrix = MatrixD.CreateWorld(pos, MuzzlePart.Item1.WorldMatrix.Forward, MuzzlePart.Item1.Parent.WorldMatrix.Up);

                    if (System.BarrelEffect1)
                    {
                        if (entityExists && ticksAgo <= System.Barrel1AvTicks && !stop)
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
                        if (entityExists && ticksAgo <= System.Barrel2AvTicks && !stop)
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
            if (ShotEnergyCost > 0 && !IsShooting && !System.DesignatorWeapon)
            {
                EventTriggerStateChanged(EventTriggers.StopFiring, false);
                Comp.CurrentDps += Dps;
                if (!Comp.UnlimitedPower)
                {
                    if (Comp.SinkPower <= Comp.IdlePower) Comp.SinkPower = 0;
                    Comp.Ai.CurrentWeaponsDraw += UseablePower;
                    Comp.SinkPower += UseablePower;
                    Comp.Ai.GridAvailablePower -= UseablePower;
                    Comp.MyCube.ResourceSink.Update();
                }

                Comp.TerminalRefresh();
            }
            IsShooting = true;
        }

        public void StopShooting(bool avOnly = false)
        {
            StopFiringSound(false);
            StopRotateSound();
            ShootGraphics(true);
            _barrelRate = 0;
            if (!avOnly)
            {
                _ticksUntilShoot = 0;
                PreFired = false;
                if (ShotEnergyCost > 0 && IsShooting && !System.DesignatorWeapon)
                {
                    EventTriggerStateChanged(EventTriggers.Firing, false);
                    EventTriggerStateChanged(EventTriggers.StopFiring, true);
                    Comp.CurrentDps = Comp.CurrentDps - Dps > 0 ? Comp.CurrentDps - Dps : 0;
                    if (!Comp.UnlimitedPower)
                    {
                        DrawingPower = false;
                        Comp.Ai.RequestedWeaponsDraw -= RequiredPower;
                        Comp.Ai.CurrentWeaponsDraw -= UseablePower;
                        Comp.SinkPower -= UseablePower;
                        Comp.Ai.GridAvailablePower += UseablePower;

                        DelayTicks = 0;
                        if (Comp.SinkPower < Comp.IdlePower) Comp.SinkPower = Comp.IdlePower;
                        Comp.MyCube.ResourceSink.Update();
                    }
                    Comp.TerminalRefresh();
                }
                IsShooting = false;
            }
        }

        public void StartReload()
        {
            if (Reloading) return;
            Reloading = true;
            EventTriggerStateChanged(state: EventTriggers.Firing, active: false);

            if (IsShooting)
            {
                StopShooting();
            }

            if ((Comp.State.Value.Weapons[WeaponId].CurrentMags == 0 && !Comp.Ai.Session.IsCreative))
            {
                if (!OutOfAmmo)
                {
                    EventTriggerStateChanged(EventTriggers.OutOfAmmo, true);
                    OutOfAmmo = true;
                }
                Reloading = false;
            }
            else
            {
                if (OutOfAmmo)
                {
                    EventTriggerStateChanged(EventTriggers.OutOfAmmo, false);
                    OutOfAmmo = false;
                }

                EventTriggerStateChanged(EventTriggers.Reloading, true);

                Comp.Ai.Session.FutureEvents.Schedule(Reloaded, this, (uint)System.ReloadTime);

                if (ReloadEmitter == null || ReloadEmitter.IsPlaying) return;
                ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);

            }
        }

        internal static void Reloaded(object o)
        {
            var w = o as Weapon;
            w.EventTriggerStateChanged(EventTriggers.Reloading, false);
            w.Comp.BlockInventory.RemoveItemsOfType(1, w.System.AmmoDefId);
            w.Comp.State.Value.Weapons[w.WeaponId].CurrentAmmo = w.System.MagazineDef.Capacity;
            w.Comp.State.Value.Weapons[w.WeaponId].ShotsFired = 1;
            w.Reloading = false;
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
            LastTargetTick = Comp.Ai.Session.Tick;
        }
    }
}
