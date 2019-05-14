using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        private static int _checkIntersectionCnt = 0;
        internal ProjectileState State;
        internal Vector3D Direction;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Weapon Weapon;
        internal List<IMyEntity> CheckList;
        internal MyCubeGrid MyGrid;
        internal Vector3D Origin;
        internal Vector3D StartSpeed;
        internal Vector3D AddSpeed;
        internal Vector3D CurrentSpeed;
        internal Vector3D FinalSpeed;
        internal Vector3D CurrentMagnitude;
        internal double ShotLength;
        internal float SpeedLength;
        internal float MaxTrajectory;
        internal bool PositionChecked;
        internal double LineReSizeLen;
        internal int ReSizeSteps;
        internal int GrowStep = 1;
        internal bool Grow;
        internal bool Shrink;
        internal MyParticleEffect Effect1 = new MyParticleEffect();
        private Projectiles _caller;
        internal void Start(Projectiles.Shot fired, Weapon weapon, Projectiles caller, List<IMyEntity> checkList)
        {
            Weapon = weapon;
            var wDef = weapon.WeaponType;
            MaxTrajectory = wDef.MaxTrajectory;
            ShotLength = wDef.ShotLength;
            SpeedLength = wDef.DesiredSpeed;// * MyUtils.GetRandomFloat(1f, 1.5f);
            LineReSizeLen = SpeedLength / 60;
            ReSizeSteps = (int)(ShotLength / LineReSizeLen);
            Grow = ReSizeSteps > 1;
            Shrink = Grow;
            Origin = fired.Position;
            Direction = fired.Direction;
            Position = Origin;
            DsDebugDraw.DrawSphere(new BoundingSphereD(Origin, 0.25f), Color.Blue);
            MyGrid = weapon.Logic.MyGrid;
            CheckList = checkList;
            _caller = caller;
            State = ProjectileState.Alive;


            //_desiredSpeed = wDef.DesiredSpeed * ((double)ammoDefinition.SpeedVar > 0.0 ? MyUtils.GetRandomFloat(1f - ammoDefinition.SpeedVar, 1f + ammoDefinition.SpeedVar) : 1f);
            StartSpeed = Weapon.Logic.Turret.CubeGrid.Physics.LinearVelocity;
            AddSpeed = Direction * SpeedLength;
            FinalSpeed = StartSpeed + AddSpeed;
            CurrentSpeed = FinalSpeed;
            //_checkIntersectionIndex = _checkIntersectionCnt % 5;
            //_checkIntersectionCnt += 3;
            PositionChecked = false;
            //ProjectileParticleStart();
        }
        /*
        internal bool Update()
        {
            if (State != ProjectileState.Alive)
                return false;
            CurrentMagnitude = CurrentSpeed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * 1f;
            LastPosition = Position;
            Position += CurrentMagnitude;
            var distTraveled = (Origin - Position);
            Vector3D? intersect = null;
            if (Vector3D.Dot(distTraveled, distTraveled) >= MaxTrajectory * MaxTrajectory || Intersect(out intersect))
            {
                Close();
                State = ProjectileState.Dead;

                if (intersect != null) Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(Weapon, 0, new LineD(LastPosition, intersect.Value), CurrentMagnitude, Vector3D.Zero, null, true));
                return false;
            }

            var newLine = new LineD(LastPosition, Position);
            PositionChecked = true;
            //Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(Weapon, 0, newLine, _currentMagnitude, Vector3D.Zero, null, true));
            return true;
        }

        private bool Intersect(out Vector3D? hitVector)
        {
            var fired = new Projectiles.FiredBeam(Weapon, new List<LineD>());
            var beam = new LineD(LastPosition, Position + CurrentMagnitude);
            //DsDebugDraw.DrawLine(beam.From, beam.To, Color.Blue, 1);
            _caller.GetAllEntitiesInLine(CheckList, fired, beam);
            var hitInfo = _caller.GetHitEntities(CheckList, beam);
            if (_caller.GetDamageInfo(fired, beam, hitInfo, 0, false))
            {
                _caller.ProjectilePool.MarkForDeallocate(this);
                CheckList.Clear();
                hitVector = hitInfo.HitPos;
                return true;
            }

            CheckList.Clear();
            _caller.DamageEntities(fired);
            hitVector = hitInfo.HitPos;
            return false;
        }
        */
        //Relative velocity proportional navigation
        //aka: Whip-Nav
        Vector3D CalculateMissileIntercept(Vector3D targetPosition, Vector3D targetVelocity, Vector3D missilePos, Vector3D missileVelocity, double missileAcceleration, double compensationFactor = 1)
        {
            var missileToTarget = Vector3D.Normalize(targetPosition - missilePos);
            var relativeVelocity = targetVelocity - missileVelocity;
            var parallelVelocity = relativeVelocity.Dot(missileToTarget) * missileToTarget;
            var normalVelocity = (relativeVelocity - parallelVelocity);

            var normalMissileAcceleration = normalVelocity * compensationFactor;

            if (Vector3D.IsZero(normalMissileAcceleration))
                return missileToTarget;

            double diff = missileAcceleration * missileAcceleration - normalMissileAcceleration.LengthSquared();
            if (diff < 0)
            {
                return normalMissileAcceleration; //fly parallel to the target
            }

            return Math.Sqrt(diff) * missileToTarget + normalMissileAcceleration;
        }

        private void ProjectileParticleStart()
        {
            var color = new Vector4(255, 255, 255, 128f); // comment out to use beam color
            var mainParticle = 32;
            var to = Position;
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(mainParticle, out Effect1, ref matrix, ref to, uint.MaxValue, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (Effect1 == null) return;
            Effect1.DistanceMax = MaxTrajectory;
            Effect1.UserColorMultiplier = color;
            Effect1.UserRadiusMultiplier = 1f;
            Effect1.UserEmitterScale = 1f;
            Effect1.Velocity = CurrentSpeed;
        }

        public void Close()
        {
            State = ProjectileState.Dead;
            Effect1.Close(true, false);
            _caller.CheckPool.Return(CheckList);
            _caller.ProjectilePool.MarkForDeallocate(this);
        }

        public enum ProjectileState
        {
            Alive,
            Dead,
        }
    }
}
