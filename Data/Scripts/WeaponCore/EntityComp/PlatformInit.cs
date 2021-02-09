using Sandbox.Game.Entities;
using System;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using System.Collections.Generic;
using Sandbox.ModAPI;
using static WeaponCore.Support.CoreComponent.Start;
using static WeaponCore.Support.CoreComponent.CompTypeSpecific;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore.Platform
{
    public class CorePlatform
    {
        internal readonly RecursiveSubparts Parts = new RecursiveSubparts();
        internal readonly MySoundPair RotationSound = new MySoundPair();
        private readonly List<int> _orderToCreate = new List<int>();
        internal List<Weapon> Weapons = new List<Weapon>();
        internal List<SupportSys> Support = new List<SupportSys>();
        internal List<Upgrades> Upgrades = new List<Upgrades>();
        internal List<Phantoms> Phantoms = new List<Phantoms>();
        internal CoreStructure Structure;
        internal CoreComponent Comp;
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
        
        internal void Setup(CoreComponent comp)
        {
            if (!comp.Session.PartPlatforms.ContainsKey(comp.Id))
            {
                PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.SubtypeName}) was not found in platform setup, I am crashing now Dave.");
                return;
            }
            Structure = comp.Session.PartPlatforms[comp.Id];
            Comp = comp;
        }

        internal void Clean()
        {
            Weapons.Clear();
            Support.Clear();
            Upgrades.Clear();
            Phantoms.Clear();
            Parts.Clean(null);
            Structure = null;
            State = PlatformState.Fresh;
            Comp = null;
        }

        internal PlatformState Init(CoreComponent comp)
        {

            if (comp.CoreEntity.MarkedForClose) 
                return PlatformCrash(comp, true, false, $"Your block subTypeId ({comp.SubtypeName}) markedForClose, init platform invalid, I am crashing now Dave.");
            
            if (comp.IsBlock && !comp.Cube.IsFunctional || comp.CoreEntity.MarkedForClose) {
                State = PlatformState.Delay;
                return State;
            }

            //Get or init Ai
            var newAi = false;
            if (!Comp.Session.GridAIs.TryGetValue(Comp.TopEntity, out Comp.Ai)) {
                newAi = true;
                Comp.Ai = Comp.Session.GridAiPool.Get();
                Comp.Ai.Init(Comp.TopEntity, Comp.Session);
                Comp.Session.GridAIs.TryAdd(Comp.TopEntity, Comp.Ai);
            }

            var blockDef = Comp.SubTypeId; 
            if (!Comp.Ai.PartCounting.ContainsKey(blockDef)) 
                Comp.Ai.PartCounting[blockDef] = Comp.Session.PartCountPool.Get();

            var wCounter = comp.Ai.PartCounting[blockDef];
            wCounter.Max = Structure.ConstructPartCap;

            if (newAi) {

                Comp.SubGridInit();
                if (Comp.Ai.MarkedForClose)
                    Log.Line($"PlatFormInit and AI MarkedForClose: CubeMarked:{Comp.CoreEntity.MarkedForClose}");
            }

            if (wCounter.Max == 0 || Comp.Ai.Construct.GetPartCount(blockDef) + 1 <= wCounter.Max) {
                wCounter.Current++;
                Ai.Constructs.UpdatePartCounters(Comp.Ai);
                State = PlatformState.Valid;
            }
            else
                return PlatformCrash(comp, true, false, $"{blockDef.String} over block limits: {wCounter.Current}.");
            
            Parts.Entity = comp.Entity as MyEntity;

            return GetParts(comp);
        }

        private PlatformState GetParts(CoreComponent comp)
        {
            for (int i = 0; i < Structure.PartHashes.Length; i++)
                _orderToCreate.Add(i);

            if (Structure.PrimaryPart > 0) {
                var tmpPos = _orderToCreate[Structure.PrimaryPart];
                _orderToCreate[tmpPos] = _orderToCreate[0];
                _orderToCreate[0] = tmpPos;
            }
            Parts.CheckSubparts();

            foreach (var i in _orderToCreate)
            {
                if (Comp.Type == CoreComponent.CompType.Weapon)
                {
                    Log.Line($"weapon");
                    if (Phantoms.Count > 0 || Support.Count > 0 || Upgrades.Count > 0)
                        return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.SubtypeName}) mixed functions, cannot mix weapons/upgrades/armorSupport/phantoms, I am crashing now Dave.");

                    var partHashId = Structure.PartHashes[i];
                    CoreSystem coreSystem;
                    if (!Structure.PartSystems.TryGetValue(partHashId, out coreSystem) || !(coreSystem is WeaponSystem))
                        return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.SubtypeName}) Invalid weapon system, I am crashing now Dave.");

                    var system = (WeaponSystem)coreSystem;
                    var muzzlePartName = system.MuzzlePartName.String != "Designator" ? system.MuzzlePartName.String : system.ElevationPartName.String;
                    MyEntity muzzlePartEntity;
                    if (!Parts.NameToEntity.TryGetValue(muzzlePartName, out muzzlePartEntity))
                    {
                        return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.SubtypeName}) Invalid barrelPart, I am crashing now Dave.");
                    }
                    foreach (var part in Parts.NameToEntity)
                    {
                        part.Value.OnClose += comp.SubpartClosed;
                        break;
                    }

                    //compatability with old configs of converted turrets
                    var azimuthPartName = comp.TypeSpecific == VanillaTurret ? string.IsNullOrEmpty(system.AzimuthPartName.String) ? "MissileTurretBase1" : system.AzimuthPartName.String : system.AzimuthPartName.String;
                    var elevationPartName = comp.TypeSpecific == VanillaTurret ? string.IsNullOrEmpty(system.ElevationPartName.String) ? "MissileTurretBarrels" : system.ElevationPartName.String : system.ElevationPartName.String;

                    MyEntity azimuthPart = null;
                    if (!Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPart))
                        return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.SubtypeName}) Weapon: {system.PartName} Invalid azimuthPart, I am crashing now Dave.");

                    MyEntity elevationPart = null;
                    if (!Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPart))
                        return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.SubtypeName}) Invalid elevationPart, I am crashing now Dave.");

                    azimuthPart.NeedsWorldMatrix = true;
                    elevationPart.NeedsWorldMatrix = true;

                    Weapons.Add(new Weapon(muzzlePartEntity, system, i, (Weapon.WeaponComponent)comp, Parts, elevationPart, azimuthPart, azimuthPartName, elevationPartName));
                }
                else if (Comp.Type == CoreComponent.CompType.Upgrade)
                {
                    Log.Line($"Upgrade");

                    if (Weapons.Count > 0 || Support.Count > 0 || Phantoms.Count > 0)
                        return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.SubtypeName}) mixed functions, cannot mix weapons/upgrades/armorSupport/phantoms, I am crashing now Dave.");

                    CoreSystem coreSystem;
                    if (Structure.PartSystems.TryGetValue(Structure.PartHashes[i], out coreSystem) || !(coreSystem is UpgradeSystem))
                    {
                        Upgrades.Add(new Upgrades((UpgradeSystem)coreSystem, (Upgrade.UpgradeComponent)comp, i));
                    }
                }
                else if (Comp.Type == CoreComponent.CompType.Support)
                {
                    Log.Line($"Support");

                    if (Weapons.Count > 0 || Upgrades.Count > 0 || Phantoms.Count > 0)
                        return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.SubtypeName}) mixed functions, cannot mix weapons/upgrades/armorSupport/phantoms, I am crashing now Dave.");

                    CoreSystem coreSystem;
                    if (Structure.PartSystems.TryGetValue(Structure.PartHashes[i], out coreSystem) || !(coreSystem is SupportSystem))
                    {
                        Support.Add(new SupportSys((SupportSystem)coreSystem, (SupportSys.SupportComponent)comp, i));
                    }
                }
                else if (Comp.Type == CoreComponent.CompType.Phantom)
                {
                    Log.Line($"Phantom");

                    if (Weapons.Count > 0 || Upgrades.Count > 0 || Support.Count > 0)
                        return PlatformCrash(comp, true, true, $"Your block subTypeId ({comp.SubtypeName}) mixed functions, cannot mix weapons/upgrades/armorSupport/phantoms, I am crashing now Dave.");

                    CoreSystem coreSystem;
                    if (Structure.PartSystems.TryGetValue(Structure.PartHashes[i], out coreSystem) || !(coreSystem is PhantomSystem))
                    {
                        Phantoms.Add(new Phantoms((PhantomSystem)coreSystem, (Phantom.PhantomComponent)comp, i));
                    }
                }
            }

            if (Comp.Type == CoreComponent.CompType.Weapon)
                CompileTurret((Weapon.WeaponComponent)comp);
            
            _orderToCreate.Clear();
            State = PlatformState.Inited;

            return State;
        }

        private void CompileTurret(Weapon.WeaponComponent comp, bool reset = false)
        {
            var c = 0;
            foreach (var pair in Structure.PartSystems)
            {
                var weaponSystem = pair.Value as WeaponSystem;

                if (weaponSystem != null)
                {
                    MyEntity muzzlePart = null;
                    var mPartName = weaponSystem.MuzzlePartName.String;
                    var v = pair.Value;

                    if (Parts.NameToEntity.TryGetValue(mPartName, out muzzlePart) || weaponSystem.DesignatorWeapon)
                    {
                        var azimuthPartName = comp.TypeSpecific == VanillaTurret ? string.IsNullOrEmpty(weaponSystem.AzimuthPartName.String) ? "MissileTurretBase1" : weaponSystem.AzimuthPartName.String : weaponSystem.AzimuthPartName.String;
                        var elevationPartName = comp.TypeSpecific == VanillaTurret ? string.IsNullOrEmpty(weaponSystem.ElevationPartName.String) ? "MissileTurretBarrels" : weaponSystem.ElevationPartName.String : weaponSystem.ElevationPartName.String;
                        var weapon = Weapons[c];

                        if (weaponSystem.DesignatorWeapon)
                        {
                            muzzlePart = weapon.ElevationPart.Entity;
                            mPartName = elevationPartName;
                        }

                        weapon.MuzzlePart.Entity = muzzlePart;
                        weapon.HeatingParts = new List<MyEntity> { weapon.MuzzlePart.Entity };

                        if (mPartName != "None")
                        {
                            var muzzlePartLocation = comp.Session.GetPartLocation("subpart_" + mPartName, muzzlePart.Parent.Model);

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

                                var fullStepAzRotation = azPartPosTo * MatrixD.CreateFromAxisAngle(partDummy.Matrix.Up, -weaponSystem.AzStep) * azPrtPosFrom;

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
                                if (partDummy == null)
                                {
                                    PlatformCrash(comp, true, true, $"partDummy null: name:{elevationPartName} - azimuthPartParentNull:{elevationPart.Parent == null}, I am crashing now Dave.");
                                    return;
                                }
                                var elPartPosTo = MatrixD.CreateTranslation(-elevationPartLocation);
                                var elPartPosFrom = MatrixD.CreateTranslation(elevationPartLocation);

                                var fullStepElRotation = elPartPosTo * MatrixD.CreateFromAxisAngle(partDummy.Matrix.Left, weaponSystem.ElStep) * elPartPosFrom;

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

                        var barrelCount = weaponSystem.Barrels.Length;

                        if (mPartName != "Designator")
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
                            var barrel = weaponSystem.Barrels[i];

                            weapon.MuzzleIdToName.Add(i, barrel);
                            if (weapon.Muzzles[i] == null)
                            {
                                weapon.Dummies[i] = new Dummy(weapon.MuzzlePart.Entity, weapon, barrel);
                                weapon.Muzzles[i] = new Weapon.Muzzle(weapon, i, comp.Session);
                            }
                            else
                                weapon.Dummies[i].Entity = weapon.MuzzlePart.Entity;
                        }

                        for (int i = 0; i < v.HeatingSubparts.Length; i++)
                        {
                            var partName = v.HeatingSubparts[i];
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
                            foreach (var emissive in weapon.System.PartEmissiveSet)
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
        }

        internal void ResetTurret(Weapon.WeaponComponent comp)
        {
            var c = 0;
            var registered = false;
            foreach (var pair in Structure.PartSystems)
            {
                var weaponSystem = pair.Value as WeaponSystem;
                if (weaponSystem != null)
                {
                    MyEntity muzzlePart = null;
                    var mPartName = weaponSystem.MuzzlePartName.String;
                    var v = pair.Value;

                    if (Parts.NameToEntity.TryGetValue(mPartName, out muzzlePart) || weaponSystem.DesignatorWeapon)
                    {
                        if (!registered)
                        {
                            Parts.NameToEntity.FirstPair().Value.OnClose += Comp.SubpartClosed;
                            registered = true;
                        }

                        var azimuthPartName = comp.TypeSpecific == VanillaTurret ? string.IsNullOrEmpty(weaponSystem.AzimuthPartName.String) ? "MissileTurretBase1" : weaponSystem.AzimuthPartName.String : weaponSystem.AzimuthPartName.String;
                        var elevationPartName = comp.TypeSpecific == VanillaTurret ? string.IsNullOrEmpty(weaponSystem.ElevationPartName.String) ? "MissileTurretBarrels" : weaponSystem.ElevationPartName.String : weaponSystem.ElevationPartName.String;
                        var weapon = Weapons[c];
                        MyEntity azimuthPartEntity;
                        if (Parts.NameToEntity.TryGetValue(azimuthPartName, out azimuthPartEntity))
                        {
                            weapon.AzimuthPart.Entity = azimuthPartEntity;
                            weapon.AzimuthPart.Parent = azimuthPartEntity.Parent;
                            weapon.AzimuthPart.Entity.NeedsWorldMatrix = true;
                        }

                        MyEntity elevationPartEntity;
                        if (Parts.NameToEntity.TryGetValue(elevationPartName, out elevationPartEntity))
                        {
                            weapon.ElevationPart.Entity = elevationPartEntity;
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

                        if (weaponSystem.DesignatorWeapon)
                            muzzlePart = weapon.ElevationPart.Entity;

                        weapon.MuzzlePart.Entity = muzzlePart;

                        weapon.HeatingParts.Clear();
                        weapon.HeatingParts.Add(weapon.MuzzlePart.Entity);

                        foreach (var animationSet in weapon.AnimationsSet)
                        {
                            for (int i = 0; i < animationSet.Value.Length; i++)
                            {
                                var animation = animationSet.Value[i];
                                MyEntity part;
                                if (Parts.NameToEntity.TryGetValue(animation.SubpartId, out part))
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

                        if (mPartName != "Designator")
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

                        for (int i = 0; i < weaponSystem.Barrels.Length; i++)
                            weapon.Dummies[i].Entity = weapon.MuzzlePart.Entity;


                        for (int i = 0; i < v.HeatingSubparts.Length; i++)
                        {
                            var partName = v.HeatingSubparts[i];
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
                            foreach (var emissive in weapon.System.PartEmissiveSet)
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
        }

        internal void ResetParts(Weapon.WeaponComponent comp)
        {
            Parts.Clean(comp.Entity as MyEntity);
            Parts.CheckSubparts();
            
            ResetTurret(comp);

            comp.Status = Started;
        }

        internal void RemoveParts(CoreComponent comp)
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
            w.BaseComp.HasStrengthSlider = w.BaseComp.HasStrengthSlider || (!w.CanUseChargeAmmo && ui.DamageModifier && w.CanUseEnergyAmmo || w.CanUseHybridAmmo);
            w.Comp.HasRofSlider = w.Comp.HasRofSlider || (ui.RateOfFire && !w.CanUseChargeAmmo);
            w.BaseComp.CanOverload = w.BaseComp.CanOverload || (ui.EnableOverload && w.CanUseBeams && !w.CanUseChargeAmmo);
            w.BaseComp.HasTurret = w.BaseComp.HasTurret || (w.System.Values.HardPoint.Ai.TurretAttached);
            w.Comp.HasTracking = w.Comp.HasTracking || w.System.Values.HardPoint.Ai.TrackTargets;
            w.Comp.HasChargeWeapon = w.Comp.HasChargeWeapon || w.CanUseChargeAmmo;
            w.Comp.ShootSubmerged = w.Comp.ShootSubmerged || w.System.Values.HardPoint.CanShootSubmerged;
            if (ui.DamageModifier || ui.EnableOverload || ui.RateOfFire || ui.ToggleGuidance)
                w.BaseComp.UiEnabled = true;

            if (w.System.HasAmmoSelection)
                w.BaseComp.ConsumableSelectionPartIds.Add(w.PartId);

            foreach (var m in w.System.Values.Assignments.MountPoints) {
                if (m.SubtypeId == Comp.SubTypeId.String && !string.IsNullOrEmpty(m.IconName)) {
                    Comp.CustomIcon = m.IconName;
                }
            }
        }
        
        internal PlatformState PlatformCrash(CoreComponent comp, bool markInvalid, bool suppress, string message)
        {
            if (suppress)
                comp.Session.SuppressWc = true;
            
            if (markInvalid)
                State = PlatformState.Invalid;
            
            if (Comp.Session.HandlesInput) {
                if (suppress)
                    MyAPIGateway.Utilities.ShowNotification($"WeaponCore hard crashed during block init, shutting down\n Send log files to server admin or submit a bug report to mod author:\n {comp.Platform?.Structure?.ModPath} - {comp.SubtypeName}", 10000);
            }
            Log.Line($"PlatformCrash: {Comp.SubtypeName} - {message}");

            return State;
        }
    }
}
