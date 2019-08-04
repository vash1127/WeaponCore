using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore.Support
{
    public class Shrinking
    {
        internal Trajectile Trajectile;
        internal WeaponSystem System;
        internal int ReSizeSteps;
        internal double LineReSizeLen;

        internal void Init(Trajectile trajectile, ref DrawProjectile drawProjectile)
        {
            Trajectile = trajectile;
            System = drawProjectile.Projectile.System;
            ReSizeSteps = drawProjectile.Projectile.ReSizeSteps;
            LineReSizeLen = drawProjectile.Projectile.MaxSpeedLength;
        }

        internal Trajectile? GetLine()
        {
            if (ReSizeSteps-- <= 0) return null;
            var length = ReSizeSteps * LineReSizeLen;
            return new Trajectile(Trajectile.PrevPosition + -(Trajectile.Direction * length), Trajectile.Position, Trajectile.Direction, length);
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

    public class Target
    {
        public MyEntity Entity;
        public Vector3D HitPos;
        public double HitShortDist;
        public double OrigDistance;
        public long TopEntityId;

        public void TransferTo(Target target)
        {
            target.Entity = Entity;
            target.HitPos = HitPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            Reset();
        }

        public void Reset()
        {
            Entity = null;
            HitPos = Vector3D.Zero;
            HitShortDist = 0;
            OrigDistance = 0;
            TopEntityId = 0;
        }
    }

    public class WeaponDamageFrame
    {
        public bool Hit;
        public int Hits;
        public Vector3D HitPos;
        public HitEntity HitEntity = new HitEntity();
    }
}
