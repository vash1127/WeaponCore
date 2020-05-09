using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Projectiles;
using static WeaponCore.Support.HitEntity.Type;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using static WeaponCore.Support.WeaponDefinition;

namespace WeaponCore.Support
{
    internal class ProInfo
    {
        internal readonly Target Target = new Target(null);
        internal readonly List<HitEntity> HitList = new List<HitEntity>();
        internal AvShot AvShot;
        internal WeaponSystem System;
        internal GridAi Ai;
        internal MyEntity PrimeEntity;
        internal MyEntity TriggerEntity;
        internal GroupOverrides Overrides;
        internal WeaponFrameCache WeaponCache;
        internal AmmoDef AmmoDef;
        internal Vector3D ShooterVel;
        internal Vector3D Origin;
        internal Vector3D OriginUp;
        internal Vector3D Direction;
        internal Vector3D VisualDir;
        internal WeaponRandomGenerator WeaponRng;
        internal int TriggerGrowthSteps;
        internal int WeaponId;
        internal int MuzzleId;
        internal int ObjectsHit;
        internal int Age;
        internal int FireCounter;
        internal ulong Id;
        internal double DistanceTraveled;
        internal double PrevDistanceTraveled;
        internal double ProjectileDisplacement;
        internal double MaxTrajectory;
        internal float ShotFade;
        internal float BaseDamagePool;
        internal float BaseHealthPool;
        internal float BaseEwarPool;
        internal double TracerLength;
        internal bool IsShrapnel;
        internal bool EnableGuidance = true;
        internal bool LastHitShield;
        internal bool TriggeredPulse;
        internal bool EwarActive;
        internal bool ModelOnly;
        internal bool LockOnFireState;
        internal bool IsFiringPlayer;
        internal bool ClientSent;
        internal bool IsVirtual;
        internal MatrixD TriggerMatrix = MatrixD.Identity;

        internal void InitVirtual(WeaponSystem system, GridAi ai, AmmoDef ammodef, MyEntity primeEntity, MyEntity triggerEntity, Target target, int weaponId, int muzzleId, Vector3D origin, Vector3D virDirection, double maxTrajectory, float shotFade)
        {
            IsVirtual = true;
            System = system;
            Ai = ai;
            AmmoDef = ammodef;
            PrimeEntity = primeEntity;
            TriggerEntity = triggerEntity;
            Target.Entity = target.Entity;
            Target.Projectile = target.Projectile;
            Target.FiringCube = target.FiringCube;
            WeaponId = weaponId;
            MuzzleId = muzzleId;
            Direction = virDirection;
            Origin = origin;
            MaxTrajectory = maxTrajectory;
            ShotFade = shotFade;
        }

        internal void Clean()
        {
            Target.Reset(System.Session.Tick, Target.States.ProjectileClosed);
            HitList.Clear();

            if (PrimeEntity != null)
            {
                AmmoDef.Const.PrimeEntityPool.Return(PrimeEntity);
                PrimeEntity = null;
            }

            if (TriggerEntity != null)
            {
                Ai.Session.TriggerEntityPool.Return(TriggerEntity);
                TriggerEntity = null;
            }

            AvShot = null;
            System = null;
            Ai = null;
            AmmoDef = null;
            WeaponCache = null;
            LastHitShield = false;
            IsShrapnel = false;
            TriggeredPulse = false;
            EwarActive = false;
            ModelOnly = false;
            LockOnFireState = false;
            IsFiringPlayer = false;
            ClientSent = false;
            TriggerGrowthSteps = 0;
            WeaponId = 0;
            MuzzleId = 0;
            Age = 0;
            ProjectileDisplacement = 0;
            MaxTrajectory = 0;
            ShotFade = 0;
            TracerLength = 0;
            FireCounter = 0;
            EnableGuidance = true;
            Direction = Vector3D.Zero;
            VisualDir = Vector3D.Zero;
            Origin = Vector3D.Zero;
            ShooterVel = Vector3D.Zero;
            TriggerMatrix = MatrixD.Identity;
        }
    }

    internal class HitEntity
    {
        internal enum Type
        {
            Shield,
            Grid,
            Voxel,
            Destroyable,
            Stale,
            Projectile,
            Field,
            Effect,
        }

        public readonly List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
        public readonly List<Vector3I> Vector3ICache = new List<Vector3I>();
        public MyEntity Entity;
        internal Projectile Projectile;
        public ProInfo Info;
        public LineD Intersection;
        public bool Hit;
        public bool SphereCheck;
        public bool DamageOverTime;
        public bool PulseTrigger;
        public BoundingSphereD PruneSphere;
        public Vector3D? HitPos;
        public double? HitDist;
        public Type EventType;

