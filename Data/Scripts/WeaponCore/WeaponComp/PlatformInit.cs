using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI;
using System;
using VRage;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.Start;
namespace WeaponCore.Platform
{
    public class MyWeaponPlatform
    {
        internal readonly Weapon[] Weapons;
        internal readonly RecursiveSubparts Parts = new RecursiveSubparts();
        internal readonly WeaponStructure Structure;
        internal readonly bool Inited;
        internal MyWeaponPlatform(WeaponComponent comp)
        {
            Structure = comp.Ai.Session.WeaponPlatforms[comp.Ai.Session.SubTypeIdHashMap[comp.MyCube.BlockDefinition.Id.SubtypeId.String]];

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

            var partCount = Structure.MuzzlePartNames.Length;
            Weapons = new Weapon[partCount];
            Parts.Entity = comp.Entity as MyEntity;
            Parts.CheckSubparts();
            for (int i = 0; i < partCount; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.MuzzlePartNames[i]].Barrels.Length;

                var wepAnimationSet =
                    comp.Ai.Session.CreateWeaponAnimationSet(Structure.WeaponSystems[Structure.MuzzlePartNames[i]].WeaponAnimationSet, Parts);

                MyEntity barrelPartEntity;
                if (!Parts.NameToEntity.TryGetValue(Structure.MuzzlePartNames[i].String, out barrelPartEntity))
                {
                    Log.Line($"Invalid barrelPart!!!!!!!!!!!!!!!!!");
                    return;
                }
                foreach (var part in Parts.NameToEntity)
                {
                    part.Value.OnClose += comp.SubpartClosed;
                    break;
                }

                var system = Structure.WeaponSystems[Structure.MuzzlePartNames[i]];

                //compatability with old configs of converted turrets
                var azimuthPartName = string.IsNullOrEmpty(system.AzimuthPartName.String) ? "MissileTurretBase1" : system.AzimuthPartName.String;
                var elevationPartName = string.IsNullOrEmpty(system.ElevationPartName.String) ? "MissileTurretBarrels" : system.ElevationPartName.String;

                Weapons[i] = new Weapon(barrelPartEntity, system, i, comp, wepAnimationSet)
                {
                    Muzzles = new Weapon.Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                    AzimuthPart = new MyTuple<MyEntity, Matrix, Matrix, Matrix, Matrix> { Item1 = Parts.NameToEntity[azimuthPartName], Item2 = Matrix.Zero, Item3 = Matrix.Zero, Item4 = Matrix.Zero, Item5 = Matrix.Zero },
                    ElevationPart = new MyTuple<MyEntity, Matrix, Matrix , Matrix, Matrix> { Item1 = Parts.NameToEntity[elevationPartName], Item2 = Matrix.Zero, Item3 = Matrix.Zero, Item4 = Matrix.Zero, Item5 = Matrix.Zero }

                };

                var weapon = Weapons[i];
                if (weapon.System.Values.HardPoint.Block.TurretController)
                {
                    weapon.TrackingAi = true;
                    comp.Debug = weapon.System.Values.HardPoint.Block.Debug || comp.Debug;
                    weapon.AimOffset = weapon.System.Values.HardPoint.Block.Offset;
                    weapon.FixedOffset = weapon.System.Values.HardPoint.Block.FixedOffset;

                    if(weapon.System.Values.HardPoint.Block.PrimaryTracking && comp.TrackingWeapon == null)
                        comp.TrackingWeapon = weapon;

                    if (weapon.AvCapable && weapon.System.HardPointRotationSound)
                    {
                        comp.RotationEmitter = new MyEntity3DSoundEmitter(comp.MyCube, true, 1f);
                        comp.RotationSound = new MySoundPair();
                        comp.RotationSound.Init(weapon.System.Values.Audio.HardPoint.HardPointRotationSound, false);
                    }
                }
                weapon.UpdatePivotPos();
            }
            CompileTurret(comp);
        }

