using Sandbox.Game.Entities;
using System;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using System.Collections.Generic;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Support.WeaponComponent.BlockType;
using static WeaponCore.Platform.Weapon;

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
            if (!comp.Session.WeaponPlatforms.ContainsKey(comp.SubtypeHash))
            {
                Log.Line($"Your block subTypeId ({comp.MyCube.BlockDefinition.Id.SubtypeId.String}) was not found in platform setup, I am crashing now Dave.");
                return;
            }
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
                Log.Line("closed, init platform invalid, I am crashing now Dave.");
                return State;
            }
            /*
            if (!comp.MyCube.IsFunctional)
            {
                State = PlatformState.Delay;
                return State;
            }
            */
            var blockDef = Comp.MyCube.BlockDefinition.Id.SubtypeId;
            if (!Comp.Ai.WeaponCounter.ContainsKey(blockDef))
                Comp.Ai.WeaponCounter.TryAdd(blockDef, Comp.Session.WeaponCountPool.Get());
            Comp.Ai.WeaponCounter[blockDef].Current++;

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
                    Log.Line("init platform invalid, I am crashing now Dave.");
                    return State;
                }
            }
            else
                State = PlatformState.Valid;

            Parts.Entity = comp.Entity as MyEntity;

            return GetParts(comp);
        }

        private PlatformState GetParts(WeaponComponent comp)
        {
            Parts.CheckSubparts();
            for (int i = 0; i < Structure.MuzzlePartNames.Length; i++)
            {
                var muzzlePartHash = Structure.MuzzlePartNames[i];
                var barrelCount = Structure.WeaponSystems[muzzlePartHash].Barrels.Length;                

                WeaponSystem system;
                if (!Structure.WeaponSystems.TryGetValue(muzzlePartHash, out system))
                {
                    Log.Line($"Invalid weapon system, I am crashing now Dave.");
                    State = PlatformState.Invalid;
                    return State;
                }

                var wepAnimationSet = comp.Session.CreateWeaponAnimationSet(system, Parts);

                var muzzlePartName = muzzlePartHash.String != "Designator" ? muzzlePartHash.String : system.ElevationPartName.String;


                MyEntity muzzlePartEntity;
                if (!Parts.NameToEntity.TryGetValue(muzzlePartName, out muzzlePartEntity))
                {
                    Log.Line($"Invalid barrelPart, I am crashing now Dave.");
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
                if (!Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPart))
                {
                    Log.Line($"Invalid azimuthPart, I am crashing now Dave.");
                    State = PlatformState.Invalid;
                    return State;
                }

                MyEntity elevationPart = null;
                if (!Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPart))
                {
                    Log.Line($"Invalid elevationPart, I am crashing now Dave.");
                    State = PlatformState.Invalid;
                    return State;
                }

                foreach (var triggerSet in wepAnimationSet)
                    for(int j = 0; j < triggerSet.Value.Length; j++)
                        comp.AllAnimations.Add(triggerSet.Value[j]);

                Weapons[i] = new Weapon(muzzlePartEntity, system, i, comp, wepAnimationSet)
                {
                    Muzzles = new Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                    AzimuthPart = new PartInfo { Entity = azimuthPart },
                    ElevationPart = new PartInfo { Entity = elevationPart },
                    AzimuthOnBase = azimuthPart.Parent == comp.MyCube,
                    AiOnlyWeapon = comp.BaseType != Turret || (azimuthPartName != "MissileTurretBase1" && elevationPartName != "MissileTurretBarrels" && azimuthPartName != "InteriorTurretBase1" && elevationPartName != "InteriorTurretBase2" && azimuthPartName != "GatlingTurretBase1" && elevationPartName != "GatlingTurretBase2")
                };

                var weapon = Weapons[i];
                SetupUi(weapon);

                if (!comp.Debug && weapon.System.Values.HardPoint.Other.Debug)
                    comp.Debug = true;

                if (weapon.System.Values.HardPoint.Ai.TurretController)
                {
                    if (weapon.System.Values.HardPoint.Ai.PrimaryTracking && comp.TrackingWeapon == null)
                        comp.TrackingWeapon = weapon;

                    if (weapon.AvCapable && weapon.System.HardPointRotationSound)
                        RotationSound.Init(weapon.System.Values.HardPoint.Audio.HardPointRotationSound, false);
                }
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

                    weapon.HeatingParts = new List<MyEntity> {weapon.MuzzlePart.Entity};

                    if (muzzlePartName != "None")
                    {
                        var muzzlePartLocation = comp.Session.GetPartLocation("subpart_" + muzzlePartName, muzzlePart.Parent.Model);

                        var muzzlePartPosTo = MatrixD.CreateTranslation(-muzzlePartLocation);
                        var muzzlePartPosFrom = MatrixD.CreateTranslation(muzzlePartLocation);

                        weapon.MuzzlePart.ToTransformation = muzzlePartPosTo;
                        weapon.MuzzlePart.FromTransformation = muzzlePartPosFrom;
                        weapon.MuzzlePart.PartLocalLocation = muzzlePartLocation;
                    }

                    if (weapon.AiOnlyWeapon)
                    {
                        var azimuthPart = weapon.AzimuthPart.Entity;
                        var elevationPart = weapon.ElevationPart.Entity;

                        if (azimuthPart != null && azimuthPartName != "None" && weapon.System.TurretMovement != WeaponSystem.TurretType.ElevationOnly)
                        {
                            var azimuthPartLocation = comp.Session.GetPartLocation("subpart_" + azimuthPartName, azimuthPart.Parent.Model);
                            var partDummy = comp.Session.GetPartDummy("subpart_" + azimuthPartName, azimuthPart.Parent.Model);

                            var azPartPosTo = MatrixD.CreateTranslation(-azimuthPartLocation);
                            var azPrtPosFrom = MatrixD.CreateTranslation(azimuthPartLocation);
                            var fullStepAzRotation = azPartPosTo * MatrixD.CreateFromAxisAngle(partDummy.Matrix.Up, - m.Value.AzStep) * azPrtPosFrom;
                            var rFullStepAzRotation = MatrixD.Invert(fullStepAzRotation);

                            weapon.AzimuthPart.RotationAxis = partDummy.Matrix.Up;
                            weapon.AzimuthPart.ToTransformation = azPartPosTo;
                            weapon.AzimuthPart.FromTransformation = azPrtPosFrom;
                            weapon.AzimuthPart.FullRotationStep = fullStepAzRotation;
                            weapon.AzimuthPart.RevFullRotationStep = rFullStepAzRotation;
                            weapon.AzimuthPart.PartLocalLocation = azimuthPartLocation;
                        }
                        else
                        {
                            weapon.AzimuthPart.RotationAxis = Vector3.Zero;
                            weapon.AzimuthPart.ToTransformation = MatrixD.Zero;
                            weapon.AzimuthPart.FromTransformation = MatrixD.Zero;
                            weapon.AzimuthPart.FullRotationStep = MatrixD.Zero;
                            weapon.AzimuthPart.RevFullRotationStep = MatrixD.Zero;
                            weapon.AzimuthPart.PartLocalLocation = Vector3.Zero;
                        }


                        if (elevationPart != null && elevationPartName != "None" && weapon.System.TurretMovement != WeaponSystem.TurretType.AzimuthOnly)
                        {
                            var elevationPartLocation = comp.Session.GetPartLocation("subpart_" + elevationPartName, elevationPart.Parent.Model);
                            var partDummy = comp.Session.GetPartDummy("subpart_" + elevationPartName, elevationPart.Parent.Model);

                            var elPartPosTo = MatrixD.CreateTranslation(-elevationPartLocation);
                            var elPartPosFrom = MatrixD.CreateTranslation(elevationPartLocation);

                            var fullStepElRotation = elPartPosTo * MatrixD.CreateFromAxisAngle(partDummy.Matrix.Left, m.Value.ElStep) * elPartPosFrom;

                            var rFullStepElRotation = MatrixD.Invert(fullStepElRotation);

                            weapon.ElevationPart.RotationAxis = partDummy.Matrix.Left;
                            weapon.ElevationPart.ToTransformation = elPartPosTo;
                            weapon.ElevationPart.FromTransformation = elPartPosFrom;
                            weapon.ElevationPart.FullRotationStep = fullStepElRotation;
                            weapon.ElevationPart.RevFullRotationStep = rFullStepElRotation;
                            weapon.ElevationPart.PartLocalLocation = elevationPartLocation;
                        }
                        else if (elevationPartName == "None")
                        {
                            weapon.ElevationPart.RotationAxis = Vector3.Zero;
                            weapon.ElevationPart.ToTransformation = MatrixD.Zero;
                            weapon.ElevationPart.FromTransformation = MatrixD.Zero;
                            weapon.ElevationPart.FullRotationStep = MatrixD.Zero;
                            weapon.ElevationPart.RevFullRotationStep = MatrixD.Zero;
                            weapon.ElevationPart.PartLocalLocation = Vector3.Zero;
                        }
                    }

                    var barrelCount = m.Value.Barrels.Length;

                    if (m.Key.String != "Designator")
                    {
                        weapon.MuzzlePart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                        weapon.MuzzlePart.Entity.OnMarkForClose += weapon.EntPartClose;

                        if (comp.Session.VanillaSubpartNames.Contains(weapon.System.AzimuthPartName.String) && comp.Session.VanillaSubpartNames.Contains(weapon.System.ElevationPartName.String))
                            weapon.ElevationPart.Entity.PositionComp.OnPositionChanged += weapon.UpdateParts;
                    }
                    else
                    {
                        if(weapon.ElevationPart.Entity != null)
                        {
                            weapon.ElevationPart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                            weapon.ElevationPart.Entity.OnMarkForClose += weapon.EntPartClose;

                            if (comp.BaseType == Turret && comp.Session.VanillaSubpartNames.Contains(weapon.System.AzimuthPartName.String) && comp.Session.VanillaSubpartNames.Contains(weapon.System.ElevationPartName.String))
                                weapon.ElevationPart.Entity.PositionComp.OnPositionChanged += weapon.UpdateParts;
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

                    for(int i = 0; i < m.Value.HeatingSubparts.Length; i++)
                    {
                        var partName = m.Value.HeatingSubparts[i];
                        MyEntity ent;
                        if (Parts.NameToEntity.TryGetValue(partName, out ent))
                        {
                            weapon.HeatingParts.Add(ent);
                            try
                            {
                                ent.SetEmissiveParts("Heating", Color.Transparent, 0);
                            }
                            catch (Exception ex) { Log.Line($"Exception no emmissive Found: {ex}"); }
                        }
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
                    {
                        weapon.AzimuthPart.Entity = azimuthPartEntity;
                        weapon.AzimuthPart.Parent = azimuthPartEntity.Parent;
                        //weapon.AzimuthPart.Entity.InvalidateOnMove = false;
                        //weapon.AzimuthPart.Entity.NeedsWorldMatrix = false;
                    }

                    MyEntity elevationPartEntity;
                    if (Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPartEntity))
                    {
                        weapon.ElevationPart.Entity = elevationPartEntity;
                        //weapon.ElevationPart.Entity.InvalidateOnMove = false;
                        //weapon.ElevationPart.Entity.NeedsWorldMatrix = false;
                    }

                    if (m.Value.DesignatorWeapon)
                        muzzlePart = weapon.ElevationPart.Entity;

                    weapon.MuzzlePart.Entity = muzzlePart;

                    weapon.HeatingParts.Clear();
                    weapon.HeatingParts.Add(weapon.MuzzlePart.Entity);

                    foreach (var animationSet in weapon.AnimationsSet)
                    {
                        foreach (var animation in animationSet.Value)
                        {
                            MyEntity part;
                            if (Parts.NameToEntity.TryGetValue(animation.SubpartId, out part))
                            {
                                animation.Part = (MyEntitySubpart)part;
                                //if (animation.Running)
                                //  animation.Paused = true;
                                animation.Reset();
                            }
                        }
                    }

                    if (m.Key.String != "Designator")
                    {
                        weapon.MuzzlePart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                        weapon.MuzzlePart.Entity.OnMarkForClose += weapon.EntPartClose;

                        if(comp.Session.VanillaSubpartNames.Contains(weapon.System.AzimuthPartName.String) && comp.Session.VanillaSubpartNames.Contains(weapon.System.ElevationPartName.String))
                            weapon.ElevationPart.Entity.PositionComp.OnPositionChanged += weapon.UpdateParts;
                    }
                    else
                    {
                        if (weapon.ElevationPart.Entity != null)
                        {
                            weapon.ElevationPart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                            weapon.ElevationPart.Entity.OnMarkForClose += weapon.EntPartClose;

                            if (comp.Session.VanillaSubpartNames.Contains(weapon.System.AzimuthPartName.String) && comp.Session.VanillaSubpartNames.Contains(weapon.System.ElevationPartName.String))
                                weapon.ElevationPart.Entity.PositionComp.OnPositionChanged += weapon.UpdateParts;
                        }
                        else
                        {
                            weapon.AzimuthPart.Entity.PositionComp.OnPositionChanged += weapon.PositionChanged;
                            weapon.AzimuthPart.Entity.OnMarkForClose += weapon.EntPartClose;
                        }
                    }

                    for (int i = 0; i < m.Value.Barrels.Length; i++)
                        weapon.Dummies[i].Entity = weapon.MuzzlePart.Entity;


                    for (int i = 0; i < m.Value.HeatingSubparts.Length; i++)
                    {
                        var partName = m.Value.HeatingSubparts[i];
                        MyEntity ent;
                        if (Parts.NameToEntity.TryGetValue(partName, out ent))
                        {
                            weapon.HeatingParts.Add(ent);
                            try
                            {
                                ent.SetEmissiveParts("Heating", Color.Transparent, 0);
                            }
                            catch (Exception ex) { Log.Line($"Exception no emmissive Found: {ex}"); }
                        }
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
                }
                c++;
            }
        }

        internal void ResetParts(WeaponComponent comp)
        {
            Parts.Clean(comp.Entity as MyEntity);
            Parts.CheckSubparts();
            
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

        internal void SetupUi(Weapon w)
        {
            //UI elements
            w.Comp.HasGuidanceToggle = w.Comp.HasGuidanceToggle || w.System.Values.HardPoint.Ui.ToggleGuidance;
            w.Comp.HasDamageSlider = w.Comp.HasDamageSlider || (!w.CanUseChargeAmmo && w.System.Values.HardPoint.Ui.DamageModifier && w.CanUseEnergyAmmo || w.CanUseHybridAmmo);
            w.Comp.HasRofSlider = w.Comp.HasRofSlider || (w.System.Values.HardPoint.Ui.RateOfFire && !w.CanUseChargeAmmo);
            w.Comp.CanOverload = w.Comp.CanOverload || (w.System.Values.HardPoint.Ui.EnableOverload && w.CanUseBeams && !w.CanUseChargeAmmo);
            w.Comp.HasTurret = w.Comp.HasTurret || (w.System.Values.HardPoint.Ai.TurretAttached);
            w.Comp.HasChargeWeapon = w.Comp.HasChargeWeapon || w.CanUseChargeAmmo;
        }
    }
}
