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
using System.Collections.Generic;

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

                foreach (var triggerSet in wepAnimationSet)
                {
                    for(int j = 0; j < triggerSet.Value.Length; j++)
                    {
                        comp.AllAnimations.Add(triggerSet.Value[j]);
                    }
                }

                Weapons[i] = new Weapon(muzzlePartEntity, system, i, comp, wepAnimationSet)
                {
                    Muzzles = new Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                    AzimuthPart = new PartInfo{ Entity = azimuthPart},
                    ElevationPart = new PartInfo { Entity = elevationPart },
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

                    var muzzlePartName  = m.Key.String;
                    if (m.Value.DesignatorWeapon)
                    {
                        muzzlePart = weapon.ElevationPart.Entity;
                        muzzlePartName = elevationPartName;
                    }

                    weapon.MuzzlePart.Entity = muzzlePart;

                    if (muzzlePartName != "None")
                    {
                        var muzzlePartLocation = comp.Session.GetPartLocation("subpart_" + muzzlePartName, muzzlePart.Parent.Model);

                        var muzzlePartPosTo = Matrix.CreateTranslation(-muzzlePartLocation);
                        var muzzlePartPosFrom = Matrix.CreateTranslation(muzzlePartLocation);

                        weapon.MuzzlePart.ToTransformation = muzzlePartPosTo;
                        weapon.MuzzlePart.FromTransformation = muzzlePartPosFrom;
                        weapon.MuzzlePart.PartLocalLocation = muzzlePartLocation;

                        try
                        {
                            weapon.MuzzlePart.Entity.SetEmissiveParts("Heating", Color.Transparent, 0);
                        }
                        catch (Exception ex) { Log.Line($"Exception in no emissive parts for barrel: {ex}"); }

                    }

                    if (weapon.AiOnlyWeapon)
                    {
                        var azimuthPart = weapon.AzimuthPart.Entity;
                        var elevationPart = weapon.ElevationPart.Entity;

                        if (azimuthPart != null && azimuthPartName != "None")
                        {
                            var azimuthPartLocation = comp.Session.GetPartLocation("subpart_" + azimuthPartName, azimuthPart.Parent.Model);
                            var azPartPosTo = Matrix.CreateTranslation(-azimuthPartLocation);
                            var azPrtPosFrom = Matrix.CreateTranslation(azimuthPartLocation);
                            var fullStepAzRotation = azPartPosTo * MatrixD.CreateRotationY(-m.Value.AzStep) * azPrtPosFrom;
                            var rFullStepAzRotation = Matrix.Invert(fullStepAzRotation);

                            weapon.AzimuthPart.ToTransformation = azPartPosTo;
                            weapon.AzimuthPart.FromTransformation = azPrtPosFrom;
                            weapon.AzimuthPart.FullRotationStep = fullStepAzRotation;
                            weapon.AzimuthPart.RevFullRotationStep = rFullStepAzRotation;
                            weapon.AzimuthPart.PartLocalLocation = azimuthPartLocation;
                        }
                        else if (azimuthPartName == "None")
                        {
                            weapon.AzimuthPart.ToTransformation = Matrix.Zero;
                            weapon.AzimuthPart.FromTransformation = Matrix.Zero;
                            weapon.AzimuthPart.FullRotationStep = Matrix.Zero;
                            weapon.AzimuthPart.RevFullRotationStep = Matrix.Zero;
                            weapon.AzimuthPart.PartLocalLocation = Vector3.Zero;
                        }


                        if (elevationPart != null && elevationPartName != "None")
                        {
                            var elevationPartLocation = comp.Session.GetPartLocation("subpart_" + elevationPartName, elevationPart.Parent.Model);

                            var elPartPosTo = Matrix.CreateTranslation(-elevationPartLocation);
                            var elPartPosFrom = Matrix.CreateTranslation(elevationPartLocation);

                            var fullStepElRotation = elPartPosTo * MatrixD.CreateRotationX(-m.Value.ElStep) * elPartPosFrom;

                            var rFullStepElRotation = Matrix.Invert(fullStepElRotation);

                            weapon.ElevationPart.ToTransformation = elPartPosTo;
                            weapon.ElevationPart.FromTransformation = elPartPosFrom;
                            weapon.ElevationPart.FullRotationStep = fullStepElRotation;
                            weapon.ElevationPart.RevFullRotationStep = rFullStepElRotation;
                            weapon.ElevationPart.PartLocalLocation = elevationPartLocation;
                        }
                        else if (elevationPartName == "None")
                        {
                            weapon.ElevationPart.ToTransformation = Matrix.Zero;
                            weapon.ElevationPart.FromTransformation = Matrix.Zero;
                            weapon.ElevationPart.FullRotationStep = Matrix.Zero;
                            weapon.ElevationPart.RevFullRotationStep = Matrix.Zero;
                            weapon.ElevationPart.PartLocalLocation = Vector3.Zero;
                        }
                    }

                    var barrelCount = m.Value.Barrels.Length;

                    if (m.Key.String != "Designator")
                    {
                        weapon.MuzzlePart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                        weapon.MuzzlePart.Entity.OnMarkForClose += weapon.EntPartClose;
                    }
                    else
                    {
                        if(weapon.ElevationPart.Entity != null)
                        {
                            weapon.ElevationPart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                            weapon.ElevationPart.Entity.OnMarkForClose += weapon.EntPartClose;
                        }
                        else
                        {
                            weapon.AzimuthPart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                            weapon.AzimuthPart.Entity.OnMarkForClose += weapon.EntPartClose;
                        }
                    }

                    for (int i = 0; i < barrelCount; i++)
                    {
                        var barrel = m.Value.Barrels[i];

                        weapon.MuzzleIdToName.Add(i, barrel);
                        if (weapon.Muzzles[i] == null)
                        {
                            weapon.Dummies[i] = new Dummy(weapon.MuzzlePart.Entity, barrel);
                            weapon.Muzzles[i] = new Muzzle(i);
                        }
                        else
                            weapon.Dummies[i].Entity = weapon.MuzzlePart.Entity;
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
            foreach (var part in Parts.NameToEntity)
            {
                comp.SubpartStates.Add(new KeyValuePair<MyEntity, MatrixD>(part.Value, MatrixD.Zero));
                var index = comp.SubpartStates.Count - 1;
                var name = part.Key;
                comp.SubpartNameToIndex[name] = index;
                comp.SubpartIndexToName[index] = name;
            }
        }

        internal void ResetTurret(WeaponComponent comp)
        {
            var c = 0;
            var registered = false;
            foreach (var m in Structure.WeaponSystems)
            {
                MyEntity muzzlePart = null;
                if (Parts.NameToEntity.TryGetValue(m.Key.String, out muzzlePart) || m.Value.DesignatorWeapon)
                {
                    if (!registered)
                    {
                        Parts.NameToEntity.FirstPair().Value.OnClose += Comp.SubpartClosed;
                        registered = true;
                    }

                    var azimuthPartName = comp.BaseType == Turret ? string.IsNullOrEmpty(m.Value.AzimuthPartName.String) ? "MissileTurretBase1" : m.Value.AzimuthPartName.String : m.Value.AzimuthPartName.String;
                    var elevationPartName = comp.BaseType == Turret ? string.IsNullOrEmpty(m.Value.ElevationPartName.String) ? "MissileTurretBarrels" : m.Value.ElevationPartName.String : m.Value.ElevationPartName.String;
                    var weapon = Weapons[c];
                    MyEntity azimuthPartEntity;
                    if (Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPartEntity))
                        weapon.AzimuthPart.Entity = azimuthPartEntity;

                    MyEntity elevationPartEntity;
                    if (Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPartEntity))
                        weapon.ElevationPart.Entity = elevationPartEntity;

                    if (m.Value.DesignatorWeapon)
                        muzzlePart = weapon.ElevationPart.Entity;

                    weapon.MuzzlePart.Entity = muzzlePart;

                    foreach (var animationSet in weapon.AnimationsSet)
                    {
                        foreach (var animation in animationSet.Value)
                        {
                            MyEntity part;
                            if (Parts.NameToEntity.TryGetValue(animation.SubpartId, out part))
                            {
                                animation.Part = (MyEntitySubpart)part;
                                if (animation.Running)
                                    animation.Paused = true;
                            }
                        }
                    }

                    if (m.Key.String != "Designator")
                    {
                        weapon.MuzzlePart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                        weapon.MuzzlePart.Entity.OnMarkForClose += weapon.EntPartClose;
                    }
                    else
                    {
                        if (weapon.ElevationPart.Entity != null)
                        {
                            weapon.ElevationPart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                            weapon.ElevationPart.Entity.OnMarkForClose += weapon.EntPartClose;
                            
                        }
                        else
                        {
                            weapon.AzimuthPart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                            weapon.AzimuthPart.Entity.OnMarkForClose += weapon.EntPartClose;
                        }
                    }

                    for (int i = 0; i < m.Value.Barrels.Length; i++)
                        weapon.Dummies[i].Entity = weapon.MuzzlePart.Entity;


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
                }
                c++;
            }
            foreach (var part in Parts.NameToEntity)
            {
                var index = comp.SubpartNameToIndex[part.Key];
                var matrix = comp.SubpartStates[index].Value;

                comp.SubpartStates[index] = new KeyValuePair<MyEntity, MatrixD>(part.Value, matrix);
            }
        }

        internal void ResetParts(WeaponComponent comp)
        {
            Parts.Clean(comp.Entity as MyEntity);
            Parts.CheckSubparts();

            //CompileTurret(comp, true);
            ResetTurret(comp);
            comp.Status = Started;
        }

        internal void RemoveParts(WeaponComponent comp)
        {
            foreach (var w in comp.Platform.Weapons)
            {
                if (w.MuzzlePart.Entity == null) continue;
                w.MuzzlePart.Entity.PositionComp.OnPositionChanged -= w.PositionChanged;
                
            }
            Parts.Clean(comp.Entity as MyEntity);
            comp.Status = Stopped;
        }
    }
}
