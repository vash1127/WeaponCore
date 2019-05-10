using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    public partial class Session
    {
        private void GenerateBeams(Logic logic)
        {
            var barrels = logic.Platform.BeamSlot[0];
            var fireBeam = new Projectiles.FiredBeam(logic, _linePool.Get());
            foreach (var barrelInfo in barrels)
            {
                /*
                var back = barrelInfo.SubPart.PositionComp.WorldMatrix.Backward;
                var forward = barrelInfo.SubPart.PositionComp.WorldMatrix.Forward;
                var barrel = new LineD(back, forward);
                fireBeam.Beams.Add(new LineD(barrel.To, barrel.To + barrel.Direction * 1000));
                */
            }
            lock (_projectiles) _projectiles.FiredBeams.Add(fireBeam);
        }

        private void GenerateBolts(Logic logic, int slot)
        {
            var barrels = logic.Platform.BeamSlot[slot];
            var firedMissile = new Projectiles.FiredProjectile(logic, _linePool.Get());

            foreach (var barrelInfo in barrels)
            {
                /*
                var back = barrelInfo.SubPart.PositionComp.WorldMatrix.Backward;
                var forward = barrelInfo.SubPart.PositionComp.WorldMatrix.Forward;
                var barrel = new LineD(back, forward);
                firedMissile.Projectiles.Add(barrel);
                */
            }
            lock (_projectiles) _projectiles.Add(firedMissile);
        }

        private void GenerateMissiles(Logic logic)
        {
            var barrels = logic.Platform.BeamSlot[0];
            var firedMissile = new Projectiles.FiredProjectile(logic, _linePool.Get());

            foreach (var barrelInfo in barrels)
            {
                /*
                var back = barrelInfo.SubPart.PositionComp.WorldMatrix.Backward;
                var forward = barrelInfo.SubPart.PositionComp.WorldMatrix.Forward;
                var barrel = new LineD(back, forward);
                firedMissile.Projectiles.Add(barrel);
                */
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
                var weapons = pInfo.Logic.Platform;
                var structure = weapons.Structure;
                var beamSlot = weapons.BeamSlot[0];
                var rgb = structure.WeaponSystems[structure.PartNames[0]].WeaponType.TrailColor;
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

                var weapons = pInfo.Logic.Platform;
                var structure = weapons.Structure;
                var beamSlot = weapons.BeamSlot[0];
                var rgb = structure.WeaponSystems[structure.PartNames[0]].WeaponType.TrailColor;
                var radius = 0.15f;
                if (Tick % 6 == 0) radius = 0.14f;
                var mainBeam = new Vector4(rgb[0], rgb[1], rgb[2], 1f);
                if (pInfo.PrimeProjectile && Tick > beamSlot[pInfo.ProjectileId] && pInfo.HitPos != Vector3D.Zero)
                {
                    beamSlot[pInfo.ProjectileId] = Tick + 20;
                    BoltParticleStart(pInfo.Entity, pInfo.HitPos, mainBeam, Vector3D.Zero);
                }
                if (distToBeam < 1000000)
                {
                    if (distToBeam > 250000) radius *= 1.5f;
                    TransparentRenderExt.DrawTransparentCylinder(ref matrix, radius, radius, (float)pInfo.Projectile.Length, 6, mainBeam, mainBeam, WarpMaterial, WarpMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                }
                else MySimpleObjectDraw.DrawLine(pInfo.Projectile.From, pInfo.Projectile.To, ProjectileMaterial, ref mainBeam, 2f);
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
                var structure = pInfo.Logic.Platform.Structure;
                var rgb = structure.WeaponSystems[structure.PartNames[0]].WeaponType.TrailColor;
                Vector4 mainBeam = new Vector4(255, 255, 255, 255);
                MySimpleObjectDraw.DrawLine(pInfo.Projectile.From, pInfo.Projectile.To, ProjectileMaterial, ref mainBeam, 0.2f);
            }
        }

        private MyParticleEffect _effect1 = new MyParticleEffect();
        private void BeamParticleStart(IMyEntity ent, Vector3D pos, Vector4 color)
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
