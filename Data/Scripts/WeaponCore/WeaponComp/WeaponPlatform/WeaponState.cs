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
                    var tick = Comp.MyAi.MySession.Tick;
                    var ticksAgo = tick - lastUpdateTick;

                    var particles = Kind.Graphics.Particles;
                    var vel = Comp.Physics.LinearVelocity;
                    var pos = dummy.Info.Position;
                    var matrix = MatrixD.CreateWorld(pos, EntityPart.WorldMatrix.Forward, EntityPart.Parent.WorldMatrix.Up);

                    if (System.BarrelEffect1 && ticksAgo <= System.Barrel1AvTicks)
                    {
                        if (BarrelEffects1[id] == null)
                            MyParticlesManager.TryCreateParticleEffect(particles.Barrel1Particle, ref matrix, ref pos, uint.MaxValue, out BarrelEffects1[id]);
                        else if (particles.Barrel1Restart && BarrelEffects1[id].IsEmittingStopped)
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

                    if (System.BarrelEffect2 && ticksAgo <= System.Barrel2AvTicks)
                    {
                        if (BarrelEffects2[id] == null)
                            MyParticlesManager.TryCreateParticleEffect(particles.Barrel2Particle, ref matrix, ref pos, uint.MaxValue, out BarrelEffects2[id]);
                        else if (particles.Barrel2Restart && BarrelEffects2[id].IsEmittingStopped)
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
            //Log.Line($"starting sound: Name:{System.WeaponName} - PartName:{System.PartName} - IsTurret:{Kind.HardPoint.IsTurret}");
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
                _ticksUntilShoot = 0;
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
