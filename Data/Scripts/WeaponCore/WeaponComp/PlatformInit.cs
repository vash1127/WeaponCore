using Sandbox.Game.Entities;
using System;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.Start;
namespace WeaponCore.Platform
{
    public class MyWeaponPlatform
    {
        internal Weapon[] Weapons;
        internal RecursiveSubparts Parts;
        internal WeaponStructure Structure;
        internal PlatformState State;

        internal enum PlatformState
        {
            Refresh,
            Invalid,
            Delay,
            Valid,
            Inited,
            Ready,
        }

        internal PlatformState PreInit(WeaponComponent comp)
        {
            var structure = comp.Ai.Session.WeaponPlatforms[comp.Ai.Session.SubTypeIdHashMap[comp.MyCube.BlockDefinition.Id.SubtypeId.String]];

            var wCounter = comp.Ai.WeaponCounter[comp.MyCube.BlockDefinition.Id.SubtypeId];
            wCounter.Max = structure.GridWeaponCap;
            if (wCounter.Max > 0)
            {
                if (wCounter.Current + 1 <= wCounter.Max)
                {
                    wCounter.Current++;
                    State = PlatformState.Valid;
                }
                else
                {
                    State = PlatformState.Invalid;
                    WeaponComponent removed;
                    if (comp.Ai.WeaponBase.TryRemove(comp.MyCube, out removed))
                        comp.UpdateCompList(add: false);

                    Log.Line("init platform invalid");
                    return State;
                }
            }
            else State = PlatformState.Valid; 

            Structure = structure;
            Parts = new RecursiveSubparts();

            var partCount = Structure.MuzzlePartNames.Length;
            Weapons = new Weapon[partCount];
            Parts.Entity = comp.Entity as MyEntity;

            if (!comp.MyCube.IsFunctional)
            {
                State = PlatformState.Delay;
                return State;
            }

            return GetParts(comp);
        }

        private PlatformState GetParts(WeaponComponent comp)
        {
            Parts.CheckSubparts();
            for (int i = 0; i < Structure.MuzzlePartNames.Length; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.MuzzlePartNames[i]].Barrels.Length;

                var wepAnimationSet =
                    comp.Ai.Session.CreateWeaponAnimationSet(Structure.WeaponSystems[Structure.MuzzlePartNames[i]].WeaponAnimationSet, Parts);

                MyEntity muzzlePartEntity = null;
                WeaponSystem system;

                if (!Structure.WeaponSystems.TryGetValue(Structure.MuzzlePartNames[i], out system))
                {
                    State = PlatformState.Invalid;
                    return State;
                }

                var muzzlePartName = Structure.MuzzlePartNames[i].String != "Designator" ? Structure.MuzzlePartNames[i].String : system.ElevationPartName.String;


                if (!Parts.NameToEntity.TryGetValue(muzzlePartName, out muzzlePartEntity))
                {
                    Log.Line($"Invalid barrelPart!!!!!!!!!!!!!!!!!");
                    State = PlatformState.Invalid;
                    return State;
                }
                foreach (var part in Parts.NameToEntity)
                {
                    part.Value.OnClose += comp.SubpartClosed;
                    break;
                }

                //compatability with old configs of converted turrets
                var azimuthPartName = !comp.IsSorterTurret ? string.IsNullOrEmpty(system.AzimuthPartName.String) ? "MissileTurretBase1" : system.AzimuthPartName.String : system.AzimuthPartName.String;
                var elevationPartName = !comp.IsSorterTurret ? string.IsNullOrEmpty(system.ElevationPartName.String) ? "MissileTurretBarrels" : system.ElevationPartName.String : system.ElevationPartName.String;

                MyEntity azimuthPart = null;
                MyEntity elevationPart = null;
                Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPart);
                Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPart);

                Weapons[i] = new Weapon(muzzlePartEntity, system, i, comp, wepAnimationSet)
                {
                    Muzzles = new Weapon.Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                    AzimuthPart = new MyTuple<MyEntity, Matrix, Matrix, Matrix, Matrix, Vector3> { Item1 = azimuthPart },
                    ElevationPart = new MyTuple<MyEntity, Matrix, Matrix, Matrix, Matrix, Vector3> { Item1 = elevationPart },
                    AiOnlyWeapon = comp.IsSorterTurret || (!comp.IsSorterTurret && (azimuthPartName != "MissileTurretBase1" || elevationPartName != "MissileTurretBarrels"))
                };

