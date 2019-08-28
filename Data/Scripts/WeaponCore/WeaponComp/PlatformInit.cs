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
        internal readonly bool Inited;
        internal MyWeaponPlatform(WeaponComponent comp)
        {
            Structure = Session.Instance.WeaponPlatforms[Session.Instance.SubTypeIdHashMap[comp.Turret.BlockDefinition.SubtypeId]];

            var wCounter = comp.Ai.WeaponCounter[comp.MyCube.BlockDefinition.Id.SubtypeId];
            wCounter.Max = Structure.GridWeaponCap;
            if (wCounter.Max > 0)
            {
                if (wCounter.Current + 1 <= wCounter.Max)
                {
                    wCounter.Current++;
                    Inited = true;
                }
                else return;
            }

            BaseDefinition = comp.MyCube.BlockDefinition as MyLargeTurretBaseDefinition;

            var partCount = Structure.AimPartNames.Length;
            Weapons = new Weapon[partCount];
            Parts.Entity = comp.Entity as MyEntity;
            Parts.CheckSubparts();
            for (int i = 0; i < partCount; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.AimPartNames[i]].Barrels.Length;
                MyEntity aimPartEntity;
                Parts.NameToEntity.TryGetValue(Structure.AimPartNames[i].String, out aimPartEntity);
                Weapons[i] = new Weapon(aimPartEntity, Structure.WeaponSystems[Structure.AimPartNames[i]], i, comp)
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
            CompileTurret(comp);
        }

        private void CompileTurret(WeaponComponent comp, bool reset = false)
        {
            var c = 0;
            foreach (var m in Structure.WeaponSystems)
            {
                var aimPart = Parts.NameToEntity[m.Key.String];
                var muzzlePartName = m.Value.MuzzlePartName.String;
                var noMuzzlePart = muzzlePartName == "None" || muzzlePartName == "none" || muzzlePartName == string.Empty;
                var muzzlePart = (noMuzzlePart ? null : Parts.NameToEntity[m.Value.MuzzlePartName.String]) ?? comp.MyCube;
                var barrelCount = m.Value.Barrels.Length;
                if (reset) Weapons[c].EntityPart = aimPart;

                Weapons[c].EntityPart.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                Weapons[c].Comp.MyCube.PositionComp.OnPositionChanged += Weapons[c].UpdatePartPos;

                for (int i = 0; i < barrelCount; i++)
                {
                    var barrel = m.Value.Barrels[i];
                    Weapons[c].Dummies[i] = new Dummy(muzzlePart, barrel);
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

            CompileTurret(comp, true);
            comp.Status = Started;
            return true;
        }

        internal void RemoveParts(WeaponComponent comp)
        {
            Log.Line("Remove parts");
            foreach (var w in comp.Platform.Weapons)
            {
                if (w.EntityPart == null) continue;

                w.EntityPart.PositionComp.OnPositionChanged -= w.PositionChanged;
                w.Comp.MyCube.PositionComp.OnPositionChanged -= w.UpdatePartPos;

                w.EntityPart = null;
            }
            Parts.Reset(comp.Entity as MyEntity);
            comp.Status = Stopped;
        }
    }
}
