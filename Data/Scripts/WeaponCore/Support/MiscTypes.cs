using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Support.HitEntity.Type;
using Projectile = WeaponCore.Projectiles.Projectile;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace WeaponCore.Support
{
    internal class Trajectile
    {
        public enum DrawState
        {
            Last,
            Hit,
            Default
        }

        public enum ReSize
        {
            Grow,
            Shrink,
            None
        }

        internal readonly Target Target = new Target();
        internal readonly List<HitEntity> HitList = new List<HitEntity>();
        internal readonly MySoundPair FireSound = new MySoundPair();
        internal readonly MySoundPair TravelSound = new MySoundPair();
        internal readonly MySoundPair HitSound = new MySoundPair();
        internal readonly MyEntity3DSoundEmitter FireEmitter = new MyEntity3DSoundEmitter(null, true, 1f);
        internal readonly MyEntity3DSoundEmitter TravelEmitter = new MyEntity3DSoundEmitter(null, true, 1f);
        internal readonly MyEntity3DSoundEmitter HitEmitter = new MyEntity3DSoundEmitter(null, true, 1f);
        internal WeaponSystem.FiringSoundState FiringSoundState;
        internal bool AmmoSound;
        internal bool HasTravelSound;
        internal bool HitSoundActive;
        internal float AmmoTravelSoundRangeSqr;

        internal WeaponSystem System;
        internal GridAi Ai;
        internal MyEntity PrimeEntity;
        internal MyEntity TriggerEntity;
        internal HitEntity HitEntity;
        internal WeaponFrameCache WeaponCache;
        internal MatrixD PrimeMatrix = MatrixD.Identity;
        internal MatrixD TriggerMatrix = MatrixD.Identity;
        internal Vector3D Position;
        internal Vector3D Origin;
        internal Vector3D OriginUp;
        internal Vector3D Direction;
        internal Vector3D LineStart;
        internal Vector3D ClosestPointOnLine;
        internal Vector4 Color;
        internal int WeaponId;
        internal int MuzzleId;
        internal int TriggerGrowthSteps;
        internal int ObjectsHit;
        internal double Length;
        internal double GrowDistance;
        internal double DistanceTraveled;
        internal double PrevDistanceTraveled;
        internal double ProjectileDisplacement;
        internal float DistanceToLine;
        internal float ScaleFov;
        internal float LineWidth;
        internal float BaseDamagePool;
        internal float AreaEffectDamage;
        internal float DetonationDamage;
        internal float BaseHealthPool;
        internal bool OnScreen;
        internal bool IsShrapnel;
        internal bool EnableGuidance = true;
        internal bool Triggered;
        internal bool Cloaked;
        internal bool End;
        internal bool FakeExplosion;
        internal bool HitSoundActived;
        internal bool StartSoundActived;
        internal bool LastHitShield;
        internal ReSize ReSizing;
        internal DrawState Draw;

        internal void SetupSounds(double distanceFromCameraSqr)
        {
            FiringSoundState = System.FiringSound;
            AmmoTravelSoundRangeSqr = System.AmmoTravelSoundDistSqr;

            if (!System.IsBeamWeapon && System.AmmoTravelSound)
            {
                HasTravelSound = true;
                TravelSound.Init(System.Values.Audio.Ammo.TravelSound, false);
            }
            else HasTravelSound = false;

            if (System.HitSound)
            {
                var hitSoundChance = System.Values.Audio.Ammo.HitPlayChance;
                HitSoundActive = (hitSoundChance >= 1 || hitSoundChance >= MyUtils.GetRandomDouble(0.0f, 1f));
                if (HitSoundActive)
                    HitSound.Init(System.Values.Audio.Ammo.HitSound, false);
            }

            if (FiringSoundState == WeaponSystem.FiringSoundState.PerShot && distanceFromCameraSqr < System.FiringSoundDistSqr)
            {
                StartSoundActived = true;
                FireSound.Init(System.Values.Audio.HardPoint.FiringSound, false);
                FireEmitter.SetPosition(Origin);
                FireEmitter.Entity = Target.FiringCube;
            }
        }

        internal void AmmoSoundStart()
        {
            TravelEmitter.SetPosition(Position);
            TravelEmitter.Entity = PrimeEntity;
            TravelEmitter.PlaySound(TravelSound, true);
            AmmoSound = true;
        }

        internal void Complete(HitEntity hitEntity, DrawState draw)
        {
            HitEntity = hitEntity;
            Draw = draw;

            var color = System.Values.Graphics.Line.Tracer.Color;
            if (System.LineColorVariance)
            {
                var cv = System.Values.Graphics.Line.ColorVariance;
                var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                color.X *= randomValue;
                color.Y *= randomValue;
                color.Z *= randomValue;
            }

            var width = System.Values.Graphics.Line.Tracer.Width;

            if (System.LineWidthVariance)
            {
                var wv = System.Values.Graphics.Line.WidthVariance;
                var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                width += randomValue;
            }

            var target = Position + (-Direction * Length);
            var cameraPos = MyAPIGateway.Session.Camera.Position;
            ClosestPointOnLine = MyUtils.GetClosestPointOnLine(ref Position, ref target, ref cameraPos);
            DistanceToLine = (float)Vector3D.Distance(ClosestPointOnLine, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            ScaleFov = (float)Math.Tan(MyAPIGateway.Session.Camera.FovWithZoom * 0.5);

            LineWidth = Math.Max(width, 0.11f * ScaleFov * (DistanceToLine / 100));
            Color = color;
        }

        internal void InitVirtual(WeaponSystem system, GridAi ai, MyEntity primeEntity, MyEntity triggerEntity, Target target, int weaponId, int muzzleId, Vector3D origin, Vector3D direction)
        {
            System = system;
            Ai = ai;
            PrimeEntity = primeEntity;
            TriggerEntity = triggerEntity;
            Target.Entity = target.Entity;
            Target.Projectile = target.Projectile;
            Target.FiringCube = target.FiringCube;
            WeaponId = weaponId;
            MuzzleId = muzzleId;
            Direction = direction;
            Origin = origin;
            Position = origin;
        }

        internal void UpdateVrShape(Vector3D position, Vector3D direction, double length, ReSize resizing)
        {
            Position = position;
            Direction = direction;
            Length = length;
            ReSizing = resizing;
            LineStart = position + -(direction * length);
        }

        internal void UpdateShape(Vector3D position, Vector3D direction, double length, ReSize resizing)
        {
            Position = position;
            Direction = direction;
            Length = length;
            ReSizing = resizing;
            LineStart = position + -(direction * length);
        }

        internal void Clean()
        {
            Target.Reset(false);
            HitList.Clear();
            System = null;
            Ai = null;
            PrimeEntity = null;
            TriggerEntity = null;
            HitEntity = null;
            WeaponCache = null;
            Triggered = false;
            End = false;
            AmmoSound = false;
            HitSoundActive = false;
            HitSoundActived = false;
            StartSoundActived = false;
            HasTravelSound = false;
            LastHitShield = false;
            FakeExplosion = false;
            TriggerGrowthSteps = 0;
            ProjectileDisplacement = 0;
            GrowDistance = 0;
            ReSizing = ReSize.None;
        }
    }

    internal class HitEntity
    {
        internal enum Type
        {
            Shield,
            Grid,
            Voxel,
            Proximity,
            Destroyable,
            Stale,
            Projectile,
            Field,
            Effect,
        }

        public readonly List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
        public MyEntity Entity;
        internal Projectile Projectile;
        public Trajectile T;
        public LineD Beam;
        public bool Hit;
        public bool SphereCheck;
        public bool DamageOverTime;
        public BoundingSphereD PruneSphere;
        public Vector3D? HitPos;
        public double? HitDist;
        public Type EventType;

        public HitEntity()
        {
        }

        public void Clean()
        {
            Entity = null;
            Projectile = null;
            /*
            if (PoolId >= 0)
            {
                if (_hashSetCache != null)
                {
                    var set = SwapSet(_hashSetCache, ((MyCubeGrid)null)?.CubeBlocks);
                    if (set != null)
                    {
                        set.Clear();
                        T.Ai.Session.Projectiles.GenericHashSetPool[PoolId].Return(_hashSetCache);
                    }
                    _hashSetCache = null;
                }
            }
            */
            Beam.Length = 0;
            Beam.Direction = Vector3D.Zero;
            Beam.From = Vector3D.Zero;
            Beam.To = Vector3D.Zero;
            Blocks.Clear();
            Hit = false;
            HitPos = null;
            HitDist = null;
            T = null;
            EventType = Stale;
            PruneSphere = new BoundingSphereD();
            SphereCheck = false;
            DamageOverTime = false;
        }
    }

    internal class Target
    {
        internal volatile bool Expired = true;
        internal MyCubeBlock FiringCube;
        internal MyEntity Entity;
        internal Projectile Projectile;
        internal volatile bool IsProjectile;
        internal int[] Deck = new int[0];
        internal int PrevDeckLength;
        internal TargetingDefinition.BlockTypes LastBlockType;
        internal Vector3D HitPos;
        internal double HitShortDist;
        internal double OrigDistance;
        internal long TopEntityId;
        internal readonly List<MyCubeBlock> Top5 = new List<MyCubeBlock>();

        internal Target(MyCubeBlock firingCube = null)
        {
            FiringCube = firingCube;
        }

        internal void TransferTo(Target target, bool reset = true)
        {
            target.Entity = Entity;
            target.Projectile = Projectile;
            target.IsProjectile = target.Projectile != null;
            target.HitPos = HitPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            target.Expired = Expired;
            if(reset)Reset(false);
        }

        internal void Set(MyEntity ent, Vector3D pos, double shortDist, double origDist, long topEntId, Projectile projectile = null)
        {
            Entity = ent;
            Projectile = projectile;
            IsProjectile = projectile != null;
            HitPos = pos;
            HitShortDist = shortDist;
            OrigDistance = origDist;
            TopEntityId = topEntId;
            Expired = false;
        }

        internal void Reset(bool targetExpired = true)
        {
            Entity = null;
            IsProjectile = false;
            Projectile = null;
            HitPos = Vector3D.Zero;
            HitShortDist = 0;
            OrigDistance = 0;
            TopEntityId = 0;
            Expired = true;
        }
    }

    internal class VoxelParallelHits
    {
        internal uint RequestTick;
        internal uint ResultTick;
        internal IHitInfo HitInfo;
        private bool _requesting;

        internal bool Query(LineD lineTest)
        {
            var thisTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);
            _requesting = thisTick - ResultTick > 1;
            RequestTick = thisTick;
            MyAPIGateway.Physics.CastRayParallel(ref lineTest.From, ref lineTest.To, CollisionLayers.VoxelCollisionLayer, Results);
            return _requesting;
        }

        internal void Results(IHitInfo info)
        {
            ResultTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);
            if (info == null)
            {
                Log.Line("is null");
                HitInfo = null;
                return;
            }

            var voxel = info.HitEntity as MyVoxelBase;
            if (voxel?.RootVoxel is MyPlanet)
            {
                HitInfo = info;
                return;
            }
            HitInfo = null;
        }

        internal bool NewResult(out IHitInfo cachedPlanetResult)
        {
            cachedPlanetResult = null;
            var thisTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);
            //Log.Line($"NewREsults - thisTick:{thisTick} - RequestTick:{RequestTick} - ResultTick:{ResultTick}");

            if (HitInfo == null) return false;
            if (thisTick > RequestTick + 1) return false;
            Log.Line($"NewResult Planet");
            cachedPlanetResult = HitInfo;
            return true;
        }
    }

    internal class WeaponFrameCache
    {
        internal bool VirtualHit;
        internal int Hits;
        internal double HitDistance;
        internal uint Tick;
        internal HitEntity HitEntity = new HitEntity();
        internal IMySlimBlock HitBlock;

        internal readonly List<Projectile> SortProjetiles = new List<Projectile>();
        internal readonly VoxelParallelHits[] VoxelHits;

        internal WeaponFrameCache(int size)
        {
            VoxelHits = new VoxelParallelHits[size];
            for (int i = 0; i < size; i++) VoxelHits[i] = new VoxelParallelHits();
        }

        internal void SortProjectiles(Weapon w)
        {
            var ai = w.Comp.Ai;
            var weaponPos = w.MyPivotPos;
            if (w.Comp.Ai.Session.Tick != Tick)
            {
                SortProjetiles.Clear();
                foreach (var lp in ai.LiveProjectile) if (lp.MaxSpeed < w.System.MaxTargetSpeed) SortProjetiles.Add(lp);
                SortProjetiles.Sort((a, b) => Vector3D.DistanceSquared(a.Position, weaponPos).CompareTo(Vector3D.DistanceSquared(b.Position, weaponPos)));
            }
        }
    }

    internal class Fragments
    {
        internal List<Fragment> Sharpnel = new List<Fragment>();

        internal void Init(Projectile p, MyConcurrentPool<Fragment> fragPool)
        {
            for (int i = 0; i < p.T.System.Values.Ammo.Shrapnel.Fragments; i++)
            {
                var frag = fragPool.Get();

                frag.System = p.T.System;
                frag.Ai = p.T.Ai;
                frag.Target = p.T.Target.Entity;
                frag.WeaponId = p.T.WeaponId;
                frag.MuzzleId = p.T.MuzzleId;
                frag.FiringCube = p.T.Target.FiringCube;
                frag.Guidance = p.T.EnableGuidance;
                frag.Origin = p.Position;
                frag.OriginUp = p.T.OriginUp;
                frag.PredictedTargetPos = p.PredictedTargetPos;
                frag.Velocity = p.Velocity;
                var dirMatrix = Matrix.CreateFromDir(p.Direction);
                var shape = p.T.System.Values.Ammo.Shrapnel.Shape;
                float neg;
                float pos;
                switch (shape)
                {
                    case Shrapnel.ShrapnelShape.Cone:
                        neg = 0;
                        pos = 15;
                        break;
                    case Shrapnel.ShrapnelShape.HalfMoon:
                        neg = 90;
                        pos = 90;
                        break;
                    default:
                        neg = 180;
                        pos = 180;
                        break;
                }
                var negValue = MathHelper.ToRadians(neg);
                var posValue = MathHelper.ToRadians(pos);
                var randomFloat1 = MyUtils.GetRandomFloat(-negValue, posValue);
                var randomFloat2 = MyUtils.GetRandomFloat(0.0f, MathHelper.TwoPi);

                var shrapnelDir = Vector3.TransformNormal(-new Vector3(
                    MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2),
                    MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2),
                    MyMath.FastCos(randomFloat1)), dirMatrix);

                frag.Direction = shrapnelDir;

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
                Projectile p;
                frag.Ai.Session.Projectiles.ProjectilePool.AllocateOrCreate(out p);
                p.T.System = frag.System;
                p.T.Ai = frag.Ai;
                p.T.Target.Entity = frag.Target;
                p.T.Target.FiringCube = frag.FiringCube;
                p.T.IsShrapnel = true;
                p.T.EnableGuidance = frag.Guidance;
                p.T.WeaponId = frag.WeaponId;
                p.T.MuzzleId = frag.MuzzleId;
                p.T.Origin = frag.Origin;
                p.T.OriginUp = frag.OriginUp;
                p.PredictedTargetPos = frag.PredictedTargetPos;
                p.Direction = frag.Direction;
                p.State = Projectile.ProjectileState.Start;

                p.StartSpeed = frag.Velocity;
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
        public MyEntity Target;
        public MyCubeBlock FiringCube;
        public Vector3D Origin;
        public Vector3D OriginUp;
        public Vector3D Direction;
        public Vector3D Velocity;
        public Vector3D PredictedTargetPos;
        public int WeaponId;
        public int MuzzleId;
        internal bool Guidance;
    }

    public class Shrinking
    {
        internal WeaponSystem System;
        internal Vector3D HitPos;
        internal Vector3D BackOfTracer;
        internal Vector3D Direction;
        internal double ResizeLen;
        internal double TracerSteps;
        internal float Thickness;
        internal int TailSteps;

        internal void Init(Trajectile trajectile, float thickness)
        {
            Thickness = thickness;
            System = trajectile.System;
            HitPos = trajectile.Position;
            Direction = trajectile.Direction;
            ResizeLen = trajectile.DistanceTraveled - trajectile.PrevDistanceTraveled;
            TracerSteps = trajectile.System.Values.Graphics.Line.Tracer.Length / ResizeLen;
            var frontOfTracer = (trajectile.LineStart + (Direction * ResizeLen));
            var tracerLength = trajectile.System.Values.Graphics.Line.Tracer.Length;
            BackOfTracer = frontOfTracer + (-Direction * (tracerLength + ResizeLen));
        }

        internal Shrunk? GetLine()
        {
            if (--TracerSteps > 0)
            {
                var stepLength = ResizeLen;
                var backOfTail = BackOfTracer + (Direction * (TailSteps++ * stepLength));
                var newTracerBack = HitPos + -(Direction * TracerSteps * stepLength);
                var reduced = TracerSteps * stepLength;
                if (TracerSteps < 0) stepLength = Vector3D.Distance(backOfTail, HitPos);

                return new Shrunk(ref newTracerBack, ref backOfTail, reduced, stepLength);
            }
            return null;
        }
    }

    internal struct Shrunk
    {
        internal readonly Vector3D PrevPosition;
        internal readonly Vector3D BackOfTail;
        internal readonly double Reduced;
        internal readonly double StepLength;

        internal Shrunk(ref Vector3D prevPosition, ref Vector3D backOfTail, double reduced, double stepLength)
        {
            PrevPosition = prevPosition;
            BackOfTail = backOfTail;
            Reduced = reduced;
            StepLength = stepLength;
        }
    }

    internal struct AfterGlow
    {
        internal WeaponSystem System;
        internal Vector3D Back;
        internal Vector3D Direction;
        internal double StepLength;
        internal uint FirstTick;
    }

    public struct InventoryChange
    {
        public enum ChangeType
        {
            Add,
            Changed
        }

        public readonly Weapon Weapon;
        public readonly MyPhysicalInventoryItem Item;
        public readonly MyFixedPoint Amount;
        public readonly ChangeType Type;
        public InventoryChange(Weapon weapon, MyPhysicalInventoryItem item, MyFixedPoint amount, ChangeType type)
        {
            Weapon = weapon;
            Item = item;
            Amount = amount;
            Type = type;
        }
    }

    public struct WeaponHit
    {
        public readonly WeaponComponent Logic;
        public readonly Vector3D HitPos;
        public readonly float Size;

        public WeaponHit(WeaponComponent logic, Vector3D hitPos, float size, string stateEffect)
        {
            Logic = logic;
            HitPos = hitPos;
            Size = size;
        }
    }

    public struct BlockInfo
    {
        public enum BlockType
        {
            Player,
            Grid,
            Other
        }

        public readonly MyEntity Entity;
        public readonly double Distance;
        public readonly float Size;
        public readonly BlockType Type;
        public BlockInfo(MyEntity entity, double distance, float size, BlockType type)
        {
            Entity = entity;
            Distance = distance;
            Size = size;
            Type = type;
        }
    }

    public struct RadiatedBlock
    {
        public Vector3I Center;
        public IMySlimBlock Slim;
        public Vector3I Position;
    }

    public class FatMap
    {
        public MyConcurrentList<MyCubeBlock> MyCubeBocks;
        public MyGridTargeting Targeting;
        public volatile bool Trash;
        public int MostBlocks;
    }

    public class Focus
    {
        internal Focus(int count)
        {
            Target = new MyEntity[count];
            SubSystem = new TargetingDefinition.BlockTypes[count];
            TargetState = new TargetStatus[count];
            PrevTargetId = new long[count];
            for (int i = 0; i < TargetState.Length; i++)
                TargetState[i] = new TargetStatus();
        }

        internal MyEntity[] Target;
        internal TargetingDefinition.BlockTypes[] SubSystem;
        internal TargetStatus[] TargetState;
        internal long[] PrevTargetId;
        internal int ActiveId;
        internal bool HasFocus;

        internal void NextActive()
        {
            var prevId = ActiveId;
            if (ActiveId + 1 > Target.Length - 1) ActiveId -= 1;
            else ActiveId += 1;
            if (Target[ActiveId] == null) Target[ActiveId] = Target[prevId];
        }

        internal bool IsFocused()
        {
            HasFocus = false;
            for (int i = 0; i < Target.Length; i++)
            {
                if (Target[i] != null)
                {
                    if (!Target[i].MarkedForClose) HasFocus = true;
                    else Target[i] = null;
                }
            }
            return HasFocus;
        }

        internal void ReleaseActive()
        {
            Target[ActiveId] = null;
        }
    }

    public class TargetStatus
    {
        public int ShieldHealth;
        public int ThreatLvl;
        public int Size;
        public int Speed;
        public int Distance;
        public int Engagement;
    }

    public class IconInfo
    {
        private readonly MyStringId _textureName;
        private readonly Vector2D _screenPosition;
        private readonly double _definedScale;
        private readonly int _slotId;
        private readonly bool _canShift;
        private readonly float[] _adjustedScale;
        private readonly Vector3D[] _positionOffset;
        private readonly Vector3D[] _altPositionOffset;
        private readonly int[] _prevSlotId;

        public IconInfo(MyStringId textureName, double definedScale, Vector2D screenPosition, int slotId, bool canShift)
        {
            _textureName = textureName;
            _definedScale = definedScale;
            _screenPosition = screenPosition;
            _slotId = slotId;
            _canShift = canShift;
            _prevSlotId = new int[2];
            for (int i = 0; i < _prevSlotId.Length; i++)
                _prevSlotId[i] = -1;

            _adjustedScale = new float[2];
            _positionOffset = new Vector3D[2];
            _altPositionOffset = new Vector3D[2];
        }

        public void GetTextureInfo(int index, int displayCount, bool altPosition, Session session, out MyStringId textureName, out float scale, out Vector3D offset)
        {
            if (displayCount != _prevSlotId[index]) InitOffset(index, displayCount);
            textureName = _textureName;
            scale = _adjustedScale[index];
            offset = !altPosition ? Vector3D.Transform(_positionOffset[index], session.CameraMatrix) : Vector3D.Transform(_altPositionOffset[index], session.CameraMatrix);
            _prevSlotId[index] = displayCount;
        }

        private void InitOffset(int index, int displayCount)
        {
            var fov = MyAPIGateway.Session.Camera.FovWithZoom;
            var screenScale = 0.075 * Math.Tan(fov * 0.5);
            const float slotSpacing = 0.06f;
            var needShift = _slotId != displayCount;
            var shiftSize = _canShift && needShift ? -(slotSpacing * (_slotId - displayCount)) : 0;
            var position = new Vector3D(_screenPosition.X + shiftSize - (index * 0.45), _screenPosition.Y, 0);
            var altPosition = new Vector3D(_screenPosition.X + shiftSize - (index * 0.45), _screenPosition.Y - 1.25, 0);

            double aspectratio = MyAPIGateway.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;

            position.X *= screenScale * aspectratio;
            position.Y *= screenScale;
            altPosition.X *= screenScale * aspectratio;
            altPosition.Y *= screenScale;
            _adjustedScale[index] = (float)(_definedScale * screenScale);
            _positionOffset[index] = new Vector3D(position.X, position.Y, -.1);
            _altPositionOffset[index] = new Vector3D(altPosition.X, altPosition.Y, -.1);
        }
    }
}