        private void CompileTurret(WeaponComponent comp, bool reset = false)
        {
            var c = 0;
            foreach (var m in Structure.WeaponSystems)
            {
                MyEntity muzzlePart;
                if (Parts.NameToEntity.TryGetValue(m.Key.String, out muzzlePart))
                {
                    var azimuthPartName = string.IsNullOrEmpty(m.Value.AzimuthPartName.String) ? "MissileTurretBase1" : m.Value.AzimuthPartName.String;
                    var elevationPartName = string.IsNullOrEmpty(m.Value.ElevationPartName.String) ? "MissileTurretBarrels" : m.Value.ElevationPartName.String;

                    if (reset)
                    {
                        MyEntity azimuthPartEntity;
                        if (Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPartEntity))
                        {
                            MyEntity elevationPartEntity;
                            if (Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPartEntity))
                            {
                                //Log.Line("Reset parts");
                                Weapons[c].AzimuthPart.Item1 = azimuthPartEntity;
                                Weapons[c].ElevationPart.Item1 = elevationPartEntity;
                            }
                            else return;
                        }
                        else return;
                    }
                    Weapons[c].BarrelPart = muzzlePart;

                    if (comp.IsAiOnlyTurret)
                    {
                        var azimuthPart = Weapons[c].AzimuthPart.Item1;
                        var elevationPart = Weapons[c].ElevationPart.Item1;

                        var azimuthPartLocation = comp.Ai.Session.GetPartLocation("subpart_" + azimuthPartName, azimuthPart.Parent.Model).Value;
                        var elevationPartLocation = comp.Ai.Session.GetPartLocation("subpart_" + elevationPartName, elevationPart.Parent.Model).Value;

                        var azPartPosTo = Matrix.CreateTranslation(-azimuthPartLocation);
                        var azPrtPosFrom = Matrix.CreateTranslation(azimuthPartLocation);
                        var elPartPosTo = Matrix.CreateTranslation(-elevationPartLocation);
                        var elPartPosFrom = Matrix.CreateTranslation(elevationPartLocation);

                        var fullStepAzRotation = azPartPosTo * Matrix.CreateRotationY(-m.Value.AzStep) * azPrtPosFrom;

                        var fullStepElRotation = elPartPosTo * Matrix.CreateRotationX(-m.Value.ElStep) * elPartPosFrom;

                        var rFullStepAzRotation = Matrix.Invert(fullStepAzRotation);
                        var rFullStepElRotation = Matrix.Invert(fullStepElRotation);

                        Weapons[c].AzimuthPart.Item2 = azPartPosTo;
                        Weapons[c].AzimuthPart.Item3 = azPrtPosFrom;
                        Weapons[c].AzimuthPart.Item4 = fullStepAzRotation;
                        Weapons[c].AzimuthPart.Item5 = rFullStepAzRotation;

                        Weapons[c].ElevationPart.Item2 = elPartPosTo;
                        Weapons[c].ElevationPart.Item3 = elPartPosFrom;
                        Weapons[c].ElevationPart.Item4 = fullStepElRotation;
                        Weapons[c].ElevationPart.Item5 = rFullStepElRotation;
                    }

                    try
                    {
                        Weapons[c].BarrelPart.SetEmissiveParts("Heating", Color.Transparent, 0);
                    }
                    catch (Exception e)
                    {
                        // no emissive parts for barrel
                    }

                    var barrelCount = m.Value.Barrels.Length;
                    if (reset)
                    {
                        var registered = false;
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
                                        if (!registered)
                                        {
                                            animation.Part.OnClose += comp.SubpartClosed;
                                            registered = true;
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

                    Weapons[c].BarrelPart.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                    Weapons[c].BarrelPart.OnMarkForClose += Weapons[c].EntPartClose;
                    Weapons[c].Comp.MyCube.PositionComp.OnPositionChanged += Weapons[c].UpdatePartPos;

                    for (int i = 0; i < barrelCount; i++)
                    {
                        var barrel = m.Value.Barrels[i];
                        Weapons[c].Dummies[i] = new Dummy(Weapons[c].BarrelPart, barrel);
                        Weapons[c].MuzzleIdToName.Add(i, barrel);
                        Weapons[c].Muzzles[i] = new Weapon.Muzzle(i);
                    }

                    c++;
                }
            }
        }

        internal bool ResetParts(WeaponComponent comp)
        {
            //Log.Line("Resetting parts!!!!!!!!!!");
            RemoveParts(comp);
            Parts.CheckSubparts();
            foreach (var w in Weapons)
            {
                w.MuzzleIdToName.Clear();
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
                if (w.BarrelPart == null) continue;

                w.BarrelPart.PositionComp.OnPositionChanged -= w.PositionChanged;
                w.Comp.MyCube.PositionComp.OnPositionChanged -= w.UpdatePartPos;

                w.BarrelPart = null;
            }
            Parts.Reset(comp.Entity as MyEntity);
            comp.Status = Stopped;
        }
    }
}
