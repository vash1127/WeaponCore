using Sandbox.Definitions;
using VRage.ModAPI;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public class MyWeaponPlatform
    {
        public readonly Weapon[] Weapons;
        public readonly RecursiveSubparts SubParts = new RecursiveSubparts();
        public readonly WeaponStructure Structure;
        public readonly MyLargeTurretBaseDefinition BaseDefinition;
        public MyWeaponPlatform(WeaponComponent comp)
        {
            BaseDefinition = comp.MyCube.BlockDefinition as MyLargeTurretBaseDefinition;
            Structure = Session.Instance.WeaponPlatforms[Session.Instance.SubTypeIdHashMap[comp.Turret.BlockDefinition.SubtypeId]];
            var subPartCount = Structure.PartNames.Length;
            Weapons = new Weapon[subPartCount];
            SubParts.Entity = comp.Entity;
            SubParts.CheckSubparts();
            for (int i = 0; i < subPartCount; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.PartNames[i]].Barrels.Length;
                IMyEntity subPartEntity;
                SubParts.NameToEntity.TryGetValue(Structure.PartNames[i].String, out subPartEntity);
                Weapons[i] = new Weapon(subPartEntity, Structure.WeaponSystems[Structure.PartNames[i]], i)
                {
                    Muzzles = new Weapon.Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                    Comp = comp,
                };

                var weapon = Weapons[i];
                if (weapon.WeaponType.TurretDef.TurretMode && comp.TrackingWeapon == null && subPartEntity?.Parent?.Parent?.Parent == comp.MyCube)
                {
                    weapon.TrackingAi = true;
                    comp.TrackingWeapon = weapon;
                }
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
                    Weapons[c].Muzzles[i] = new Weapon.Muzzle();
                }
                c++;
            }
        }
    }
}
