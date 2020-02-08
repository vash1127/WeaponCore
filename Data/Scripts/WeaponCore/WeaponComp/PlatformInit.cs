using Sandbox.Game.Entities;
using System;
using VRage;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Support.WeaponComponent.BlockType;
using static WeaponCore.Platform.Weapon;
using Sandbox.ModAPI;

namespace WeaponCore.Platform
{
    public class MyWeaponPlatform
    {
        internal readonly RecursiveSubparts Parts = new RecursiveSubparts();
        internal readonly MySoundPair RotationSound = new MySoundPair();
        internal Weapon[] Weapons = new Weapon[1];
        internal WeaponStructure Structure;
        internal WeaponComponent Comp;
        internal PlatformState State;

        internal enum PlatformState
        {
            Fresh,
            Invalid,
            Delay,
            Valid,
            Inited,
            Ready,
        }

        internal void Setup(WeaponComponent comp)
        {
            Structure = comp.Session.WeaponPlatforms[comp.SubtypeHash];
            Comp = comp;

            if (Weapons.Length != Structure.MuzzlePartNames.Length)
                Array.Resize(ref Weapons, Structure.MuzzlePartNames.Length);
        }

        internal void Clean()
        {
            for (int i = 0; i < Weapons.Length; i++) Weapons[i] = null;
            Parts.Clean(Comp.MyCube);
            Structure = null;
            State = PlatformState.Fresh;
            Comp = null;
        }

        internal PlatformState Init(WeaponComponent comp)
        {

            if (comp.MyCube.MarkedForClose || comp.MyCube.CubeGrid.MarkedForClose)
            {
                State = PlatformState.Invalid;
                Log.Line("closed, init platform invalid");
                return State;
            }

            if (!comp.MyCube.IsFunctional)
            {
                State = PlatformState.Delay;
                return State;
            }


            var wCounter = comp.Ai.WeaponCounter[comp.SubtypeHash];
            wCounter.Max = Structure.GridWeaponCap;
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
                    Log.Line("init platform invalid");
                    return State;
                }
            }
            else
            {
                if (comp.BaseType == Turret && comp.TurretBase.AIEnabled)
                {
                    Log.Line($"ai is enabled in SBC! WEAPON DISABELED for: {comp.MyCube.BlockDefinition.Id.SubtypeName}");
                    State = PlatformState.Invalid;
                    WeaponComponent removed;
                    if (comp.Ai.WeaponBase.TryRemove(comp.MyCube, out removed))
                        return State;
                }
                State = PlatformState.Valid;
            } 

            Parts.Entity = comp.Entity as MyEntity;