        public void Clean()
        {
            Vector3ICache.Clear();
            Entity = null;
            Projectile = null;
            Intersection.Length = 0;
            Intersection.Direction = Vector3D.Zero;
            Intersection.From = Vector3D.Zero;
            Intersection.To = Vector3D.Zero;
            Blocks.Clear();
            Hit = false;
            HitPos = null;
            HitDist = null;
            Info = null;
            EventType = Stale;
            PruneSphere = new BoundingSphereD();
            SphereCheck = false;
            DamageOverTime = false;
            PulseTrigger = false;
        }
    }

    internal struct Hit
    {
        internal IMySlimBlock Block;
        internal MyEntity Entity;
        internal Vector3D HitPos;
        internal Vector3D VisualHitPos;
        internal Vector3D HitVelocity;
        internal uint HitTick;
    }

    internal class VoxelParallelHits
    {
        internal uint RequestTick;
        internal uint ResultTick;
        internal uint LastTick;
        internal IHitInfo HitInfo;
        private bool _start;
        private uint _startTick;
        private int _miss;
        private int _maxDelay;
        private bool _idle;
        private Vector3D _endPos = Vector3D.MinValue;

        internal bool Cached(LineD lineTest, ProInfo i)
        {
            double dist;
            Vector3D.DistanceSquared(ref _endPos, ref lineTest.To, out dist);

            _maxDelay = i.MuzzleId == -1 ? i.System.Barrels.Length : 1;

            var thisTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);
            _start = thisTick - LastTick > _maxDelay || dist > 5;

            LastTick = thisTick;

            if (_start) {
                _startTick = thisTick;
                _endPos = lineTest.To;
            }

            var runTime = thisTick - _startTick;

            var fastPath = runTime > (_maxDelay * 3) + 1;
            var useCache = runTime > (_maxDelay * 3) + 2;
            if (fastPath) {
                if (_miss > 1) {
                    if (_idle && _miss % 120 == 0) _idle = false;
                    else _idle = true;

                    if (_idle) return true;
                }
                RequestTick = thisTick;
                MyAPIGateway.Physics.CastRayParallel(ref lineTest.From, ref lineTest.To, CollisionLayers.VoxelCollisionLayer, Results);
            }
            return useCache;
        }

        internal void Results(IHitInfo info)
        {
            ResultTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);
            if (info == null)
            {
                _miss++;
                HitInfo = null;
                return;
            }

