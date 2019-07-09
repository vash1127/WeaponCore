using VRage;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore.Support
{
    public class Shrinking
    {
        internal DrawProjectile DrawProjectile;
        internal Vector3D Position;
        internal Vector3D Direction;
        internal double Length;
        internal int ShrinkStep;

        internal void Init(LineD line, ref DrawProjectile projectile)
        {
            DrawProjectile = projectile;
            Position = line.To;
            Direction = line.Direction;
            Length = line.Length;
            ShrinkStep = DrawProjectile.ReSizeSteps;
        }

        internal LineD? GetLine()
        {
            if (ShrinkStep-- <= 0) return null;
            return new LineD(Position + -(Direction * (ShrinkStep * DrawProjectile.LineReSizeLen)), Position);
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

    public struct TargetInfo
    {
        public enum TargetType
        {
            Player,
            Grid,
            Other
        }

        public readonly MyEntity Entity;
        public readonly double Distance;
        public readonly float Size;
        public readonly TargetType Type;
        public TargetInfo(MyEntity entity, double distance, float size, TargetType type)
        {
            Entity = entity;
            Distance = distance;
            Size = size;
            Type = type;
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
}
