using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    public class Weapon
    {
        public Weapon(IMyEntity entity)
        {
            EntityPart = entity;
            LocalTranslation = entity.LocalMatrix.Translation;
            PivotOffsetVec = (Vector3.Transform(entity.PositionComp.LocalAABB.Center, entity.PositionComp.LocalMatrix) - entity.GetTopMostParent(typeof(MyCubeBlock)).PositionComp.LocalAABB.Center);
            UpPivotOffsetLen = PivotOffsetVec.Length();
        }

        public IMyEntity EntityPart;
        public WeaponSystem WeaponSystem;
        public Dummy[] Dummies;
        public Muzzle[] Muzzles;

        public uint PosUpdatedTick = uint.MinValue;
        public uint PosChangedTick = 1;
        public MatrixD WeaponMatrix;
        public MatrixD OldWeaponMatrix;
        public Vector3D WeaponPosition;
        public Vector3D OldWeaponPosition;
        public Vector3 LocalTranslation;
        public Vector3 PivotOffsetVec;
        public float UpPivotOffsetLen;


        public void PositionChanged(MyPositionComponentBase pComp)
        {
            PosChangedTick = Session.Instance.Tick;
        }

        private int RotationTime { get; set; }

        public void MovePart(float radians, int time, bool xAxis, bool yAxis, bool zAxis)
        {
            MatrixD rotationMatrix;
            if (xAxis) rotationMatrix = MatrixD.CreateRotationX(radians * RotationTime);
            else if (yAxis) rotationMatrix = MatrixD.CreateRotationY(radians * RotationTime);
            else if (zAxis) rotationMatrix = MatrixD.CreateRotationZ(radians * RotationTime);
            else return;

            RotationTime += time;
            rotationMatrix.Translation = LocalTranslation;
            EntityPart.PositionComp.LocalMatrix = rotationMatrix;
        }
    }

    public class Muzzle
    {
        public Vector3D Position;
        public Vector3D Direction;
        public uint LastFireTick;
        public uint LastPosUpdate;
    }

    public class MyWeaponPlatform
    {
        public readonly Weapon[] Weapons;
        public readonly RecursiveSubparts SubParts = new RecursiveSubparts();
        public readonly WeaponStructure Structure;
        public uint[][] BeamSlot { get; set; }

        public MyWeaponPlatform(MyStringHash subTypeIdHash, IMyEntity entity)
        {
            Structure = Session.Instance.WeaponStructure[subTypeIdHash];
            //PartNames = Structure.PartNames;
            var subPartCount = Structure.PartNames.Length;

            Weapons = new Weapon[subPartCount];
            BeamSlot = new uint[subPartCount][];

            SubParts.Entity = entity;
            SubParts.CheckSubparts();
            for (int i = 0; i < subPartCount; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.PartNames[i]].Barrels.Length;
                IMyEntity subPartEntity;
                SubParts.NameToEntity.TryGetValue(Structure.PartNames[i].String, out subPartEntity);
                BeamSlot[i] = new uint[barrelCount];
                Weapons[i] = new Weapon(subPartEntity)
                {
                    Muzzles = new Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                    WeaponSystem = Structure.WeaponSystems[Structure.PartNames[i]]
                };
            }

            CompileTurret();
        }

        private void CompileTurret()
        {
            var c = 0;
            foreach (var m in Structure.WeaponSystems)
            {
                var subPart = SubParts.NameToEntity[m.Key.String];
                var barrelCount = m.Value.Barrels.Length;
                Weapons[c].EntityPart.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                for (int i = 0; i < barrelCount; i++)
                {
                    var barrel = m.Value.Barrels[i];
                    Weapons[c].Dummies[i] = new Dummy(subPart, barrel);
                    Weapons[c].Muzzles[i] = new Muzzle();
                }
                c++;
            }
        }
    }

}