                //UI elements
                comp.HasGuidanceToggle = comp.HasGuidanceToggle || (system.Values.HardPoint.Ui.ToggleGuidance && system.Values.Ammo.Trajectory.Guidance != AmmoTrajectory.GuidanceType.None);

                comp.HasDamageSlider = comp.HasDamageSlider || (!system.MustCharge && system.Values.HardPoint.Ui.DamageModifier && system.EnergyAmmo || system.IsHybrid);

                comp.HasRofSlider = comp.HasRofSlider || (system.Values.HardPoint.Ui.RateOfFire && !system.MustCharge);

                comp.CanOverload = comp.CanOverload || (system.Values.HardPoint.Ui.EnableOverload && system.IsBeamWeapon && !system.MustCharge);

                comp.HasTurret = comp.HasTurret || (system.Values.HardPoint.Block.TurretAttached);

                comp.HasChargeWeapon = comp.HasChargeWeapon || system.MustCharge;

                var weapon = Weapons[i];
                if (weapon.System.Values.HardPoint.Block.TurretController)
                {
                    weapon.TrackingAi = true;
                    comp.Debug = weapon.System.Values.HardPoint.Block.Debug || comp.Debug;
                    weapon.AimOffset = weapon.System.Values.HardPoint.Block.Offset;
                    weapon.FixedOffset = weapon.System.Values.HardPoint.Block.FixedOffset;

                    if (weapon.System.Values.HardPoint.Block.PrimaryTracking && comp.TrackingWeapon == null)
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
            State = PlatformState.Inited;

            return State;
        }

