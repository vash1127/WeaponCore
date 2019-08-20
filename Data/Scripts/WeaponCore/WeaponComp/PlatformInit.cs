using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.Start;
namespace WeaponCore.Platform
{
    public class MyWeaponPlatform
    {
        internal readonly Weapon[] Weapons;
        internal readonly RecursiveSubparts Parts = new RecursiveSubparts();
        internal readonly WeaponStructure Structure;
        internal readonly MyLargeTurretBaseDefinition BaseDefinition;
        internal MyWeaponPlatform(WeaponComponent comp)
        {
            BaseDefinition = comp.MyCube.BlockDefinition as MyLargeTurretBaseDefinition;
            Structure = Session.Instance.WeaponPlatforms[Session.Instance.SubTypeIdHashMap[comp.Turret.BlockDefinition.SubtypeId]];
            var partCount = Structure.PartNames.Length;
            Weapons = new Weapon[partCount];
            Parts.Entity = comp.Entity as MyEntity;
            Parts.CheckSubparts();
            for (int i = 0; i < partCount; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.PartNames[i]].Barrels.Length;
                MyEntity partEntity;
                Parts.NameToEntity.TryGetValue(Structure.PartNames[i].String, out partEntity);
                Weapons[i] = new Weapon(partEntity, Structure.WeaponSystems[Structure.PartNames[i]], i, comp)
                {
                    Muzzles = new Weapon.Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                };

                var weapon = Weapons[i];
                if (weapon.System.Values.HardPoint.TurretController && comp.TrackingWeapon == null)
                {
                    weapon.TrackingAi = true;
                    comp.TrackingWeapon = weapon;
                    if (weapon.AvCapable && weapon.System.HardPointRotationSound)
                    {
                        comp.RotationEmitter = new MyEntity3DSoundEmitter(comp.MyCube, true, 1f);
                        comp.RotationSound = new MySoundPair();
                        comp.RotationSound.Init(weapon.System.Values.Audio.HardPoint.HardPointRotationSound, false);
                    }
                }
            }
            CompileTurret();
        }

        private void CompileTurret(bool reset = false)
        {
            var c = 0;
            foreach (var m in Structure.WeaponSystems)
            {
                Log.Line($"PartToNameCount:{Parts.NameToEntity.Count}");
                var part = Parts.NameToEntity[m.Key.String];
                var barrelCount = m.Value.Barrels.Length;
                if (reset) Weapons[c].EntityPart = part;

                if (Weapons[c].IsTurret)
                    Weapons[c].EntityPart.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                else
                    Weapons[c].Comp.MyCube.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;

                Weapons[c].Comp.MyCube.PositionComp.OnPositionChanged += Weapons[c].UpdatePartPOS;


                for (int i = 0; i < barrelCount; i++)
                {
                    var barrel = m.Value.Barrels[i];
                    Weapons[c].Dummies[i] = new Dummy(part, barrel);
                    Weapons[c].Muzzles[i] = new Weapon.Muzzle(i);
                }
                c++;
            }
        }

        internal bool ResetParts(WeaponComponent comp)
        {
            Log.Line("Resetting parts!!!!!!!!!!");
            RemoveParts(comp);
            Parts.CheckSubparts();
            foreach (var w in Weapons)
            {
                w.Muzzles = new Weapon.Muzzle[w.System.Barrels.Length];
                w.Dummies = new Dummy[w.System.Barrels.Length];
            }

            CompileTurret(true);
            comp.Status = Started;
            return true;
        }

        internal void RemoveParts(WeaponComponent comp)
        {
            Log.Line("Remove parts");
            foreach (var w in comp.Platform.Weapons)
            {
                if (w.EntityPart == null) continue;
                if(w.IsTurret)
                    w.EntityPart.PositionComp.OnPositionChanged -= w.PositionChanged;
                else
                    w.Comp.MyCube.PositionComp.OnPositionChanged -= w.PositionChanged;

                w.Comp.MyCube.PositionComp.OnPositionChanged -= w.UpdatePartPOS;

                w.EntityPart = null;
            }
            Parts.Reset(comp.Entity as MyEntity);
            comp.Status = Stopped;
        }
    }
}
