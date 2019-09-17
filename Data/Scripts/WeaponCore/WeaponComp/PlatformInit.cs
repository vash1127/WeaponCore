using Sandbox.Definitions;
using Sandbox.Game.Entities;
using System;
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
            else Inited = true;

            BaseDefinition = comp.MyCube.BlockDefinition as MyLargeTurretBaseDefinition;

            var partCount = Structure.AimPartNames.Length;
            Weapons = new Weapon[partCount];
            Parts.Entity = comp.Entity as MyEntity;
            Parts.CheckSubparts();
            for (int i = 0; i < partCount; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.AimPartNames[i]].Barrels.Length;
                MyEntity aimPartEntity;

                var wepAnimationSet =
                    Session.Instance.CreateWeaponAnimationSet(Structure.WeaponSystems[Structure.AimPartNames[i]].WeaponAnimationSet, Parts);

                Parts.NameToEntity.TryGetValue(Structure.AimPartNames[i].String, out aimPartEntity);
                foreach (var part in Parts.NameToEntity)
                {
                    part.Value.OnClose += comp.SubpartClosed;
                    break;
                }

                Weapons[i] = new Weapon(aimPartEntity, Structure.WeaponSystems[Structure.AimPartNames[i]], i, comp, wepAnimationSet)
                {
                    Muzzles = new Weapon.Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                };

                var weapon = Weapons[i];
                if (weapon.System.Values.HardPoint.Block.TurretController && comp.TrackingWeapon == null)
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
                MyEntity aimPart;
                if (Parts.NameToEntity.TryGetValue(m.Key.String, out aimPart))
                {
                    var muzzlePartName = m.Value.MuzzlePartName.String;
                    
                    var noMuzzlePart = muzzlePartName == "None" || muzzlePartName == "none" || muzzlePartName == string.Empty;
                    Weapons[c].BarrelPart = (noMuzzlePart ? comp.MyCube : Parts.NameToEntity[m.Value.MuzzlePartName.String]);

                    var barrelCount = m.Value.Barrels.Length;
                    if (reset)
                    {
                        Weapons[c].EntityPart = aimPart;
                        var Registered = false;
                        try
                        {
                            foreach (var animationSet in Weapons[c].AnimationsSet)
                            {
                                foreach (var animation in animationSet.Value)
                                {
                                    MyEntity part;
                                    if (Parts.NameToEntity.TryGetValue(animation.SubpartId, out part))
                                    {
                                        animation.Part = (MyEntitySubpart) part;
                                        if (!Registered)
                                        {
                                            animation.Part.OnClose += comp.SubpartClosed;
                                            Registered = true;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Line($"Exception in Compile Turret: {ex.Message}  -  {ex.StackTrace}");
                        }
                    }

                    Weapons[c].EntityPart.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                    Weapons[c].EntityPart.OnMarkForClose += Weapons[c].EntPartClose;
                    Weapons[c].Comp.MyCube.PositionComp.OnPositionChanged += Weapons[c].UpdatePartPos;

                    for (int i = 0; i < barrelCount; i++)
                    {
                        var barrel = m.Value.Barrels[i];
                        Weapons[c].Dummies[i] = new Dummy(Weapons[c].BarrelPart, barrel);
                        Weapons[c].MuzzleIDToName.Add(i, barrel);
                        Weapons[c].Muzzles[i] = new Weapon.Muzzle(i);
                    }

                    c++;
                }
            }
        }

        internal bool ResetParts(WeaponComponent comp)
        {
            Log.Line("Resetting parts!!!!!!!!!!");
            RemoveParts(comp);
            Parts.CheckSubparts();
            foreach (var w in Weapons)
            {
                w.MuzzleIDToName.Clear();
                w.Muzzles = new Weapon.Muzzle[w.System.Barrels.Length];
                w.Dummies = new Dummy[w.System.Barrels.Length];
            }

            CompileTurret(comp, true);
            comp.Status = Started;
            return true;
        }

        internal void RemoveParts(WeaponComponent comp)
        {
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
