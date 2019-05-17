using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Projectiles.Projectiles;
using static WeaponCore.Support.WeaponDefinition;
namespace WeaponCore
{
    public partial class Session
    {

        private void UpdateWeaponPlatforms()
        {
            var p = 0;
            for (int i = 0; i < Logic.Count; i++)
            {
                var logic = Logic[i];
                if (logic.State.Value.Online)
                {
                    for (int j = 0; j < logic.Platform.Weapons.Length; j++)
                    {
                        if (j != 0) continue;
                        var w = logic.Platform.Weapons[j];
                        if (w.TrackTarget && w.TargetSwap) w.SelectTarget();
                        if (w.TurretMode && w.Target != null) w.Rotate();
                        if (w.TrackTarget && w.ReadyToTrack) logic.Turret.TrackTarget(w.Target);
                        if (w.ReadyToShoot) w.Shoot();
                        if (w.ShotCounter == 0)
                        {
                            switch (w.WeaponType.Ammo)
                            {
                                case AmmoType.Beam:
                                    GenerateBeams(w);
                                    BeamOn = true;
                                    continue;
                                default:
                                    foreach (var m in w.Muzzles)
                                        if (Tick == m.LastShot)
                                        {
                                            Projectile pro;
                                            switch (p)
                                            {
                                                case 0:
                                                    lock (_projectiles.WaitA)
                                                    {
                                                        _projectiles.ProjectilePoolA.AllocateOrCreate(out pro);
                                                        pro.Start(new Shot(m.Position, m.DeviatedDir), w, _projectiles.CheckPoolA.Get());
                                                    }
                                                    break;
                                                case 1:
                                                    lock (_projectiles.WaitB)
                                                    {
                                                        _projectiles.ProjectilePoolB.AllocateOrCreate(out pro);
                                                        pro.Start(new Shot(m.Position, m.DeviatedDir), w, _projectiles.CheckPoolB.Get());
                                                    }
                                                    break;
                                                case 2:
                                                    lock (_projectiles.WaitC)
                                                    {
                                                        _projectiles.ProjectilePoolC.AllocateOrCreate(out pro);
                                                        pro.Start(new Shot(m.Position, m.DeviatedDir), w, _projectiles.CheckPoolC.Get());
                                                    }
                                                    break;
                                                case 3:
                                                    lock (_projectiles.WaitD)
                                                    {
                                                        _projectiles.ProjectilePoolD.AllocateOrCreate(out pro);
                                                        pro.Start(new Shot(m.Position, m.DeviatedDir), w, _projectiles.CheckPoolD.Get());
                                                    }
                                                    break;
                                                case 4:
                                                    lock (_projectiles.WaitE)
                                                    {
                                                        _projectiles.ProjectilePoolE.AllocateOrCreate(out pro);
                                                        pro.Start(new Shot(m.Position, m.DeviatedDir), w, _projectiles.CheckPoolE.Get());
                                                    }
                                                    break;
                                                case 5:
                                                    lock (_projectiles.WaitF)
                                                    {
                                                        _projectiles.ProjectilePoolF.AllocateOrCreate(out pro);
                                                        pro.Start(new Shot(m.Position, m.DeviatedDir), w, _projectiles.CheckPoolF.Get());
                                                    }
                                                    break;
                                            }
                                            if (p++ >= 5) p = 0;
                                        }
                                    break;
                            }
                        }
                    }
                }
            }
        }

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
            _projectiles.FiredBeams.Add(fireBeam);
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

                var radius = 0.15f;
                if (Tick % 6 == 0) radius = 0.14f;
                var weapon = pInfo.Weapon;
                var beamSlot = weapon.BeamSlot;
                var material = weapon.WeaponType.PhysicalMaterial;
                var trailColor = weapon.WeaponType.TrailColor;
                var particleColor = weapon.WeaponType.ParticleColor;
                if (pInfo.PrimeProjectile && Tick > beamSlot[pInfo.ProjectileId] && pInfo.HitPos != Vector3D.Zero)
                {
                    beamSlot[pInfo.ProjectileId] = Tick + 20;
                    BeamParticleStart(pInfo.Entity, pInfo.HitPos, particleColor);
                }

                if (distToBeam < 1000000)
                {
                    if (distToBeam > 250000) radius *= 1.5f;
                    TransparentRenderExt.DrawTransparentCylinder(ref matrix, radius, radius, (float)pInfo.Projectile.Length, 6, trailColor, trailColor, WarpMaterial, WarpMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                }
                else MySimpleObjectDraw.DrawLine(pInfo.Projectile.From, pInfo.Projectile.To, material, ref trailColor, 2f);
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
            _effect1.UserRadiusMultiplier = (float)radius;
            _effect1.UserEmitterScale = 1;
            _effect1.Velocity = vel;
            _effect1.Play();
        }

        private void BoltParticleStart(IMyEntity ent, Vector3D pos, Vector4 color, Vector3D speed)
        {
            var dist = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, pos);
            var logOfPlayerDist = Math.Log(dist);

            var mainParticle = 32;

            var size = 10;
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