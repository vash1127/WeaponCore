using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.ModAPI;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public class MyWeaponPlatform
    {
        public readonly Weapon[] Weapons;
        public readonly RecursiveSubparts Parts = new RecursiveSubparts();
        public readonly WeaponStructure Structure;
        public readonly MyLargeTurretBaseDefinition BaseDefinition;
        public MyWeaponPlatform(WeaponComponent comp)
        {
            BaseDefinition = comp.MyCube.BlockDefinition as MyLargeTurretBaseDefinition;
            Structure = Session.Instance.WeaponPlatforms[Session.Instance.SubTypeIdHashMap[comp.Turret.BlockDefinition.SubtypeId]];
            var partCount = Structure.PartNames.Length;
            Weapons = new Weapon[partCount];
            Parts.Entity = comp.Entity;
            Parts.CheckSubparts();
            for (int i = 0; i < partCount; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.PartNames[i]].Barrels.Length;
                IMyEntity partEntity;
                Parts.NameToEntity.TryGetValue(Structure.PartNames[i].String, out partEntity);
                Weapons[i] = new Weapon(partEntity, Structure.WeaponSystems[Structure.PartNames[i]], i, comp)
                {
                    Muzzles = new Weapon.Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                };

                var weapon = Weapons[i];
                if (weapon.Kind.HardPoint.TurretController && comp.TrackingWeapon == null)
                {
                    weapon.TrackingAi = true;
                    comp.TrackingWeapon = weapon;
                    if (weapon.AvCapable && weapon.System.HardPointRotationSound)
                    {
                        comp.RotationEmitter = new MyEntity3DSoundEmitter(comp.MyCube, true, 1f);
                        comp.RotationSound = new MySoundPair();
                        comp.RotationSound.Init(weapon.Kind.Audio.HardPoint.HardPointRotationSound, false);
                    }
                }
            }
            CompileTurret();
        }

        private void CompileTurret()
        {
            var c = 0;

            foreach (var m in Structure.WeaponSystems)
            {
                var part = Parts.NameToEntity[m.Key.String];
                var barrelCount = m.Value.Barrels.Length;
                Weapons[c].EntityPart.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                for (int i = 0; i < barrelCount; i++)
                {
                    var barrel = m.Value.Barrels[i];
                    Weapons[c].Dummies[i] = new Dummy(part, barrel);
                    Weapons[c].Muzzles[i] = new Weapon.Muzzle(i);
                }
                c++;
            }
        }
    }
}
