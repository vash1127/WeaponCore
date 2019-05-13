using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore
{
    public partial class Session
    {
        private void GenerateBeams(Weapon weapon)
        {
            var barrels = weapon.Muzzles;
            var fireBeam = new FiredBeam(weapon, _linePool.Get());
            foreach (var m in barrels)
            {
                if (Tick == m.LastShot)
                {
                    fireBeam.Beams.Add(new LineD(m.Position, m.Direction));
                }
            }
            lock (_projectiles) _projectiles.FiredBeams.Add(fireBeam);
        }

        private void GenerateBolts(Weapon weapon)
        {
            var barrels = weapon.Muzzles;
            var firedBolt = new FiredProjectile(weapon, _shotPool.Get());

            foreach (var m in barrels)
            {
                if (Tick == m.LastShot)
                {
                    firedBolt.Projectiles.Add(new Shot(m.Position, m.Direction));
                }
            }
            lock (_projectiles) _projectiles.Add(firedBolt);
        }

        private void GenerateMissiles(Weapon weapon)
        {
            var barrels = weapon.Muzzles;
            var firedMissile = new FiredProjectile(weapon, _shotPool.Get());

            foreach (var m in barrels)
            {
                if (Tick == m.LastShot)
                {
                    firedMissile.Projectiles.Add(new Shot(m.Position, m.Direction));
                }
            }
            lock (_projectiles) _projectiles.Add(firedMissile);
        }

        private void DrawBeam(DrawProjectile pInfo)
        {
            var cameraPos = MyAPIGateway.Session.Camera.Position;
            var beamScaledDir = pInfo.Projectile.From - pInfo.Projectile.To;
            var beamCenter = pInfo.Projectile.From + -(beamScaledDir * 0.5f);
            var distToBeam = Vector3D.DistanceSquared(cameraPos, beamCenter);
            if (distToBeam > 25000000) return;
            var beamSphereRadius = pInfo.Projectile.Length * 0.5;
            var beamSphere = new BoundingSphereD(beamCenter, beamSphereRadius);
            if (MyAPIGateway.Session.Camera.IsInFrustum(ref beamSphere))
            {
                var matrix = MatrixD.CreateFromDir(pInfo.Projectile.Direction);
                matrix.Translation = pInfo.Projectile.From;
                var weapon = pInfo.Weapon;
                var beamSlot = weapon.BeamSlot;
                var rgb = weapon.WeaponType.TrailColor;
                var radius = 0.15f;
                if (Tick % 6 == 0) radius = 0.14f;
                var mainBeam = new Vector4(rgb[0], rgb[1], rgb[2], 1f);
                if (pInfo.PrimeProjectile && Tick > beamSlot[pInfo.ProjectileId] && pInfo.HitPos != Vector3D.Zero)
                {
                    beamSlot[pInfo.ProjectileId] = Tick + 20;
                    BeamParticleStart(pInfo.Entity, pInfo.HitPos, mainBeam);
                }

                if (distToBeam < 1000000)
                {
                    if (distToBeam > 250000) radius *= 1.5f;
                    TransparentRenderExt.DrawTransparentCylinder(ref matrix, radius, radius, (float) pInfo.Projectile.Length, 6, mainBeam, mainBeam, WarpMaterial, WarpMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                }
                else MySimpleObjectDraw.DrawLine(pInfo.Projectile.From, pInfo.Projectile.To, ProjectileMaterial, ref mainBeam, 2f);
            }
        }

        private void DrawBolt(DrawProjectile pInfo)
        {
            var cameraPos = MyAPIGateway.Session.Camera.Position;
            var beamScaledDir = pInfo.Projectile.From - pInfo.Projectile.To;
            var beamCenter = pInfo.Projectile.From + -(beamScaledDir * 0.5f);
            var distToBeam = Vector3D.DistanceSquared(cameraPos, beamCenter);
            if (distToBeam > 25000000) return;
            var beamSphereRadius = pInfo.Projectile.Length * 0.5;
            var beamSphere = new BoundingSphereD(beamCenter, beamSphereRadius);
            if (MyAPIGateway.Session.Camera.IsInFrustum(ref beamSphere))
            {
                var matrix = MatrixD.CreateFromDir(pInfo.Projectile.Direction);
                matrix.Translation = pInfo.Projectile.From;

                var weapon = pInfo.Weapon;
                var beamSlot = weapon.BeamSlot;
                var rgb = weapon.WeaponType.TrailColor;
                var radius = 0.15f;
                if (Tick % 6 == 0) radius = 0.14f;

                var mainBeam = new Vector4(255, 0, 0, 175);
                if (pInfo.PrimeProjectile && Tick > beamSlot[pInfo.ProjectileId] && pInfo.HitPos != Vector3D.Zero)
                {
                    beamSlot[pInfo.ProjectileId] = Tick + 20;
                    BoltParticleStart(pInfo.Entity, pInfo.HitPos, mainBeam, Vector3D.Zero);
                }
                MySimpleObjectDraw.DrawLine(pInfo.Projectile.From, pInfo.Projectile.To, ProjectileMaterial, ref mainBeam, 0.1f);
            }
        }

        private void DrawMissile(DrawProjectile pInfo)
        {
            var cameraPos = MyAPIGateway.Session.Camera.Position;
            var beamScaledDir = pInfo.Projectile.From - pInfo.Projectile.To;
            var beamCenter = pInfo.Projectile.From + -(beamScaledDir * 0.5f);
            var distToBeam = Vector3D.DistanceSquared(cameraPos, beamCenter);
            if (distToBeam > 25000000) return;
            var beamSphereRadius = pInfo.Projectile.Length * 0.5;
            var beamSphere = new BoundingSphereD(beamCenter, beamSphereRadius);
            if (MyAPIGateway.Session.Camera.IsInFrustum(ref beamSphere))
            {
                var matrix = MatrixD.CreateFromDir(pInfo.Projectile.Direction);
                matrix.Translation = pInfo.Projectile.From;
                var weapon = pInfo.Weapon;
                var rgb = weapon.WeaponType.TrailColor;
                Vector4 mainBeam = new Vector4(255, 255, 255, 255);
                MySimpleObjectDraw.DrawLine(pInfo.Projectile.From, pInfo.Projectile.To, ProjectileMaterial, ref mainBeam, 0.2f);
            }
        }

        private MyParticleEffect _effect1 = new MyParticleEffect();

        internal void BeamParticleStart(IMyEntity ent, Vector3D pos, Vector4 color)
        {
            color = new Vector4(255, 10, 0, 1f); // comment out to use beam color
            var dist = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, pos);
            var logOfPlayerDist = Math.Log(dist);

            var mainParticle = 32;

            var size = 20;
            var radius = size / logOfPlayerDist;
            var vel = ent.Physics.LinearVelocity;
            var matrix = MatrixD.CreateTranslation(pos);
            MyParticlesManager.TryCreateParticleEffect(mainParticle, out _effect1, ref matrix, ref pos, uint.MaxValue, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53

            if (_effect1 == null) return;
            _effect1.Loop = false;
            _effect1.DurationMax = 0.3333333332f;
            _effect1.DurationMin = 0.3333333332f;
            _effect1.UserColorMultiplier = color;
            _effect1.UserRadiusMultiplier = (float) radius;
            _effect1.UserEmitterScale = 1;
            _effect1.Velocity = vel;
            _effect1.Play();
        }

        private void BoltParticleStart(IMyEntity ent, Vector3D pos, Vector4 color, Vector3D speed)
        {
            color = new Vector4(255, 10, 0, 1f); // comment out to use beam color
            var dist = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, pos);
            var logOfPlayerDist = Math.Log(dist);

            var mainParticle = 32;

            var size = 20;
            var radius = size / logOfPlayerDist;
            var vel = ent.Physics.LinearVelocity;
            var matrix = MatrixD.CreateTranslation(pos);
            MyParticlesManager.TryCreateParticleEffect(mainParticle, out _effect1, ref matrix, ref pos, uint.MaxValue, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53

            if (_effect1 == null) return;
            _effect1.Loop = false;
            _effect1.DurationMax = 0.3333333332f;
            _effect1.DurationMin = 0.3333333332f;
            _effect1.UserColorMultiplier = color;
            _effect1.UserRadiusMultiplier = (float)radius;
            _effect1.UserEmitterScale = 1;
            _effect1.Velocity = speed;
            _effect1.Play();
        }
    }
}