            var voxel = info.HitEntity as MyVoxelBase;
            if (voxel?.RootVoxel is MyPlanet)
            {
                HitInfo = info;
                _miss = 0;
                return;
            }
            _miss++;
            HitInfo = null;
        }

        internal bool NewResult(out IHitInfo cachedPlanetResult)
        {
            cachedPlanetResult = null;
            var thisTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);

            if (HitInfo == null)
            {
                _miss++;
                return false;
            }

            if (thisTick > RequestTick + _maxDelay)
                return false;

            //Log.Line($"newResult: {thisTick} - {RequestTick} - {_maxDelay} - {RequestTick + _maxDelay} - {thisTick - (RequestTick + _maxDelay)}");
            cachedPlanetResult = HitInfo;
            return true;
        }
    }

    internal class WeaponFrameCache
    {
        internal bool VirtualHit;
        internal int Hits;
        internal double HitDistance;
        internal HitEntity HitEntity = new HitEntity();
        internal IMySlimBlock HitBlock;
        internal int VirutalId = -1;
        internal VoxelParallelHits[] VoxelHits;

        internal WeaponFrameCache(int size)
        {
            VoxelHits = new VoxelParallelHits[size];
            for (int i = 0; i < size; i++) VoxelHits[i] = new VoxelParallelHits();
        }
    }

    internal class Fragments
    {
        internal List<Fragment> Sharpnel = new List<Fragment>();
        internal void Init(Projectile p, MyConcurrentPool<Fragment> fragPool)
        {
            for (int i = 0; i < p.Info.AmmoDef.Shrapnel.Fragments; i++)
            {
                var frag = fragPool.Get();

                frag.System = p.Info.System;
                frag.Ai = p.Info.Ai;
                frag.AmmoDef = p.Info.System.WeaponAmmoTypes[p.Info.AmmoDef.Const.ShrapnelId].AmmoDef;
                frag.Target = p.Info.Target.Entity;
                frag.Overrides = p.Info.Overrides;
                frag.WeaponId = p.Info.WeaponId;
                frag.MuzzleId = p.Info.MuzzleId;
                frag.FiringCube = p.Info.Target.FiringCube;
                frag.Guidance = p.Info.EnableGuidance;
                frag.Origin = !Vector3D.IsZero(p.Hit.HitPos) ? p.Hit.HitPos : p.Position;
                frag.OriginUp = p.Info.OriginUp;
                frag.WeaponRng = p.Info.WeaponRng;
                frag.IsFiringPlayer = p.Info.IsFiringPlayer;
                frag.ClientSent = p.Info.ClientSent;
                frag.PredictedTargetPos = p.PredictedTargetPos;
                frag.Velocity = p.Velocity;
                frag.DeadSphere = p.DeadSphere;
                frag.LockOnFireState = p.Info.LockOnFireState;
                var dirMatrix = Matrix.CreateFromDir(p.Info.Direction);
                var posValue = MathHelper.ToRadians(MathHelper.Clamp(p.Info.AmmoDef.Shrapnel.Degrees, 0, 360));
                posValue *= 0.5f;
                var randomFloat1 = (float)(frag.WeaponRng.TurretRandom.NextDouble() * posValue);
                var randomFloat2 = (float)(frag.WeaponRng.TurretRandom.NextDouble() * MathHelper.TwoPi);
                frag.WeaponRng.TurretCurrentCounter += 2;
                var mutli = p.Info.AmmoDef.Shrapnel.Reverse ? -1 : 1;

                var shrapnelDir = Vector3.TransformNormal(mutli  * -new Vector3(
                    MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2),
                    MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2),
                    MyMath.FastCos(randomFloat1)), dirMatrix);

                frag.Direction = shrapnelDir;
                frag.PrimeEntity = null;
                frag.TriggerEntity = null;
                if (frag.AmmoDef.Const.PrimeModel && frag.AmmoDef.Const.PrimeEntityPool.Count > 0)
                    frag.PrimeEntity = frag.AmmoDef.Const.PrimeEntityPool.Get();

                if (frag.AmmoDef.Const.TriggerModel && p.Info.Ai.Session.TriggerEntityPool.Count > 0)
                    frag.TriggerEntity = p.Info.Ai.Session.TriggerEntityPool.Get();

                if (frag.AmmoDef.Const.PrimeModel && frag.PrimeEntity == null || frag.AmmoDef.Const.TriggerModel && frag.TriggerEntity == null) 
                    p.Info.Ai.Session.FragmentsNeedingEntities.Add(frag);

                Sharpnel.Add(frag);
            }
        }

        internal void Spawn()
        {
            Session session = null;
            for (int i = 0; i < Sharpnel.Count; i++)
            {
                var frag = Sharpnel[i];
                session = frag.Ai.Session;
                var p = frag.Ai.Session.Projectiles.ProjectilePool.Count > 0 ? frag.Ai.Session.Projectiles.ProjectilePool.Pop() : new Projectile();
                p.Info.System = frag.System;
                p.Info.Ai = frag.Ai;
                p.Info.Id = frag.Ai.Session.Projectiles.CurrentProjectileId++;
                p.Info.AmmoDef = frag.AmmoDef;
                p.Info.PrimeEntity = frag.PrimeEntity;
                p.Info.TriggerEntity = frag.TriggerEntity;
                p.Info.Target.Entity = frag.Target;
                p.Info.Target.FiringCube = frag.FiringCube;
                p.Info.Overrides = frag.Overrides;
                p.Info.IsShrapnel = true;
                p.Info.EnableGuidance = frag.Guidance;
                p.Info.WeaponId = frag.WeaponId;
                p.Info.MuzzleId = frag.MuzzleId;
                p.Info.Origin = frag.Origin;
                p.Info.OriginUp = frag.OriginUp;
                p.Info.WeaponRng = frag.WeaponRng;
                p.Info.ClientSent = frag.ClientSent;
                p.Info.IsFiringPlayer = frag.IsFiringPlayer;
                p.Info.BaseDamagePool = frag.AmmoDef.BaseDamage;
                p.PredictedTargetPos = frag.PredictedTargetPos;
                p.Info.Direction = frag.Direction;
                p.DeadSphere = frag.DeadSphere;
                p.StartSpeed = frag.Velocity;
                p.Info.LockOnFireState = frag.LockOnFireState;
                p.Info.MaxTrajectory = frag.AmmoDef.Const.MaxTrajectory;
                p.Info.ShotFade = 0;
                p.Info.FireCounter = 0;
                p.State = Projectiles.Projectile.ProjectileState.Start;

                frag.Ai.Session.Projectiles.ActiveProjetiles.Add(p);

                if (p.Info.AmmoDef.Health > 0 && !p.Info.AmmoDef.Const.IsBeamWeapon)
                    frag.Ai.Session.Projectiles.AddTargets.Add(p);

                frag.Ai.Session.Projectiles.FragmentPool.Return(frag);
            }

            session?.Projectiles.ShrapnelPool.Return(this);
            Sharpnel.Clear();
        }
    }

    internal class Fragment
    {
        public WeaponSystem System;
        public GridAi Ai;
        public AmmoDef AmmoDef;
        public MyEntity PrimeEntity;
        public MyEntity TriggerEntity;
        public MyEntity Target;
        public MyCubeBlock FiringCube;
        public GroupOverrides Overrides;
        public Vector3D Origin;
        public Vector3D OriginUp;
        public Vector3D Direction;
        public Vector3D Velocity;
        public Vector3D PredictedTargetPos;
        public BoundingSphereD DeadSphere;
        public int WeaponId;
        public int MuzzleId;
        public WeaponRandomGenerator WeaponRng;
        public bool Guidance;
        public bool ClientSent;
        public bool IsFiringPlayer;
        public bool LockOnFireState;
    }
}