        private void CompileTurret(WeaponComponent comp, bool reset = false)
        {
            var c = 0;
            foreach (var m in Structure.WeaponSystems)
            {
                MyEntity muzzlePart = null;
                if (Parts.NameToEntity.TryGetValue(m.Key.String, out muzzlePart) || m.Value.DesignatorWeapon)
                {
                    var azimuthPartName = !comp.IsSorterTurret ? string.IsNullOrEmpty(m.Value.AzimuthPartName.String) ? "MissileTurretBase1" : m.Value.AzimuthPartName.String : m.Value.AzimuthPartName.String;
                    var elevationPartName = !comp.IsSorterTurret ? string.IsNullOrEmpty(m.Value.ElevationPartName.String) ? "MissileTurretBarrels" : m.Value.ElevationPartName.String : m.Value.ElevationPartName.String;

                    if (reset)
                    {
                        MyEntity azimuthPartEntity;
                        if (Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPartEntity))
                            Weapons[c].AzimuthPart.Item1 = azimuthPartEntity;

                        MyEntity elevationPartEntity;
                        if (Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPartEntity))
                            Weapons[c].ElevationPart.Item1 = elevationPartEntity;
                    }

                    var muzzlePartName  = m.Key.String;
                    if (m.Value.DesignatorWeapon)
                    {
                        muzzlePart = Weapons[c].ElevationPart.Item1;
                        muzzlePartName = elevationPartName;
                    }

                    Weapons[c].MuzzlePart.Item1 = muzzlePart;

                    if (muzzlePartName != "None")
                    {
                        var muzzlePartLocation = comp.Ai.Session.GetPartLocation("subpart_" + muzzlePartName, muzzlePart.Parent.Model).Value;

                        var muzzlePartPosTo = Matrix.CreateTranslation(-muzzlePartLocation);
                        var muzzlePartPosFrom = Matrix.CreateTranslation(muzzlePartLocation);

                        Weapons[c].MuzzlePart.Item2 = muzzlePartPosTo;
                        Weapons[c].MuzzlePart.Item3 = muzzlePartPosFrom;
                        Weapons[c].MuzzlePart.Item4 = muzzlePartLocation;

                        try
                        {
                            Weapons[c].MuzzlePart.Item1.SetEmissiveParts("Heating", Color.Transparent, 0);
                        }
                        catch (Exception e)
                        {
                            // no emissive parts for barrel
                        }
                    }

                    if (Weapons[c].AiOnlyWeapon)
                    {
                        var azimuthPart = Weapons[c].AzimuthPart.Item1;
                        var elevationPart = Weapons[c].ElevationPart.Item1;

                        if (azimuthPart != null && azimuthPartName != "None")
                        {
                            var azimuthPartLocation = comp.Ai.Session.GetPartLocation("subpart_" + azimuthPartName, azimuthPart.Parent.Model).Value;
                            var azPartPosTo = Matrix.CreateTranslation(-azimuthPartLocation);
                            var azPrtPosFrom = Matrix.CreateTranslation(azimuthPartLocation);
                            var fullStepAzRotation = azPartPosTo * MatrixD.CreateRotationY(-m.Value.AzStep) * azPrtPosFrom;
                            var rFullStepAzRotation = Matrix.Invert(fullStepAzRotation);

                            Weapons[c].AzimuthPart.Item2 = azPartPosTo;
                            Weapons[c].AzimuthPart.Item3 = azPrtPosFrom;
                            Weapons[c].AzimuthPart.Item4 = fullStepAzRotation;
                            Weapons[c].AzimuthPart.Item5 = rFullStepAzRotation;
                            Weapons[c].AzimuthPart.Item6 = azimuthPartLocation;
                        }
                        else if (azimuthPartName == "None")
                        {
                            Weapons[c].AzimuthPart.Item2 = Matrix.Zero;
                            Weapons[c].AzimuthPart.Item3 = Matrix.Zero;
                            Weapons[c].AzimuthPart.Item4 = Matrix.Zero;
                            Weapons[c].AzimuthPart.Item5 = Matrix.Zero;
                            Weapons[c].AzimuthPart.Item6 = Vector3.Zero;
                        }


                        if (elevationPart != null && elevationPartName != "None")
                        {
                            var elevationPartLocation = comp.Ai.Session.GetPartLocation("subpart_" + elevationPartName, elevationPart.Parent.Model).Value;

                            var elPartPosTo = Matrix.CreateTranslation(-elevationPartLocation);
                            var elPartPosFrom = Matrix.CreateTranslation(elevationPartLocation);

                            var fullStepElRotation = elPartPosTo * MatrixD.CreateRotationX(-m.Value.ElStep) * elPartPosFrom;

                            var rFullStepElRotation = Matrix.Invert(fullStepElRotation);

                            Weapons[c].ElevationPart.Item2 = elPartPosTo;
                            Weapons[c].ElevationPart.Item3 = elPartPosFrom;
                            Weapons[c].ElevationPart.Item4 = fullStepElRotation;
                            Weapons[c].ElevationPart.Item5 = rFullStepElRotation;
                            Weapons[c].ElevationPart.Item6 = elevationPartLocation;
                        }
                        else if (elevationPartName == "None")
                        {
                            Weapons[c].ElevationPart.Item2 = Matrix.Zero;
                            Weapons[c].ElevationPart.Item3 = Matrix.Zero;
                            Weapons[c].ElevationPart.Item4 = Matrix.Zero;
                            Weapons[c].ElevationPart.Item5 = Matrix.Zero;
                            Weapons[c].ElevationPart.Item6 = Vector3.Zero;
                        }
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
                                        animation.Part = (MyEntitySubpart)part;
                                        animation.Reset();

                                        //if (comp.Ai.Session.AnimationsToProcess.Contains(animation))
                                            //comp.Ai.Session.AnimationsToProcess.Remove(animation);

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

                    if (m.Key.String != "Designator")
                    {
                        Weapons[c].MuzzlePart.Item1.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                        Weapons[c].MuzzlePart.Item1.OnMarkForClose += Weapons[c].EntPartClose;
                    }
                    else
                    {
                        if(Weapons[c].ElevationPart.Item1 != null)
                        {
                            Weapons[c].ElevationPart.Item1.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                            Weapons[c].ElevationPart.Item1.OnMarkForClose += Weapons[c].EntPartClose;
                        }
                        else
                        {
                            Weapons[c].AzimuthPart.Item1.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                            Weapons[c].AzimuthPart.Item1.OnMarkForClose += Weapons[c].EntPartClose;
                        }

                    }

                    for (int i = 0; i < barrelCount; i++)
                    {
                        var barrel = m.Value.Barrels[i];
                        Weapons[c].Dummies[i] = new Dummy(Weapons[c].MuzzlePart.Item1, barrel);
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
                if (w.MuzzlePart.Item1 == null) continue;

                w.MuzzlePart.Item1.PositionComp.OnPositionChanged -= w.PositionChanged;
            }
            Parts.Reset(comp.Entity as MyEntity);
            comp.Status = Stopped;
        }
    }
}
