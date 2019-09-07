using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Support.HitEntity.Type;
using Projectile = WeaponCore.Projectiles.Projectile;

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

        internal readonly Target Target = new Target();
        internal readonly List<HitEntity> HitList = new List<HitEntity>();
        internal WeaponSystem System;
        internal GridAi Ai;
        internal MyEntity PrimeEntity;
        internal MyEntity TriggerEntity;
        internal HitEntity HitEntity;
        internal WeaponFrameCache WeaponCache;
        internal MatrixD PrimeMatrix = MatrixD.Identity;
        internal MatrixD TriggerMatrix = MatrixD.Identity;
        internal Vector3D Position;
        internal Vector3D PrevPosition;
        internal Vector3D Direction;
        internal Vector4 Color;
        internal int WeaponId;
        internal int MuzzleId;
        internal int ReSizeSteps;
        internal int TriggerGrowthSteps;
        internal int ObjectsHit;
        internal double Length;
        internal double MaxSpeedLength;
        internal float LineWidth;
        internal float BaseDamagePool;
        internal float AreaEffectDamage;
        internal float DetonationDamage;
        internal float BaseHealthPool;
        internal bool Shrink;
        internal bool AfterGlow;
        internal bool OnScreen;
        internal bool IsShrapnel;
        internal bool EnableGuidance = true;
        internal bool Triggered;
        internal DrawState Draw;

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

            LineWidth = width;
            Color = color;
        }

        internal void InitVirtual(WeaponSystem system, GridAi ai, MyEntity primeEntity, MyEntity triggerEntity, Target target, int weaponId, int muzzleId, Vector3D position, Vector3D direction)
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
            Position = position;
            PrevPosition = Position;
            Direction = direction;
        }

        internal void UpdateVrShape(Vector3D prevPosition, Vector3D position, Vector3D direction, double length)
        {
            PrevPosition = prevPosition;
            Position = position;
            Direction = direction;
            Length = length;
        }

        internal void UpdateShape(Vector3D prevPosition, Vector3D position, Vector3D direction, double length)
        {
            PrevPosition = prevPosition;
            Position = position;
            Direction = direction;
            Length = length;
        }

        internal void Clean()
        {
            Target.Reset();
            HitList.Clear();
            System = null;
            Ai = null;
            PrimeEntity = null;
            TriggerEntity = null;
            HitEntity = null;
            WeaponCache = null;
            Triggered = false;
            TriggerGrowthSteps = 0;
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
        }

        public readonly List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
        public MyEntity Entity;
        internal Projectile Projectile;
        public LineD Beam;
        public bool Hit;
        public bool SphereCheck;
        public BoundingSphereD PruneSphere;
        public Vector3D? HitPos;
        public Type EventType;
        private object _listCache;
        private object _hashSetCache;

        public int PoolId = - 1;

        internal static HashSet<T> SwapSet<T>(object cacheObject, HashSet<T> lst)
        {
            return cacheObject as HashSet<T>;
        }

        internal static List<T> SwapList<T>(object cacheObject, HashSet<T> lst)
        {
            return cacheObject as List<T>;
        }

        internal static List<T> CastOrGetList<T>(HitEntity hitEnt, HashSet<T> lst)
        {
            if (hitEnt._listCache == null)
                hitEnt._listCache = Session.Instance.Projectiles.GenericListPool[hitEnt.PoolId].Get();
            var list = hitEnt._listCache as List<T>;
            return list;
        }

        internal static HashSet<T> CastOrGetHashSet<T>(HitEntity hitEnt, HashSet<T> lst)
        {
            if (hitEnt._hashSetCache == null)
                hitEnt._hashSetCache = Session.Instance.Projectiles.GenericHashSetPool[hitEnt.PoolId].Get();
            var hashSet = hitEnt._hashSetCache as HashSet<T>;

            return hashSet;
        }

        public HitEntity()
        {
        }

        public void Clean()
        {
            Entity = null;
            Projectile = null;
            if (PoolId >= 0)
            {
                /*
                if (_listCache != null)
                {
                    var set = SwapList(_listCache, ((MyCubeGrid)null)?.CubeBlocks);
                    if (set != null)
                    {
                        set.Clear();
                        Session.Instance.Projectiles.GenericListPool[PoolId].Return(_listCache);
                    }
                    _listCache = null;
                }
                */
                if (_hashSetCache != null)
                {
                    var set = SwapSet(_hashSetCache, ((MyCubeGrid)null)?.CubeBlocks);
                    if (set != null)
                    {
                        set.Clear();
                        Session.Instance.Projectiles.GenericHashSetPool[PoolId].Return(_hashSetCache);
                    }
                    _hashSetCache = null;
                }
            }
            PoolId = -1;
            Beam.Length = 0;
            Beam.Direction = Vector3D.Zero;
            Beam.From = Vector3D.Zero;
            Beam.To = Vector3D.Zero;
            Blocks.Clear();
            Hit = false;
            HitPos = null;
            EventType = Stale;
            PruneSphere = new BoundingSphereD();
            SphereCheck = false;
        }
    }

    internal class Target
    {
        internal volatile bool Expired;
        internal MyCubeBlock FiringCube;
        internal MyEntity Entity;
        internal Projectile Projectile;
        internal bool IsProjectile;
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

        internal void TransferTo(Target target)
        {
            target.Entity = Entity;
            target.Projectile = Projectile;
            target.IsProjectile = IsProjectile;
            target.HitPos = HitPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            target.Expired = Expired;
            Reset();
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

        internal void Reset()
        {
            Entity = null;
            Projectile = null;
            IsProjectile = false;
            HitPos = Vector3D.Zero;
            HitShortDist = 0;
            OrigDistance = 0;
            TopEntityId = 0;
            Expired = true;
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

        internal void SortProjectiles(Weapon w)
        {
            var ai = w.Comp.Ai;
            var weaponPos = w.Comp.MyPivotPos;
            if (Session.Instance.Tick != Tick)
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

                frag.Origin = p.Position;
                frag.OriginUp = p.OriginUp;
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

        internal void Spawn(int poolId)
        {
            for (int i = 0; i < Sharpnel.Count; i++)
            {
                var frag = Sharpnel[i];
                Projectile p;
                Session.Instance.Projectiles.ProjectilePool[poolId].AllocateOrCreate(out p);
                p.T.System = frag.System;
                p.T.Ai = frag.Ai;
                p.T.Target.Entity = frag.Target;
                p.T.IsShrapnel = true;

                p.T.WeaponId = frag.WeaponId;
                p.T.MuzzleId = frag.MuzzleId;

                p.Origin = frag.Origin;
                p.OriginUp = frag.OriginUp;
                p.PredictedTargetPos = frag.PredictedTargetPos;
                p.Direction = frag.Direction;
                p.State = Projectile.ProjectileState.Start;

                p.StartSpeed = frag.Velocity;
                Session.Instance.Projectiles.FragmentPool[poolId].Return(frag);
            }
            Session.Instance.Projectiles.ShrapnelPool[poolId].Return(this);
            Sharpnel.Clear();
        }
    }

    internal class Fragment
    {
        public WeaponSystem System;
        public GridAi Ai;
        public MyEntity Target;
        public Vector3D Origin;
        public Vector3D OriginUp;
        public Vector3D Direction;
        public Vector3D Velocity;
        public Vector3D PredictedTargetPos;
        public int WeaponId;
        public int MuzzleId;
    }

    public class Shrinking
    {
        internal WeaponSystem System;
        internal Vector3D Position;
        internal Vector3D PrevPosition;
        internal Vector3D Direction;
        internal int ReSizeSteps;
        internal double LineReSizeLen;

        internal void Init(Trajectile trajectile)
        {
            System = trajectile.System;
            Position = trajectile.Position;
            PrevPosition = trajectile.PrevPosition;
            Direction = trajectile.Direction;
            ReSizeSteps = trajectile.ReSizeSteps;
            LineReSizeLen = trajectile.MaxSpeedLength;
        }

        internal Shrunk? GetLine()
        {
            if (ReSizeSteps-- <= 0) return null;
            var length = ReSizeSteps * LineReSizeLen;
            return new Shrunk(PrevPosition + -(Direction * length), Position, Direction, length);
        }
    }

    internal struct Shrunk
    {
        internal readonly Vector3D PrevPosition;
        internal readonly Vector3D Position;
        internal readonly Vector3D Direction;
        internal readonly double Length;
        internal Shrunk(Vector3D prevPosition, Vector3D position, Vector3D direction, double length)
        {
            PrevPosition = prevPosition;
            Position = position;
            Direction = direction;
            Length = length;
        }
    }

    public class AfterGlow
    {
        internal WeaponSystem System;
        internal Vector3D Position;
        internal Vector3D PrevPosition;
        internal Vector3D Direction;
        internal int ReSizeSteps;
        internal double LineReSizeLen;

        internal void Init(Trajectile trajectile)
        {
            System = trajectile.System;
            Position = trajectile.Position;
            PrevPosition = trajectile.PrevPosition;
            Direction = trajectile.Direction;
            ReSizeSteps = trajectile.ReSizeSteps;
            LineReSizeLen = trajectile.MaxSpeedLength;
        }

        internal Glow? GetLine()
        {
            if (ReSizeSteps-- <= 0) return null;
            var length = ReSizeSteps * LineReSizeLen;
            return new Glow(PrevPosition + -(Direction * length), Position, Direction, length);
        }
    }

    internal struct Glow
    {
        internal readonly Vector3D PrevPosition;
        internal readonly Vector3D Position;
        internal readonly Vector3D Direction;
        internal readonly double Length;
        internal Glow(Vector3D prevPosition, Vector3D position, Vector3D direction, double length)
        {
            PrevPosition = prevPosition;
            Position = position;
            Direction = direction;
            Length = length;
        }
    }

    public struct InventoryChange
    {
        public enum ChangeType
        {
            Add,
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

}
