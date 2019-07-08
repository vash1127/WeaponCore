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

        private void ShootGraphics()
        {
            if (System.TurretEffect1 || System.TurretEffect2)
            {
                var particles = Kind.Graphics.Particles;
                var vel = Comp.Physics.LinearVelocity;
                var dummy = Dummies[NextMuzzle];
                var pos = dummy.Info.Position;
                var matrix = MatrixD.CreateWorld(pos, EntityPart.WorldMatrix.Forward, EntityPart.Parent.WorldMatrix.Up);

                if (System.TurretEffect1)
                {
                    if (MuzzleEffect1 == null)
                        MyParticlesManager.TryCreateParticleEffect(particles.Turret1Particle, ref matrix, ref pos, uint.MaxValue, out MuzzleEffect1);
                    else if (particles.Turret1Restart && MuzzleEffect1.IsEmittingStopped)
                        MuzzleEffect1.Play();

                    if (MuzzleEffect1 != null)
                    {
                        MuzzleEffect1.WorldMatrix = matrix;
                        MuzzleEffect1.Velocity = vel;
                    }
                }

                if (System.TurretEffect2)
                {
                    if (MuzzleEffect2 == null)
                        MyParticlesManager.TryCreateParticleEffect(particles.Turret2Particle, ref matrix, ref pos, uint.MaxValue, out MuzzleEffect2);
                    else if (particles.Turret2Restart && MuzzleEffect2.IsEmittingStopped)
                        MuzzleEffect2.Play();

                    if (MuzzleEffect2 != null)
                    {
                        MuzzleEffect2.WorldMatrix = matrix;
                        MuzzleEffect2.Velocity = vel;
                    }
                }
            }
        }

        public void StartShooting()
        {
            Log.Line($"starting sound: Name:{System.WeaponName} - PartName:{System.PartName} - IsTurret:{Kind.HardPoint.IsTurret}");
            if (FiringEmitter != null) StartFiringSound();
            if (System.ShotEnergyCost > 0)
            {
                var hardPoint = System.Kind.HardPoint;
                var gameStep = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
                var powerRequired = ((System.ShotEnergyCost * (hardPoint.RateOfFire * gameStep)) * hardPoint.BarrelsPerShot) * hardPoint.ShotsPerBarrel;
                Comp.SinkPower = powerRequired;
                Comp.Sink.Update();
                Log.Line($"BaseCost PerSec:{System.ShotEnergyCost * (hardPoint.RateOfFire * gameStep)} - PerShotCost:{System.ShotEnergyCost} - ShotsPerSec:{(hardPoint.RateOfFire * gameStep)} - 1St Multi:{hardPoint.BarrelsPerShot} - 2nd Multi:{hardPoint.ShotsPerBarrel} - Total:{powerRequired}");
            }
            IsShooting = true;
        }

        public void StopShooting(bool avOnly = false)
        {
            if (MuzzleEffect2 != null)
            {
                MuzzleEffect2.Stop(true);
                MuzzleEffect2 = null;
            }

            if (MuzzleEffect1 != null)
            {
                MuzzleEffect1.Stop(false);
                MuzzleEffect1 = null;
            }
            Log.Line("stop shooting");
            StopFiringSound(false);
            StopRotateSound();
            if (!avOnly)
            {
                TicksUntilShoot = 0;
                IsShooting = false;
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
