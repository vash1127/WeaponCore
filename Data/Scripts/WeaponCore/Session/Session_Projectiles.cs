using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore
{
    public partial class Session
    {
        private void DrawLists(List<DrawProjectile> drawList)
        {
            var sFound = false;
            for (int i = 0; i < drawList.Count; i++)
            {
                var p = drawList[i];
                var wDef = p.Weapon.WeaponType;
                var line = p.Projectile;
                if (p.Shrink)
                {
                    sFound = true;
                    var shrink = _shrinkPool.Get();
                    shrink.Init(wDef, line, p.ReSizeSteps, p.LineReSizeLen);
                    _shrinking.Add(shrink);
                }

                MyTransparentGeometry.AddLocalLineBillboard(wDef.PhysicalMaterial, wDef.TrailColor, line.From, 0, line.Direction, (float)line.Length, wDef.ShotWidth);
            }
            drawList.Clear();
            if (sFound) _shrinking.ApplyAdditions();
        }

        private void Shrink()
        {
            var sRemove = false;
            foreach (var s in _shrinking)
            {
                var line = s.GetLine();
                if (line.HasValue)
                    MyTransparentGeometry.AddLocalLineBillboard(s.WepDef.PhysicalMaterial, s.WepDef.TrailColor, line.Value.From, 0, line.Value.Direction, (float)line.Value.Length, s.WepDef.ShotWidth);
                else
                {
                    _shrinking.Remove(s);
                    sRemove = true;
                }
            }
            if (sRemove) _shrinking.ApplyRemovals();
        }

        private void UpdateWeaponPlatforms()
        {
            for (int i = 0; i < Logic.Count; i++)
            {
                var logic = Logic[i];
                if (!logic.State.Value.Online) continue;

                for (int j = 0; j < logic.Platform.Weapons.Length; j++)
                {
                    if (j != 0) continue;
                    var w = logic.Platform.Weapons[j];
                    var gunner = false;
                    if (!logic.Turret.IsUnderControl)
                    {
                        if (w.Target != null)
                        {
                            DsDebugDraw.DrawLine(w.EntityPart.PositionComp.WorldAABB.Center, w.Target.PositionComp.WorldAABB.Center, Color.Black, 0.01f);
                        }
                        if (w.TrackTarget && w.SeekTarget) w.SelectTarget();
                        if (w.TurretMode && w.Target != null) w.Rotate(w.WeaponType.RotateSpeed);
                        if (w.TrackTarget && w.ReadyToTrack)
                        {
                            //logic.Turret.TrackTarget(w.Target);
                            //logic.Turret.EnableIdleRotation = false;
                        }
                    }
                    else
                    {
                        if (MyAPIGateway.Input.IsAnyMousePressed())
                        {
                            var currentAmmo = logic.Gun.GunBase.CurrentAmmo;
                            if (currentAmmo <= 1) logic.Gun.GunBase.CurrentAmmo += 1;
                            gunner = true;
                        }
                    }

                    if (w.ReadyToShoot || gunner) w.Shoot();
                    if (w.ShotCounter != 0) continue;

                    for (int k = 0; k < w.Muzzles.Length; k++)
                    {
                        var m = w.Muzzles[k];
                        if (Tick != m.LastShot) continue;

                        lock (_projectiles.Wait[_pCounter])
                        {
                            Projectile pro;
                            _projectiles.ProjectilePool[_pCounter].AllocateOrCreate(out pro);
                            pro.Start(new Shot(m.Position, m.DeviatedDir), w, _projectiles.CheckPool[_pCounter].Get());
                        }
                        if (_pCounter++ >= _projectiles.Wait.Length -1) _pCounter = 0;
                    }
                }
            }
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