            return GetParts(comp);
        }

        private PlatformState GetParts(WeaponComponent comp)
        {
            Parts.CheckSubparts();
            for (int i = 0; i < Structure.MuzzlePartNames.Length; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.MuzzlePartNames[i]].Barrels.Length;                

                MyEntity muzzlePartEntity = null;
                WeaponSystem system;

                if (!Structure.WeaponSystems.TryGetValue(Structure.MuzzlePartNames[i], out system))
                {
                    State = PlatformState.Invalid;
                    return State;
                }

                var wepAnimationSet = comp.Session.CreateWeaponAnimationSet(system, Structure.WeaponSystems[Structure.MuzzlePartNames[i]].WeaponAnimationSet, Parts);

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
                var azimuthPartName = comp.BaseType == Turret ? string.IsNullOrEmpty(system.AzimuthPartName.String) ? "MissileTurretBase1" : system.AzimuthPartName.String : system.AzimuthPartName.String;
                var elevationPartName = comp.BaseType == Turret ? string.IsNullOrEmpty(system.ElevationPartName.String) ? "MissileTurretBarrels" : system.ElevationPartName.String : system.ElevationPartName.String;

                MyEntity azimuthPart = null;
                MyEntity elevationPart = null;
                Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPart);
                Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPart);

                Weapons[i] = new Weapon(muzzlePartEntity, system, i, comp, wepAnimationSet)
                {
                    Muzzles = new Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                    AzimuthPart = new MyTuple<MyEntity, Matrix, Matrix, Matrix, Matrix, Vector3> { Item1 = azimuthPart },
                    ElevationPart = new MyTuple<MyEntity, Matrix, Matrix, Matrix, Matrix, Vector3> { Item1 = elevationPart },
                    AiOnlyWeapon = comp.BaseType != Turret || (azimuthPartName != "MissileTurretBase1" && elevationPartName != "MissileTurretBarrels" && azimuthPartName != "InteriorTurretBase1" && elevationPartName != "InteriorTurretBase2" && azimuthPartName != "GatlingTurretBase1" && elevationPartName != "GatlingTurretBase2")
                };

                //UI elements
                comp.HasGuidanceToggle = comp.HasGuidanceToggle || (system.Values.HardPoint.Ui.ToggleGuidance && system.Values.Ammo.Trajectory.Guidance != AmmoTrajectory.GuidanceType.None);

                comp.HasDamageSlider = comp.HasDamageSlider || (!system.MustCharge && system.Values.HardPoint.Ui.DamageModifier && system.EnergyAmmo || system.IsHybrid);

                comp.HasRofSlider = comp.HasRofSlider || (system.Values.HardPoint.Ui.RateOfFire && !system.MustCharge);

                comp.CanOverload = comp.CanOverload || (system.Values.HardPoint.Ui.EnableOverload && system.IsBeamWeapon && !system.MustCharge);

                comp.HasTurret = comp.HasTurret || (system.Values.HardPoint.Block.TurretAttached);

                comp.HasChargeWeapon = comp.HasChargeWeapon || system.MustCharge;

                var weapon = Weapons[i];
                
                if (!comp.Debug && weapon.System.Values.HardPoint.Block.Debug)
                    comp.Debug = true;

                if (weapon.System.Values.HardPoint.Block.TurretController)
                {
                    weapon.TrackingAi = true;
                    weapon.AimOffset = weapon.System.Values.HardPoint.Block.Offset;
                    weapon.FixedOffset = weapon.System.Values.HardPoint.Block.FixedOffset;

                    if (weapon.System.Values.HardPoint.Block.PrimaryTracking && comp.TrackingWeapon == null)
                        comp.TrackingWeapon = weapon;

                    if (weapon.AvCapable && weapon.System.HardPointRotationSound)
                        RotationSound.Init(weapon.System.Values.Audio.HardPoint.HardPointRotationSound, false);
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
                    var azimuthPartName = comp.BaseType == Turret ? string.IsNullOrEmpty(m.Value.AzimuthPartName.String) ? "MissileTurretBase1" : m.Value.AzimuthPartName.String : m.Value.AzimuthPartName.String;
                    var elevationPartName = comp.BaseType == Turret ? string.IsNullOrEmpty(m.Value.ElevationPartName.String) ? "MissileTurretBarrels" : m.Value.ElevationPartName.String : m.Value.ElevationPartName.String;
                    var weapon = Weapons[c];
                    if (reset)
                    {
                        MyEntity azimuthPartEntity;
                        if (Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPartEntity))
                            weapon.AzimuthPart.Item1 = azimuthPartEntity;

                        MyEntity elevationPartEntity;
                        if (Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPartEntity))
                            weapon.ElevationPart.Item1 = elevationPartEntity;
                    }

                    var muzzlePartName  = m.Key.String;
                    if (m.Value.DesignatorWeapon)
                    {
                        muzzlePart = weapon.ElevationPart.Item1;
                        muzzlePartName = elevationPartName;
                    }

                    weapon.MuzzlePart.Item1 = muzzlePart;

                    if (muzzlePartName != "None")
                    {
                        var muzzlePartLocation = comp.Session.GetPartLocation("subpart_" + muzzlePartName, muzzlePart.Parent.Model);

                        var muzzlePartPosTo = Matrix.CreateTranslation(-muzzlePartLocation);
                        var muzzlePartPosFrom = Matrix.CreateTranslation(muzzlePartLocation);

                        weapon.MuzzlePart.Item2 = muzzlePartPosTo;
                        weapon.MuzzlePart.Item3 = muzzlePartPosFrom;
                        weapon.MuzzlePart.Item4 = muzzlePartLocation;

                        try
                        {
                            weapon.MuzzlePart.Item1.SetEmissiveParts("Heating", Color.Transparent, 0);
                        }
                        catch (Exception ex) { Log.Line($"Exception in no emissive parts for barrel: {ex}"); }

                    }

                    if (weapon.AiOnlyWeapon)
                    {
                        var azimuthPart = weapon.AzimuthPart.Item1;
                        var elevationPart = weapon.ElevationPart.Item1;

                        if (azimuthPart != null && azimuthPartName != "None")
                        {
                            var azimuthPartLocation = comp.Session.GetPartLocation("subpart_" + azimuthPartName, azimuthPart.Parent.Model);
                            var azPartPosTo = Matrix.CreateTranslation(-azimuthPartLocation);
                            var azPrtPosFrom = Matrix.CreateTranslation(azimuthPartLocation);
                            var fullStepAzRotation = azPartPosTo * MatrixD.CreateRotationY(-m.Value.AzStep) * azPrtPosFrom;
                            var rFullStepAzRotation = Matrix.Invert(fullStepAzRotation);

                            weapon.AzimuthPart.Item2 = azPartPosTo;
                            weapon.AzimuthPart.Item3 = azPrtPosFrom;
                            weapon.AzimuthPart.Item4 = fullStepAzRotation;
                            weapon.AzimuthPart.Item5 = rFullStepAzRotation;
                            weapon.AzimuthPart.Item6 = azimuthPartLocation;
                        }
                        else if (azimuthPartName == "None")
                        {
                            weapon.AzimuthPart.Item2 = Matrix.Zero;
                            weapon.AzimuthPart.Item3 = Matrix.Zero;
                            weapon.AzimuthPart.Item4 = Matrix.Zero;
                            weapon.AzimuthPart.Item5 = Matrix.Zero;
                            weapon.AzimuthPart.Item6 = Vector3.Zero;
                        }


                        if (elevationPart != null && elevationPartName != "None")
                        {
                            var elevationPartLocation = comp.Session.GetPartLocation("subpart_" + elevationPartName, elevationPart.Parent.Model);

                            var elPartPosTo = Matrix.CreateTranslation(-elevationPartLocation);
                            var elPartPosFrom = Matrix.CreateTranslation(elevationPartLocation);

                            var fullStepElRotation = elPartPosTo * MatrixD.CreateRotationX(-m.Value.ElStep) * elPartPosFrom;

                            var rFullStepElRotation = Matrix.Invert(fullStepElRotation);

                            weapon.ElevationPart.Item2 = elPartPosTo;
                            weapon.ElevationPart.Item3 = elPartPosFrom;
                            weapon.ElevationPart.Item4 = fullStepElRotation;
                            weapon.ElevationPart.Item5 = rFullStepElRotation;
                            weapon.ElevationPart.Item6 = elevationPartLocation;
                        }
                        else if (elevationPartName == "None")
                        {
                            weapon.ElevationPart.Item2 = Matrix.Zero;
                            weapon.ElevationPart.Item3 = Matrix.Zero;
                            weapon.ElevationPart.Item4 = Matrix.Zero;
                            weapon.ElevationPart.Item5 = Matrix.Zero;
                            weapon.ElevationPart.Item6 = Vector3.Zero;
                        }
                    }

                    var barrelCount = m.Value.Barrels.Length;
                    if (reset)
                    {
                        var registered = false;
                        try
                        {
                            foreach (var animationSet in weapon.AnimationsSet)
                            {
                                foreach (var animation in animationSet.Value)
                                {
                                    MyEntity part;
                                    if (Parts.NameToEntity.TryGetValue(animation.SubpartId, out part))
                                    {
                                        animation.Part = (MyEntitySubpart)part;
                                        animation.Reset();

                                        //if (comp.Session.AnimationsToProcess.Contains(animation))
                                            //comp.Session.AnimationsToProcess.Remove(animation);

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
                        weapon.MuzzlePart.Item1.PositionComp.OnPositionChanged += weapon.PositionChanged;
                        weapon.MuzzlePart.Item1.OnMarkForClose += weapon.EntPartClose;
                    }
                    else
                    {
                        if(weapon.ElevationPart.Item1 != null)
                        {
                            weapon.ElevationPart.Item1.PositionComp.OnPositionChanged += weapon.PositionChanged;
                            weapon.ElevationPart.Item1.OnMarkForClose += weapon.EntPartClose;
                        }
                        else
                        {
                            weapon.AzimuthPart.Item1.PositionComp.OnPositionChanged += weapon.PositionChanged;
                            weapon.AzimuthPart.Item1.OnMarkForClose += weapon.EntPartClose;
                        }
                    }

                    for (int i = 0; i < barrelCount; i++)
                    {
                        var barrel = m.Value.Barrels[i];

                        weapon.MuzzleIdToName.Add(i, barrel);
                        if (weapon.Muzzles[i] == null)
                        {
                            weapon.Dummies[i] = new Dummy(weapon.MuzzlePart.Item1, barrel);
                            weapon.Muzzles[i] = new Muzzle(i);
                        }
                        else
                            weapon.Dummies[i].Entity = weapon.MuzzlePart.Item1;
                    }

                    //was run only on weapon first build, needs to run every reset as well
                    try
                    {
                        foreach (var emissive in weapon.System.WeaponEmissiveSet)
                        {
                            if (emissive.Value.EmissiveParts == null) continue;

                            foreach (var part in emissive.Value.EmissiveParts)
                            {
                                Parts.SetEmissiveParts(part, Color.Transparent, 0);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        //cant check for emissives so may be null ref
                    }

                    c++;
                }
            }
        }

        internal void ResetParts(WeaponComponent comp)
        {
            for (int i = 0; i < Weapons.Length; i++)
            {
                var w = Weapons[i];
                w.MuzzleIdToName.Clear();
                if (w.MuzzlePart.Item1 == null) continue;
                w.MuzzlePart.Item1.PositionComp.OnPositionChanged -= w.PositionChanged;
            }

            Parts.Clean(comp.Entity as MyEntity);
            Parts.CheckSubparts();

            CompileTurret(comp, true);
            comp.Status = Started;
        }

        internal void RemoveParts(WeaponComponent comp)
        {
            foreach (var w in comp.Platform.Weapons)
            {
                if (w.MuzzlePart.Item1 == null) continue;
                w.MuzzlePart.Item1.PositionComp.OnPositionChanged -= w.PositionChanged;
                
            }
            Parts.Clean(comp.Entity as MyEntity);
            comp.Status = Stopped;
        }
    }
}
