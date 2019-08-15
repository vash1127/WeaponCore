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
            if(Comp.LastPivotUpdateTick != Session.Instance.Tick && Target.Expired)
                Comp.UpdatePivotPos(this);

            _posChangedTick = Session.Instance.Tick;
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

        internal void ChangeEmissiveState(Emissives state, bool active)
        {
            switch (state)
            {
                case Emissives.Firing:
                    var stages = System.Values.Graphics.Emissive.Firing.Stages;
                    var stageSize = _ticksPerShot / 6;
                    if (stageSize < 2) stageSize = 2;
                    var timeToShoot = _ticksPerShot - ShotCounter;
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
                    break;
                case Emissives.Reloading:
                    var reloadColor = System.Values.Graphics.Emissive.Reloading.Color;
                    var rIntensity = active ? 1 : reloadColor.W;
                    EntityPart.SetEmissiveParts("Reloading", reloadColor, rIntensity);
                    break;
                case Emissives.Tracking:
                    var trackingColor = System.Values.Graphics.Emissive.Tracking.Color;
                    var tIntensity = active ? 1 : trackingColor.W;
                    EntityPart.SetEmissiveParts("Tracking", trackingColor, tIntensity);
                    TargetWasExpired = Target.Expired;
                    break;
                case Emissives.Heating:
                    var hIntensity = active ? 1 : 0.1f;
                    EntityPart.SetEmissiveParts("Heating", Color.Red, hIntensity);
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
                    var tick = Comp.Ai.MySession.Tick;
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
                                MyParticlesManager.TryCreateParticleEffect(particles.Barrel1.Name, ref matrix, ref pos, uint.MaxValue, out BarrelEffects1[id]);
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
                                MyParticlesManager.TryCreateParticleEffect(particles.Barrel2.Name, ref matrix, ref pos, uint.MaxValue, out BarrelEffects2[id]);
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
            if (System.ShotEnergyCost > 0 && !IsShooting)
            {
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
            FiringEmitter.PlaySound(FiringSound, false);
            //Log.Line("Start Firing Sound");
        }

        public void StopFiringSound(bool force)
        {
            if (FiringEmitter == null)
                return;
            //Log.Line("Stop Firing Sound");
            FiringEmitter.StopSound(force, true);
        }

        public void StartReloadSound()
        {
            if (ReloadEmitter == null || ReloadEmitter.IsPlaying) return;
            //Log.Line("Start Reload Sound");
            ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);
        }

        public void StopReloadSound()
        {
            if (ReloadEmitter == null) return;
            //Log.Line("Stop Reload Sound");
            ReloadEmitter?.StopSound(true, true);
        }

        public void StartRotateSound()
        {
            //Log.Line("Start Rotate Sound");
            RotateEmitter.PlaySound(RotateSound, true, false, false, false, false, false);
        }

        public void StopRotateSound()
        {
            if (RotateEmitter == null) return;
            //Log.Line("Stop Rotate Sound");
            RotateEmitter.StopSound(true, true);
        }
    }
}
