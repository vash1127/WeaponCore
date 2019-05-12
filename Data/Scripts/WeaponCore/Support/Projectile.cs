using System;
using VRage.Game;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    class Projectile
    {
        private static int _checkIntersectionCnt = 0;
        private ProjectileState _state;
        private Vector3D _origin;
        private Vector3D _velocity_Projectile;
        private Vector3D _velocity_Combined;
        private Vector3D _directionNormalized;
        private float _speed;
        private float _maxTrajectory;
        private Vector3D _position;
        private bool _positionChecked;
        private int _checkIntersectionIndex;
        private double _lengthMultiplier;
        private Weapon _weapon;

        internal void Start(LineD fired, Weapon weapon)
        {
            //var initVel = Vector3D.One;
            //_projectileAmmoDefinition = ammoDefinition;
            _weapon = weapon;
            _maxTrajectory = 5000;
            _state = ProjectileState.Alive;
            _directionNormalized = -fired.Direction;
            _origin = fired.From + (3 * _directionNormalized);
            _position = _origin;
            _speed = 250f;// * MyUtils.GetRandomFloat(1f, 1.5f);
            _lengthMultiplier = 1f;//MyUtils.GetRandomFloat(1f, 1.7f);
            //_speed = ammoDefinition.DesiredSpeed * ((double)ammoDefinition.SpeedVar > 0.0 ? MyUtils.GetRandomFloat(1f - ammoDefinition.SpeedVar, 1f + ammoDefinition.SpeedVar) : 1f);
            _velocity_Projectile = _directionNormalized * _speed;
            //_velocity_Combined = initVel + _velocity_Projectile;
            _velocity_Combined = _velocity_Projectile;
            // _maxTrajectory = ammoDefinition.MaxTrajectory;
            _checkIntersectionIndex = _checkIntersectionCnt % 5;
            _checkIntersectionCnt += 3;
            _positionChecked = false;
            ProjectileParticleStart();
        }

        internal bool Update()
        {
            if (_state != ProjectileState.Alive)
                return false;
            var position = _position;
            var speed = _velocity_Combined * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * 1f;
            _position += speed;
            var lastPos = (_position - _origin);
            if (Vector3D.Dot(lastPos, lastPos) >= _maxTrajectory * _maxTrajectory)
            {
                Close();
                _state = ProjectileState.Dead;
                return false;
            }
            _checkIntersectionIndex = ++_checkIntersectionIndex % 5;
            if (_checkIntersectionIndex != 0 && _positionChecked)
                return true;
            //var to = position + (2 * _lengthMultiplier) * (speed);
            var to = position + (speed * _directionNormalized);
            var newLine = new LineD(position, to);
            _positionChecked = true;
            //_effect1.SetTranslation(_position);
            Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(_weapon, 0, newLine, _velocity_Projectile, Vector3D.Zero, null, true));
            return true;
        }

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

        private MyParticleEffect _effect1 = new MyParticleEffect();
        private void ProjectileParticleStart()
        {
            var color = new Vector4(255, 255, 255, 128f); // comment out to use beam color
            var speed = (_velocity_Combined * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * 1f);
            var pos = _position;
            var to = (pos + speed) + 1 * speed;
            var mainParticle = 32;
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(mainParticle, out _effect1, ref matrix, ref to, uint.MaxValue, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (_effect1 == null) return;
            _effect1.DistanceMax = 5000;
            _effect1.UserColorMultiplier = color;
            _effect1.UserRadiusMultiplier = 1f;
            _effect1.UserEmitterScale = 1f;
            _effect1.Velocity = _velocity_Combined;
        }

        internal void Close()
        {
            _effect1.Close(true, false);
        }

        private enum ProjectileState
        {
            Alive,
            Dead,
        }
    }
}
