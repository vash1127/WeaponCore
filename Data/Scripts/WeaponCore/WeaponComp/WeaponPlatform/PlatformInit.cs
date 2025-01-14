﻿using Sandbox.Game.Entities;
using System;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using System.Collections.Generic;
using Sandbox.ModAPI;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Support.WeaponComponent.BlockType;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore.Platform
{
    public class MyWeaponPlatform
    {
        internal readonly RecursiveSubparts Parts = new RecursiveSubparts();
        internal readonly MySoundPair RotationSound = new MySoundPair();
        private readonly List<int> _orderToCreate = new List<int>();
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
            Incomplete
        }

        internal void Setup(WeaponComponent comp)
        {
            if (!comp.Session.WeaponPlatforms.ContainsKey(comp.MyCube.BlockDefinition.Id))
            {
                PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.MyCube.BlockDefinition.Id.SubtypeId.String}) was not found in platform setup, I am crashing now Dave.");
                return;
            }
            Structure = comp.Session.WeaponPlatforms[comp.MyCube.BlockDefinition.Id];
            Comp = comp;

            if (Weapons.Length != Structure.MuzzlePartNames.Length)
                Array.Resize(ref Weapons, Structure.MuzzlePartNames.Length);
        }

        internal void Clean()
        {
            for (int i = 0; i < Weapons.Length; i++) Weapons[i] = null;
            Parts.Clean(null);
            Structure = null;
            State = PlatformState.Fresh;
            Comp = null;
        }

        internal PlatformState Init(WeaponComponent comp)
        {

            if (comp.MyCube.MarkedForClose) 
                return PlatformCrash(comp, true, false, $"Your block subTypeId ({comp.MyCube.BlockDefinition.Id.SubtypeId.String}) markedForClose, init platform invalid, I am crashing now Dave.");
            
            if (!comp.MyCube.IsFunctional) {
                State = PlatformState.Delay;
                return State;
            }

            //Get or init Ai
            var newAi = false;
            if (!Comp.Session.GridTargetingAIs.TryGetValue(Comp.MyCube.CubeGrid, out Comp.Ai)) {
                newAi = true;
                Comp.Ai = Comp.Session.GridAiPool.Get();
                Comp.Ai.Init(Comp.MyCube.CubeGrid, Comp.Session);
                Comp.Session.GridTargetingAIs.TryAdd(Comp.MyCube.CubeGrid, Comp.Ai);
            }

            var blockDef = Comp.MyCube.BlockDefinition.Id.SubtypeId; 
            if (!Comp.Ai.WeaponCounter.ContainsKey(blockDef)) 
                Comp.Ai.WeaponCounter[blockDef] = Comp.Session.WeaponCountPool.Get();

            var wCounter = comp.Ai.WeaponCounter[blockDef];
            wCounter.Max = Structure.GridWeaponCap;

            if (newAi) {

                Comp.SubGridInit();
                if (Comp.Ai.MarkedForClose)
                    Log.Line($"PlatFormInit and AI MarkedForClose: CubeMarked:{Comp.MyCube.MarkedForClose}");
            }

            if (wCounter.Max == 0 || Comp.Ai.Construct.GetWeaponCount(blockDef) + 1 <= wCounter.Max) {
                wCounter.Current++;
                GridAi.Constructs.UpdateWeaponCounters(Comp.Ai);
                State = PlatformState.Valid;
            }
            else
                return PlatformCrash(comp, true, false, $"{blockDef.String} over block limits: {wCounter.Current}.");
            
            Parts.Entity = comp.Entity as MyEntity;

            return GetParts(comp);
        }

        private PlatformState GetParts(WeaponComponent comp)
        {
            for (int i = 0; i < Structure.MuzzlePartNames.Length; i++)
                _orderToCreate.Add(i);

            if (Structure.PrimaryWeapon > 0) {
                var tmpPos = _orderToCreate[Structure.PrimaryWeapon];
                _orderToCreate[tmpPos] = _orderToCreate[0];
                _orderToCreate[0] = tmpPos;
            }

            Parts.CheckSubparts();

            foreach (var i in _orderToCreate)
            {
                var muzzlePartHash = Structure.MuzzlePartNames[i];
                WeaponSystem system;
                if (!Structure.WeaponSystems.TryGetValue(muzzlePartHash, out system)) 
                    return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.MyCube.BlockDefinition.Id.SubtypeId.String}) Invalid weapon system, I am crashing now Dave.");

                var muzzlePartName = muzzlePartHash.String != "Designator" ? muzzlePartHash.String : system.ElevationPartName.String;

                MyEntity muzzlePartEntity;
                if (!Parts.NameToEntity.TryGetValue(muzzlePartName, out muzzlePartEntity)) {
                    return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.MyCube.BlockDefinition.Id.SubtypeId.String}) Invalid barrelPart, I am crashing now Dave.");
                }
                foreach (var part in Parts.NameToEntity) {
                    part.Value.OnClose += comp.SubpartClosed;
                    break;
                }

                //compatability with old configs of converted turrets
                var azimuthPartName = comp.BaseType == Turret ? string.IsNullOrEmpty(system.AzimuthPartName.String) ? "MissileTurretBase1" : system.AzimuthPartName.String : system.AzimuthPartName.String;
                var elevationPartName = comp.BaseType == Turret ? string.IsNullOrEmpty(system.ElevationPartName.String) ? "MissileTurretBarrels" : system.ElevationPartName.String : system.ElevationPartName.String;

                MyEntity azimuthPart = null;
                if (!Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPart)) 
                    return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.MyCube.BlockDefinition.Id.SubtypeId.String}) Weapon: {system.WeaponName} Invalid azimuthPart, I am crashing now Dave.");

                MyEntity elevationPart = null;
                if (!Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPart)) 
                    return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.MyCube.BlockDefinition.Id.SubtypeId.String}) Invalid elevationPart, I am crashing now Dave.");

                azimuthPart.NeedsWorldMatrix = true;
                elevationPart.NeedsWorldMatrix = true;

                var weapon = Weapons[i] = new Weapon(muzzlePartEntity, system, i, comp, Parts, elevationPart, azimuthPart, azimuthPartName, elevationPartName);
                
                SetupUi(weapon);

                if (!comp.Debug && weapon.System.Values.HardPoint.Other.Debug)
                    comp.Debug = true;

                if (weapon.System.Values.HardPoint.Ai.TurretController) {
                    if (weapon.System.Values.HardPoint.Ai.PrimaryTracking && comp.TrackingWeapon == null)
                        comp.TrackingWeapon = weapon;

                    if (weapon.AvCapable && weapon.System.HardPointRotationSound)
                        RotationSound.Init(weapon.System.Values.HardPoint.Audio.HardPointRotationSound, false);
                }
            }
            _orderToCreate.Clear();

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
                        weapon.MuzzlePart.Entity.NeedsWorldMatrix = true;
                    }

                    if (weapon.AiOnlyWeapon)
                    {
                        var azimuthPart = weapon.AzimuthPart.Entity;
                        var elevationPart = weapon.ElevationPart.Entity;
                        if (azimuthPart != null && azimuthPartName != "None" && weapon.System.TurretMovement != WeaponSystem.TurretType.ElevationOnly)
                        {

                            var azimuthPartLocation = comp.Session.GetPartLocation("subpart_" + azimuthPartName, azimuthPart.Parent.Model);
                            var partDummy = comp.Session.GetPartDummy("subpart_" + azimuthPartName, azimuthPart.Parent.Model);
                            if (partDummy == null)
                            {
                                PlatformCrash(comp, true, true, $"partDummy null: name:{azimuthPartName} - azimuthPartParentNull:{azimuthPart.Parent == null}, I am crashing now Dave.");
                                return;
                            }

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
                            weapon.AzimuthPart.OriginalPosition = azimuthPart.PositionComp.LocalMatrixRef;
                            //weapon.AzimuthPart.Entity.NeedsWorldMatrix = true;

                        }
                        else
                        {
                            weapon.AzimuthPart.RotationAxis = Vector3.Zero;
                            weapon.AzimuthPart.ToTransformation = MatrixD.Zero;
                            weapon.AzimuthPart.FromTransformation = MatrixD.Zero;
                            weapon.AzimuthPart.FullRotationStep = MatrixD.Zero;
                            weapon.AzimuthPart.RevFullRotationStep = MatrixD.Zero;
                            weapon.AzimuthPart.PartLocalLocation = Vector3.Zero;
                            weapon.AzimuthPart.OriginalPosition = MatrixD.Zero;
                            //weapon.AzimuthPart.Entity.NeedsWorldMatrix = true;

                        }

                        if (elevationPart != null && elevationPartName != "None" && weapon.System.TurretMovement != WeaponSystem.TurretType.AzimuthOnly)
                        {
                            var elevationPartLocation = comp.Session.GetPartLocation("subpart_" + elevationPartName, elevationPart.Parent.Model);
                            var partDummy = comp.Session.GetPartDummy("subpart_" + elevationPartName, elevationPart.Parent.Model);
                            if (partDummy == null) {
                                PlatformCrash(comp, true, true, $"partDummy null: name:{elevationPartName} - azimuthPartParentNull:{elevationPart.Parent == null}, I am crashing now Dave.");
                                return;
                            }
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
                            weapon.ElevationPart.OriginalPosition = elevationPart.PositionComp.LocalMatrix;
                            //weapon.ElevationPart.Entity.NeedsWorldMatrix = true;

                        }
                        else if (elevationPartName == "None")
                        {
                            weapon.ElevationPart.RotationAxis = Vector3.Zero;
                            weapon.ElevationPart.ToTransformation = MatrixD.Zero;
                            weapon.ElevationPart.FromTransformation = MatrixD.Zero;
                            weapon.ElevationPart.FullRotationStep = MatrixD.Zero;
                            weapon.ElevationPart.RevFullRotationStep = MatrixD.Zero;
                            weapon.ElevationPart.PartLocalLocation = Vector3.Zero;
                            weapon.ElevationPart.OriginalPosition = MatrixD.Zero;
                            //weapon.ElevationPart.Entity.NeedsWorldMatrix = true;
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

                    for (int i = 0; i < barrelCount; i++)
                    {
                        var barrel = m.Value.Barrels[i];

                        weapon.MuzzleIdToName.Add(i, barrel);
                        if (weapon.Muzzles[i] == null)
                        {
                            weapon.Dummies[i] = new Dummy(weapon.MuzzlePart.Entity, weapon, barrel);
                            weapon.Muzzles[i] = new Muzzle(weapon, i, comp.Session);
                        }
                        else
                            weapon.Dummies[i].Entity = weapon.MuzzlePart.Entity;
                    }

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

                    if (weapon.Comp.FunctionalBlock.Enabled)
                        if (weapon.AnimationsSet.ContainsKey(EventTriggers.TurnOn))
                            weapon.Comp.Session.FutureEvents.Schedule(weapon.TurnOnAV, null, 4);
                        else
                        if (weapon.AnimationsSet.ContainsKey(EventTriggers.TurnOff))
                            weapon.Comp.Session.FutureEvents.Schedule(weapon.TurnOffAv, null, 4);

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
                        weapon.AzimuthPart.Entity.NeedsWorldMatrix = true;
                    }

                    MyEntity elevationPartEntity;
                    if (Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPartEntity))
                    {
                        weapon.ElevationPart.Entity = elevationPartEntity;
                        //weapon.ElevationPart.Entity.InvalidateOnMove = false;
                        weapon.ElevationPart.Entity.NeedsWorldMatrix = true;
                    }

                    string ejectorMatch;
                    MyEntity ejectorPart;
                    if (weapon.System.HasEjector && Comp.Platform.Parts.FindFirstDummyByName(weapon.System.Values.Assignments.Ejector, weapon.System.AltEjectorName, out ejectorPart, out ejectorMatch))
                        weapon.Ejector.Entity = ejectorPart;

                    string scopeMatch;
                    MyEntity scopePart;
                    if ((weapon.System.HasScope) && Comp.Platform.Parts.FindFirstDummyByName(weapon.System.Values.Assignments.Scope, weapon.System.AltScopeName, out scopePart, out scopeMatch))
                        weapon.Scope.Entity = scopePart;

                    if (m.Value.DesignatorWeapon)
                        muzzlePart = weapon.ElevationPart.Entity;

                    weapon.MuzzlePart.Entity = muzzlePart;

                    weapon.HeatingParts.Clear();
                    weapon.HeatingParts.Add(weapon.MuzzlePart.Entity);

                    foreach (var animationSet in weapon.AnimationsSet)
                    {
                        for(int i = 0; i < animationSet.Value.Length; i++)
                        {
                            var animation = animationSet.Value[i];
                            MyEntity part;
                            if (Parts.NameToEntity.TryGetValue(animation.SubpartId, out part) && !(string.IsNullOrEmpty(animation.SubpartId) || animation.SubpartId == "None"))
                            {
                                animation.Part = part;
                                //if (animation.Running)
                                //  animation.Paused = true;
                                animation.Reset();
                            }
                        }
                    }

                    foreach (var particleEvents in weapon.ParticleEvents)
                    {
                        for (int i = 0; i < particleEvents.Value.Length; i++)
                        {
                            var particle = particleEvents.Value[i];

                            MyEntity part;
                            if (Parts.NameToEntity.TryGetValue(particle.PartName, out part))
                                particle.MyDummy.Entity = part;
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

                    if (weapon.Comp.IsWorking)
                        if (weapon.AnimationsSet.ContainsKey(EventTriggers.TurnOn))
                            weapon.Comp.Session.FutureEvents.Schedule(weapon.TurnOnAV, null, 4);
                        else
                        if (weapon.AnimationsSet.ContainsKey(EventTriggers.TurnOff))
                            weapon.Comp.Session.FutureEvents.Schedule(weapon.TurnOffAv, null, 4);
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
            var ui = w.System.Values.HardPoint.Ui;
            w.Comp.HasGuidanceToggle = w.Comp.HasGuidanceToggle || ui.ToggleGuidance;
            w.Comp.HasDamageSlider = w.Comp.HasDamageSlider || (!w.CanUseChargeAmmo && ui.DamageModifier && w.CanUseEnergyAmmo || w.CanUseHybridAmmo);
            w.Comp.HasRofSlider = w.Comp.HasRofSlider || (ui.RateOfFire && !w.CanUseChargeAmmo);
            w.Comp.CanOverload = w.Comp.CanOverload || (ui.EnableOverload && w.CanUseBeams && !w.CanUseChargeAmmo);
            w.Comp.HasTurret = w.Comp.HasTurret || (w.System.Values.HardPoint.Ai.TurretAttached);
            w.Comp.OverrideLeads = w.Comp.OverrideLeads || w.System.Values.HardPoint.Ai.OverrideLeads;
            w.Comp.HasTracking = w.Comp.HasTracking || w.System.Values.HardPoint.Ai.TrackTargets;
            w.Comp.HasGuidedAmmo = w.Comp.HasGuidedAmmo || w.System.HasGuidedAmmo;
            w.Comp.HasDelayToFire = w.Comp.HasDelayToFire || w.System.DelayToFire > 0;
            w.Comp.HasChargeWeapon = w.Comp.HasChargeWeapon || w.CanUseChargeAmmo;
            w.Comp.ShootSubmerged = w.Comp.ShootSubmerged || w.System.Values.HardPoint.CanShootSubmerged;
            if (ui.DamageModifier || ui.EnableOverload || ui.RateOfFire || ui.ToggleGuidance)
                w.Comp.UiEnabled = true;

            if (w.System.HasAmmoSelection)
                w.Comp.AmmoSelectionWeaponIds.Add(w.WeaponId);

            foreach (var m in w.System.Values.Assignments.MountPoints) {
                if (m.SubtypeId == Comp.SubtypeHash.String && !string.IsNullOrEmpty(m.IconName)) {
                    Comp.CustomIcon = m.IconName;
                }
            }
        }
        
        internal PlatformState PlatformCrash(WeaponComponent comp, bool markInvalid, bool suppress, string message)
        {
            if (suppress)
                comp.Session.SuppressWc = true;
            
            if (markInvalid)
                State = PlatformState.Invalid;
            
            if (Comp.Session.HandlesInput) {
                if (suppress)
                    MyAPIGateway.Utilities.ShowNotification($"WeaponCore hard crashed during block init, shutting down\n Send log files to server admin or submit a bug report to mod author:\n {comp.Platform?.Structure?.ModPath} - {comp.MyCube.BlockDefinition.Id.SubtypeName}", 10000);
            }
            Log.Line($"PlatformCrash: {Comp.MyCube.BlockDefinition.Id.SubtypeName} - {message}");

            return State;
        }
    }
}
