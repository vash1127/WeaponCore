using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Support.HitEntity.Type;

namespace WeaponCore.Support
{

    internal class Trajectile
    {
        internal WeaponSystem System;
        internal MyCubeBlock FiringCube;
        internal MyEntity Entity;
        internal MatrixD EntityMatrix = MatrixD.Identity;
        internal int WeaponId;
        internal int MuzzleId;

        internal void InitVirtual(WeaponSystem system, MyCubeBlock firingCube, MyEntity entity, int weaponId, int muzzleId, Vector3D position, Vector3D direction)
        {
            System = system;
            FiringCube = firingCube;
            Entity = entity;
            WeaponId = weaponId;
            MuzzleId = muzzleId;
            Position = position;
            PrevPosition = Position;
            Direction = direction;
        }

        internal Vector3D Position;
        internal Vector3D PrevPosition;
        internal Vector3D Direction;
        internal double Length;

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

        internal HitEntity HitEntity;
        internal double MaxSpeedLength;
        internal float LineWidth;
        internal int ReSizeSteps;
        internal bool Shrink;
        internal bool OnScreen;
        internal bool Last;
        internal Vector4 Color;
        internal void Complete(HitEntity hitEntity, bool last)
        {
            HitEntity = hitEntity;
            Last = last;
            var color = System.Values.Graphics.Line.Color;
            if (System.LineColorVariance)
            {
                var cv = System.Values.Graphics.Line.ColorVariance;
                var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                color.X *= randomValue;
                color.Y *= randomValue;
                color.Z *= randomValue;
            }

            var width = System.Values.Graphics.Line.Width;
            if (System.LineWidthVariance)
            {
                var wv = System.Values.Graphics.Line.WidthVariance;
                var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                width += randomValue;
            }

            LineWidth = width;
            Color = color;
        }
    }

    public class HitEntity
    {
        public enum Type
        {
            Shield,
            Grid,
            Voxel,
            Proximity,
            Destroyable,
            Stale,
        }

        public readonly List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
        public MyEntity Entity;
        public LineD Beam;
        public bool Hit;
        public Vector3D? HitPos;
        public Type EventType;

        public HitEntity()
        {
        }

        public void Clean()
        {
            Entity = null;
            Beam.Length = 0;
            Beam.Direction = Vector3D.Zero;
            Beam.From = Vector3D.Zero;
            Beam.To = Vector3D.Zero;
            Blocks.Clear();
            Hit = false;
            HitPos = null;
            EventType = Stale;
        }
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
}
