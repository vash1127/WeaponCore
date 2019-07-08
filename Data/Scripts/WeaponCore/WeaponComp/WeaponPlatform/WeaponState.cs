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
            _posChangedTick = Session.Instance.Tick;
            if (!BarrelMove && (TrackingAi || !IsTurret)) Comp.UpdatePivotPos(this);
        }

        public class Muzzle
        {
            public Vector3D Position;
            public Vector3D Direction;
            public Vector3D DeviatedDir;
            public uint LastShot;
            public uint LastUpdateTick;
        }

        public void ShootGraphics()
        {
            if (System.BarrelEffect1 || System.BarrelEffect2)
            {
                var removal = false;
                var avSlot = 0;
                foreach (var barrelPair in BarrelAvUpdater)
                {
                    var slot = avSlot++;
                    var lastUpdateTick = barrelPair.Value;
                    var dummy = barrelPair.Key;
                    var tick = Comp.MyAi.MySession.Tick;
                    var ticksAgo = tick - lastUpdateTick;

                    var particles = Kind.Graphics.Particles;
                    var vel = Comp.Physics.LinearVelocity;
                    var pos = dummy.Info.Position;
                    var matrix = MatrixD.CreateWorld(pos, EntityPart.WorldMatrix.Forward, EntityPart.Parent.WorldMatrix.Up);

                    if (System.BarrelEffect1 && ticksAgo <= System.Barrel1AvTicks)
                    {
                        if (BarrelEffects1[slot] == null)
                            MyParticlesManager.TryCreateParticleEffect(particles.Barrel1Particle, ref matrix, ref pos, uint.MaxValue, out BarrelEffects1[slot]);
                        else if (particles.Barrel1Restart && BarrelEffects1[slot].IsEmittingStopped)
                            BarrelEffects1[slot].Play();

                        if (BarrelEffects1[slot] != null)
                        {
                            BarrelEffects1[slot].WorldMatrix = matrix;
                            BarrelEffects1[slot].Velocity = vel;
                        }
                    }
                    else if (BarrelEffects1[slot] != null)
                    {
                        BarrelEffects1[slot].Stop(true);
                        BarrelEffects1[slot] = null;
                    }

                    if (System.BarrelEffect2 && ticksAgo <= System.Barrel2AvTicks)
                    {
                        if (BarrelEffects2[slot] == null)
                            MyParticlesManager.TryCreateParticleEffect(particles.Barrel2Particle, ref matrix, ref pos, uint.MaxValue, out BarrelEffects2[slot]);
                        else if (particles.Barrel2Restart && BarrelEffects2[slot].IsEmittingStopped)
                            BarrelEffects2[slot].Play();

                        if (BarrelEffects2[slot] != null)
                        {
                            BarrelEffects2[slot].WorldMatrix = matrix;
                            BarrelEffects2[slot].Velocity = vel;
                        }
                    }
                    else if (BarrelEffects2[slot] != null)
                    {
                        BarrelEffects2[slot].Stop(true);
                        BarrelEffects2[slot] = null;
                    }

                    if (ticksAgo > System.Barrel1AvTicks && ticksAgo > System.Barrel2AvTicks)
                    {
                        removal = true;
                        BarrelAvUpdater.Remove(dummy);
                    }
                }
                if (removal) BarrelAvUpdater.ApplyRemovals();
            }
        }

        public void StartShooting()
        {
            Log.Line($"starting sound: Name:{System.WeaponName} - PartName:{System.PartName} - IsTurret:{Kind.HardPoint.IsTurret}");
            if (FiringEmitter != null) StartFiringSound();
            if (System.ShotEnergyCost > 0)
            {
                Comp.SinkPower += RequiredPower;
                Comp.Sink.Update();
            }
            IsShooting = true;
        }

        public void StopShooting(bool avOnly = false)
        {
            Log.Line("stop shooting");
            StopFiringSound(false);
            StopRotateSound();
            ShootGraphics();
            if (!avOnly)
            {
                TicksUntilShoot = 0;
                IsShooting = false;
                Comp.SinkPower -= RequiredPower;
                Comp.Sink.Update();
            }
        }


        public void StartFiringSound()
        {
            FiringEmitter.PlaySound(FiringSound, true);
            Log.Line("Start Firing Sound");
        }

        public void StopFiringSound(bool force)
        {
            if (FiringEmitter == null)
                return;
            Log.Line("Stop Firing Sound");
            FiringEmitter.StopSound(force);
        }

        public void StartReloadSound()
        {
            if (ReloadEmitter == null || ReloadEmitter.IsPlaying) return;
            Log.Line("Start Reload Sound");
            ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);
        }

        public void StopReloadSound()
        {
            if (ReloadEmitter == null) return;
            Log.Line("Stop Reload Sound");
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
