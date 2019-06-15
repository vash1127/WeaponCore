using VRage;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Support.GraphicDefinition;

namespace WeaponCore.Support
{
    public class Shrinking
    {
        internal WeaponDefinition WepDef;
        internal Vector3D Position;
        internal Vector3D Direction;
        internal double Length;
        internal int ReSizeSteps;
        internal double LineReSizeLen;
        internal int ShrinkStep;

        internal void Init(WeaponDefinition wepDef, LineD line, int reSizeSteps, double lineReSizeLen)
        {
            WepDef = wepDef;
            Position = line.To;
            Direction = line.Direction;
            Length = line.Length;
            ReSizeSteps = reSizeSteps;
            LineReSizeLen = lineReSizeLen;
            ShrinkStep = reSizeSteps;
        }

        internal LineD? GetLine()
        {
            if (ShrinkStep-- <= 0) return null;
            return new LineD(Position + -(Direction * (ShrinkStep * LineReSizeLen)), Position);
        }
    }

    public struct InventoryChange
    {
        public enum ChangeType
        {
            Add,
            Check,
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
        public readonly EffectType Effect;

        public WeaponHit(WeaponComponent logic, Vector3D hitPos, float size, EffectType effect)
        {
            Logic = logic;
            HitPos = hitPos;
            Size = size;
            Effect = effect;
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
