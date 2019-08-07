using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
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
        public WeaponSystem System;
        public MyCubeBlock MyCube;

        public readonly List<MyCubeBlock> Top5 = new List<MyCubeBlock>();
        public int[] Deck = new int[0];
        public int PrevDeckLength;
        public SubSystemDefinition.BlockTypes LastBlockType;

        public MyEntity Entity;
        public Vector3D HitPos;
        public double HitShortDist;
        public double OrigDistance;
        public long TopEntityId;

        public Target(WeaponSystem system = null, MyCubeBlock myCube = null)
        {
            System = system;
            MyCube = myCube;
        }

        public void TransferTo(Target target)
        {
            target.Entity = Entity;
            target.HitPos = HitPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            Reset();
        }

        public void Set(MyEntity ent, Vector3D pos, double shortDist, double origDist, long topEntId)
        {
            Entity = ent;
            HitPos = pos;
            HitShortDist = shortDist;
            OrigDistance = origDist;
            TopEntityId = topEntId;
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
        public bool VirtualHit;
        public int Hits;
        public HitEntity HitEntity = new HitEntity();
        public IMySlimBlock HitBlock;
    }

    public struct BatteryInfo
    {
        public readonly MyResourceSourceComponent Source;
        public readonly MyResourceSinkComponent Sink;
        public readonly MyCubeBlock CubeBlock;
        public BatteryInfo(MyResourceSourceComponent source)
        {
            Source = source;
            Sink = Source.Entity.Components.Get<MyResourceSinkComponent>();
            CubeBlock = source.Entity as MyCubeBlock;
        }
    }

    public class BlockSets
    {
        public readonly HashSet<MyResourceSourceComponent> Sources = new HashSet<MyResourceSourceComponent>();
        public readonly HashSet<MyShipController> ShipControllers = new HashSet<MyShipController>();
        public readonly HashSet<BatteryInfo> Batteries = new HashSet<BatteryInfo>();
    }
}
