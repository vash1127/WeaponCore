using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Engine.Analytics;
using Sandbox.Game;
using Sandbox.Game.Entities;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Support.PartAnimation;

namespace WeaponCore.Support
{
    public class WeaponSystem
    {
        private const string Arc = "Arc";

        public readonly MyStringHash MuzzlePartName;
        public readonly MyStringHash AzimuthPartName;
        public readonly MyStringHash ElevationPartName;
        public readonly WeaponDefinition Values;
        public readonly MyDefinitionId AmmoDefId;
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly MyStringId TracerMaterial;
        public readonly MyStringId TrailMaterial;
        public readonly Session Session;
        public readonly MyConcurrentPool<MyEntity> PrimeEntityPool;
        public readonly Dictionary<MyDefinitionBase, float> CustomBlockDefinitionBasesToScales;
        public readonly Dictionary<Weapon.EventTriggers, PartAnimation[]> WeaponAnimationSet;
        public readonly Dictionary<Weapon.EventTriggers, uint> WeaponAnimationLengths;
        public readonly HashSet<string> AnimationIdLookup;
        public readonly Dictionary<string, EmissiveState> WeaponEmissiveSet;
        public readonly Dictionary<string, Matrix[]> WeaponLinearMoveSet;
        public readonly MyPhysicalInventoryItem AmmoItem;
        public readonly AreaDamage.AreaEffectType AreaEffect;
        public readonly string WeaponName;
        public readonly string ModelPath;
        public readonly string[] Barrels;
        public readonly int ReloadTime;
        public readonly int DelayToFire;
        public readonly int TimeToCeaseFire;
        public readonly int MaxObjectsHit;
        public readonly int TargetLossTime;
        public readonly int MinAzimuth;
        public readonly int MaxAzimuth;
        public readonly int MinElevation;
        public readonly int MaxElevation;
        public readonly int MaxHeat;
        public readonly int WeaponId;
        public readonly int BarrelsPerShot;
        public readonly int HeatPerShot;
        public readonly int RateOfFire;
        public readonly int BarrelSpinRate;
        public readonly int MaxTargets;
        public readonly int PulseInterval;
        public readonly int PulseChance;
        public readonly int EnergyMagSize;
        public readonly TurretType TurretMovement;
        public readonly bool PrimeModel;
        public readonly bool TriggerModel;
        public readonly bool HasBarrelRate;
        public readonly bool ElevationOnly;
        public readonly bool LimitedAxisTurret;
        public readonly bool BurstMode;
        public readonly bool AmmoParticle;
        public readonly bool HitParticle;
        public readonly bool BarrelAxisRotation;
        public readonly bool AmmoAreaEffect;
        public readonly bool AmmoSkipAccel;
        public readonly bool LineWidthVariance;
        public readonly bool LineColorVariance;
        public readonly bool EnergyAmmo;
        public readonly bool MustCharge;
        public readonly bool IsHybrid;
        public readonly bool BarrelEffect1;
        public readonly bool BarrelEffect2;
        public readonly bool HasBarrelShootAv;
        public readonly bool HasBackKickForce;
        public readonly bool SpeedVariance;
        public readonly bool RangeVariance;
        public readonly bool VirtualBeams;
        public readonly bool IsBeamWeapon;
        public readonly bool ConvergeBeams;
        public readonly bool RotateRealBeam;
        public readonly bool OneHitParticle;
        public readonly bool DamageScaling;
        public readonly bool ArmorScaling;
        public readonly bool CustomDamageScales;
        public readonly bool TargetOffSet;
        public readonly bool TargetSubSystems;
        public readonly bool OnlySubSystems;
        public readonly bool ClosestFirst;
        public readonly bool DegRof;
        public readonly bool TrackProjectile;
        public readonly bool TrackOther;
        public readonly bool TrackGrids;
        public readonly bool TrackCharacters;
        public readonly bool TrackMeteors;
        public readonly bool TrackNeutrals;
        public readonly bool CollisionIsLine;
        public readonly bool SelfDamage;
        public readonly bool VoxelDamage;
        public readonly bool OffsetEffect;
        public readonly bool Trail;
        public readonly bool IsMine;
        public readonly bool IsField;
        public readonly bool DesignatorWeapon;
        public readonly bool AmmoParticleShrinks;
        public readonly bool HitParticleShrinks;
        public readonly bool DrawLine;
        public readonly bool Ewar;
        public readonly bool EwarEffect;
        public readonly bool NeedsPrediction;
        public readonly double CollisionSize;
        public readonly double MaxTrajectory;
        public readonly double MaxTrajectorySqr;
        public readonly double AreaRadiusSmall;
        public readonly double AreaRadiusLarge;
        public readonly double AreaEffectSize;
        public readonly double DetonateRadiusSmall;
        public readonly double DetonateRadiusLarge;
        public readonly double MaxTargetSpeed;
        public readonly double ShieldModifier;
        public readonly double TracerLength;
        public readonly double AzStep;
        public readonly double ElStep;
        public readonly float DesiredProjectileSpeed;
        public readonly double SmartsDelayDistSqr;
        public readonly float TargetLossDegree;
        public readonly float Barrel1AvTicks;
        public readonly float Barrel2AvTicks;
        public readonly float WepCoolDown;
        public readonly float BaseDamage;
        public readonly float AreaEffectDamage;
        public readonly float DetonationDamage;
        public readonly float MinTargetRadius;
        public readonly float MaxTargetRadius;
        public readonly float MaxAmmoVolume;
        public readonly float TrailWidth;
        public readonly HardPointDefinition.Prediction Prediction;
        public float FiringSoundDistSqr;
        public float ReloadSoundDistSqr;
        public float BarrelSoundDistSqr;
        public float HardPointSoundDistSqr;
        public float NoAmmoSoundDistSqr;
        public float HitSoundDistSqr;
        public float AmmoTravelSoundDistSqr;
        public float HardPointAvMaxDistSqr;
        public float AmmoSoundMaxDistSqr;
        public FiringSoundState FiringSound;
        public bool HitSound;
        public bool WeaponReloadSound;
        public bool NoAmmoSound;
        public bool HardPointRotationSound;
        public bool BarrelRotationSound;
        public bool AmmoTravelSound;
        public bool AnimationsInited;

        public enum FiringSoundState
        {
            None,
            PerShot,
            WhenDone
        }

        public enum TurretType
        {
            Full,
            AzimuthOnly,
            ElevationOnly,
            Fixed //not used yet
        }

        public WeaponSystem(Session session, MyStringHash muzzlePartName, MyStringHash azimuthPartName, MyStringHash elevationPartName, WeaponDefinition values, string weaponName, MyDefinitionId ammoDefId, int weaponId)
        {
            Session = session;
            MuzzlePartName = muzzlePartName;
            DesignatorWeapon = muzzlePartName.String == "Designator";
            AzimuthPartName = azimuthPartName;
            ElevationPartName = elevationPartName;

            Values = values;
            Barrels = values.Assignments.Barrels;
            WeaponId = weaponId;
            WeaponName = weaponName;
            AmmoDefId = ammoDefId;
            
            MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(AmmoDefId);
            TracerMaterial = MyStringId.GetOrCompute(values.Graphics.Line.TracerMaterial);
            TrailMaterial = MyStringId.GetOrCompute(values.Graphics.Line.Trail.Material);

            if (ammoDefId.SubtypeName != "Blank") AmmoItem = new MyPhysicalInventoryItem() { Amount = 1, Content = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_AmmoMagazine>(AmmoDefId.SubtypeName) };

            IsMine = Values.Ammo.Trajectory.Guidance == AmmoTrajectory.GuidanceType.DetectFixed || Values.Ammo.Trajectory.Guidance == AmmoTrajectory.GuidanceType.DetectSmart || Values.Ammo.Trajectory.Guidance == AmmoTrajectory.GuidanceType.DetectTravelTo;
            IsField = Values.Ammo.Trajectory.FieldTime > 0;

            TurretMovements(out AzStep, out ElStep, out MinAzimuth, out MaxAzimuth, out MinElevation, out MaxElevation, out TurretMovement, out ElevationOnly, out LimitedAxisTurret);

            MaxAmmoVolume = Values.HardPoint.Block.InventorySize;
            AmmoParticle = values.Graphics.Particles.Ammo.Name != string.Empty;
            AmmoParticleShrinks = values.Graphics.Particles.Ammo.ShrinkByDistance;
            HitParticleShrinks = values.Graphics.Particles.Hit.ShrinkByDistance;
            
            BarrelsAv(out BarrelEffect1, out BarrelEffect2, out Barrel1AvTicks, out Barrel2AvTicks, out BarrelAxisRotation);

            HitParticle = values.Graphics.Particles.Hit.Name != string.Empty;

            DrawLine = Values.Graphics.Line.Tracer.Enable;
            LineColorVariance = values.Graphics.Line.ColorVariance.Start > 0 && values.Graphics.Line.ColorVariance.End > 0;
            LineWidthVariance = values.Graphics.Line.WidthVariance.Start > 0 || values.Graphics.Line.WidthVariance.End > 0;
            SpeedVariance = values.Ammo.Trajectory.SpeedVariance.Start > 0 || values.Ammo.Trajectory.SpeedVariance.End > 0;
            RangeVariance = values.Ammo.Trajectory.RangeVariance.Start > 0 || values.Ammo.Trajectory.RangeVariance.End > 0;
            TrailWidth = values.Graphics.Line.Trail.CustomWidth > 0 ? values.Graphics.Line.Trail.CustomWidth : values.Graphics.Line.Tracer.Width;

            TargetOffSet = values.Ammo.Trajectory.Smarts.Inaccuracy > 0;
            TimeToCeaseFire = values.HardPoint.DelayCeaseFire;
            ReloadTime = values.HardPoint.Loading.ReloadTime;
            DelayToFire = values.HardPoint.Loading.DelayUntilFire;
            TargetLossTime = values.Ammo.Trajectory.TargetLossTime > 0 ? values.Ammo.Trajectory.TargetLossTime : int.MaxValue;
            MaxObjectsHit = values.Ammo.ObjectsHit.MaxObjectsHit > 0 ? values.Ammo.ObjectsHit.MaxObjectsHit : int.MaxValue;
            BaseDamage = values.Ammo.BaseDamage;
            MaxTargets = Values.Ammo.Trajectory.Smarts.MaxTargets;
            TargetLossDegree = Values.Ammo.Trajectory.TargetLossDegree > 0 ? (float)Math.Cos(MathHelper.ToRadians(Values.Ammo.Trajectory.TargetLossDegree)) : 0;

            Fields(out PulseInterval, out PulseChance);
            Heat(out DegRof, out MaxHeat, out WepCoolDown, out HeatPerShot);
            BarrelValues(out BarrelsPerShot, out BarrelSpinRate, out HasBarrelRate, out RateOfFire);
            AreaEffects(out AreaEffect, out AreaEffectDamage, out AreaEffectSize, out DetonationDamage, out AmmoAreaEffect, out AreaRadiusSmall, out AreaRadiusLarge, out DetonateRadiusSmall, out DetonateRadiusLarge, out Ewar, out EwarEffect);
            Energy(out EnergyAmmo, out MustCharge, out EnergyMagSize, out BurstMode, out IsHybrid);

            ShieldModifier = Values.DamageScales.Shields.Modifier > 0 ? Values.DamageScales.Shields.Modifier : 1;
            AmmoSkipAccel = values.Ammo.Trajectory.AccelPerSec <= 0;
            

            MaxTrajectory = values.Ammo.Trajectory.MaxTrajectory;
            MaxTrajectorySqr = MaxTrajectory * MaxTrajectory;
            HasBackKickForce = values.Ammo.BackKickForce > 0;
            MaxTargetSpeed = values.Targeting.StopTrackingSpeed > 0 ? values.Targeting.StopTrackingSpeed : double.MaxValue;
            ClosestFirst = values.Targeting.ClosestFirst;
            Sound();

            DamageScales(out DamageScaling, out ArmorScaling, out CustomDamageScales, out CustomBlockDefinitionBasesToScales, out SelfDamage, out VoxelDamage);
            Beams(out IsBeamWeapon, out VirtualBeams, out RotateRealBeam, out ConvergeBeams, out OneHitParticle, out OffsetEffect);
            CollisionShape(out CollisionIsLine, out CollisionSize, out TracerLength);
            SmartsDelayDistSqr = (CollisionSize * Values.Ammo.Trajectory.Smarts.TrackingDelay) * (CollisionSize * Values.Ammo.Trajectory.Smarts.TrackingDelay);
            PrimeEntityPool = Models(out PrimeModel, out TriggerModel, out ModelPath);
            Track(out TrackProjectile, out TrackGrids, out TrackCharacters, out TrackMeteors, out TrackNeutrals, out TrackOther);
            SubSystems(out TargetSubSystems, out OnlySubSystems);
            ValidTargetSize(out MinTargetRadius, out MaxTargetRadius);
            HasBarrelShootAv = BarrelEffect1 || BarrelEffect2 || HardPointRotationSound || FiringSound == FiringSoundState.WhenDone;

            DesiredProjectileSpeed = (float)(!IsBeamWeapon ? values.Ammo.Trajectory.DesiredSpeed : MaxTrajectory * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
            Predictions(out NeedsPrediction, out Prediction);

            Trail = values.Graphics.Line.Trail.Enable;

            Session.CreateAnimationSets(Values.Animations, this, out WeaponAnimationSet, out WeaponEmissiveSet, out WeaponLinearMoveSet, out AnimationIdLookup, out WeaponAnimationLengths);
        }

        private void Energy(out bool energyAmmo, out bool mustCharge, out int energyMagSize, out bool burstMode, out bool isHybrid)
        {
            energyAmmo = AmmoDefId.SubtypeId.String == "Blank";
            isHybrid = Values.HardPoint.Hybrid;
            mustCharge = (energyAmmo || isHybrid) && ReloadTime > 0;
            burstMode = Values.HardPoint.Loading.ShotsInBurst > 0 && (energyAmmo || MagazineDef.Capacity >= Values.HardPoint.Loading.ShotsInBurst);

            if (MustCharge)
            {
                var ewar = (int)Values.Ammo.AreaEffect.AreaEffect > 3;
                var shotEnergyCost = ewar ? Values.HardPoint.EnergyCost * AreaEffectDamage : Values.HardPoint.EnergyCost * BaseDamage;
                var requiredPower = (((shotEnergyCost * ((RateOfFire / MyEngineConstants.UPDATE_STEPS_PER_SECOND) * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * Values.HardPoint.Loading.BarrelsPerShot) * Values.HardPoint.Loading.TrajectilesPerBarrel);

                energyMagSize = (int)(requiredPower * (ReloadTime / MyEngineConstants.UPDATE_STEPS_PER_SECOND));
                return;
            }

            energyMagSize = 0;
        }


        private void Predictions(out bool needsPrediction, out HardPointDefinition.Prediction type)
        {
            type = Values.HardPoint.AimLeadingPrediction;
            needsPrediction = type != HardPointDefinition.Prediction.Off && !IsBeamWeapon && DesiredProjectileSpeed > 0;
        }

        private void Fields(out int pulseInterval, out int pulseChance)
        {
            pulseInterval = Values.Ammo.AreaEffect.Pulse.Interval;
            pulseChance = Values.Ammo.AreaEffect.Pulse.PulseChance;
        }

        private void Heat(out bool degRof, out int maxHeat, out float wepCoolDown, out int heatPerShot)
        {
            degRof = Values.HardPoint.Loading.DegradeRof;
            maxHeat = Values.HardPoint.Loading.MaxHeat;
            wepCoolDown = Values.HardPoint.Loading.Cooldown;
            heatPerShot = Values.HardPoint.Loading.HeatPerShot;
            if (wepCoolDown < .2f) wepCoolDown = .2f;
            if (wepCoolDown > .95f) wepCoolDown = .95f;
        }

        private void BarrelValues(out int barrelsPerShot, out int barrelSpinRate, out bool hasBarrelRate, out int rateOfFire)
        {
            barrelsPerShot = Values.HardPoint.Loading.BarrelsPerShot;
            barrelSpinRate = Values.HardPoint.Loading.BarrelSpinRate;
            hasBarrelRate = BarrelSpinRate > 0;
            rateOfFire = Values.HardPoint.Loading.RateOfFire;
        }

        private void BarrelsAv(out bool barrelEffect1, out bool barrelEffect2, out float barrel1AvTicks, out float barrel2AvTicks, out bool barrelAxisRotation)
        {
            barrelEffect1 = Values.Graphics.Particles.Barrel1.Name != string.Empty;
            barrelEffect2 = Values.Graphics.Particles.Barrel2.Name != string.Empty;
            barrel1AvTicks = Values.Graphics.Particles.Barrel1.Extras.MaxDuration;
            barrel2AvTicks = Values.Graphics.Particles.Barrel2.Extras.MaxDuration;
            barrelAxisRotation = Values.HardPoint.RotateBarrelAxis != 0;
        }

        private void AreaEffects(out AreaDamage.AreaEffectType areaEffect, out float areaEffectDamage, out double areaEffectSize, out float detonationDamage, out bool ammoAreaEffect, out double areaRadiusSmall, out double areaRadiusLarge, out double detonateRadiusSmall, out double detonateRadiusLarge, out bool eWar, out bool eWarEffect)
        {
            areaEffect = Values.Ammo.AreaEffect.AreaEffect;
            areaEffectDamage = Values.Ammo.AreaEffect.AreaEffectDamage;
            areaEffectSize = Values.Ammo.AreaEffect.AreaEffectRadius;
            detonationDamage = Values.Ammo.AreaEffect.Detonation.DetonationDamage;
            ammoAreaEffect = Values.Ammo.AreaEffect.AreaEffect != AreaDamage.AreaEffectType.Disabled;
            areaRadiusSmall = Session.ModRadius(Values.Ammo.AreaEffect.AreaEffectRadius, false);
            areaRadiusLarge = Session.ModRadius(Values.Ammo.AreaEffect.AreaEffectRadius, true);
            detonateRadiusSmall = Session.ModRadius(Values.Ammo.AreaEffect.Detonation.DetonationRadius, false);
            detonateRadiusLarge = Session.ModRadius(Values.Ammo.AreaEffect.Detonation.DetonationRadius, true);
            eWar = areaEffect > (AreaDamage.AreaEffectType)2;
            eWarEffect = areaEffect > (AreaDamage.AreaEffectType)3;
        }

        private void TurretMovements(out double azStep, out double elStep, out int minAzimuth, out int maxAzimuth, out int minElevation, out int maxElevation, out TurretType turretMove, out bool elevationOnly, out bool limitedAxisTurret)
        {
            azStep = Values.HardPoint.Block.RotateRate;
            elStep = Values.HardPoint.Block.ElevateRate;
            minAzimuth = Values.HardPoint.Block.MinAzimuth;
            maxAzimuth = Values.HardPoint.Block.MaxAzimuth;
            minElevation = Values.HardPoint.Block.MinElevation;
            maxElevation = Values.HardPoint.Block.MaxElevation;
            
            elevationOnly = false;
            limitedAxisTurret = false;
            turretMove = TurretType.Full;

            if (minAzimuth == maxAzimuth)
            {
                turretMove = TurretType.ElevationOnly;
                elevationOnly = true;
                limitedAxisTurret = true;
            }
            if (minElevation == maxElevation && TurretMovement != TurretType.Full)
            {
                turretMove = TurretType.Fixed;
                limitedAxisTurret = true;
            }
            else if (minElevation == maxElevation)
            {
                turretMove = TurretType.AzimuthOnly;
                elevationOnly = false;
                limitedAxisTurret = true;
            }
        }

        private void DamageScales(out bool damageScaling, out bool armorScaling, out bool customDamageScales, out Dictionary<MyDefinitionBase, float> customBlockDef, out bool selfDamage, out bool voxelDamage)
        {
            armorScaling = false;
            customDamageScales = false;
            var d = Values.DamageScales;
            customBlockDef = null;
            if (d.Custom.Types != null && d.Custom.Types.Length > 0)
            {
                foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
                foreach (var customDef in d.Custom.Types)
                    if (customDef.Modifier >= 0 && def.Id.SubtypeId.String == customDef.SubTypeId)
                    {
                        if (customBlockDef == null) customBlockDef = new Dictionary<MyDefinitionBase, float>();
                        customBlockDef.Add(def, customDef.Modifier);
                        customDamageScales = customBlockDef.Count > 0;
                    }
            }
            damageScaling = d.MaxIntegrity > 0 || d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0 || d.Grids.Large >= 0 || d.Grids.Small >= 0 || customDamageScales;
            if (damageScaling) armorScaling = d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0;
            selfDamage = Values.DamageScales.SelfDamage && !IsBeamWeapon;
            voxelDamage = Values.DamageScales.DamageVoxels;
        }

        private void Track(out bool trackProjectile, out bool trackGrids, out bool trackCharacters, out bool trackMeteors, out bool trackNeutrals, out bool trackOther)
        {
            trackProjectile = false;
            trackGrids = false;
            trackCharacters = false;
            trackMeteors = false;
            trackNeutrals = false;
            trackOther = false;

            var threats = Values.Targeting.Threats;
            foreach (var threat in threats)
            {
                if (threat == TargetingDefinition.Threat.Projectiles)
                    trackProjectile = true;
                else if (threat == TargetingDefinition.Threat.Grids)
                {
                    trackGrids = true;
                    trackOther = true;
                }
                else if (threat == TargetingDefinition.Threat.Characters)
                {
                    trackCharacters = true;
                    trackOther = true;
                }
                else if (threat == TargetingDefinition.Threat.Meteors)
                {
                    trackMeteors = true;
                    trackOther = true;
                }
                else if (threat == TargetingDefinition.Threat.Neutrals)
                {
                    trackNeutrals = true;
                    trackOther = true;
                }
            }
        }

        private void SubSystems(out bool targetSubSystems, out bool onlySubSystems)
        {
            targetSubSystems = false;
            var anySystemDetected = false;
            if (Values.Targeting.SubSystems.Length > 0)
            {
                foreach (var system in Values.Targeting.SubSystems)
                {
                    if (system != TargetingDefinition.BlockTypes.Any) targetSubSystems = true;
                    else anySystemDetected = true;
                }
            }
            if (TargetSubSystems && anySystemDetected) onlySubSystems = false;
            else onlySubSystems = true;
        }

        private void ValidTargetSize(out float minTargetRadius, out float maxTargetRadius)
        {
            var minDiameter = Values.Targeting.MinimumDiameter;
            var maxDiameter = Values.Targeting.MaximumDiameter;

            minTargetRadius = (float)(minDiameter > 0 ? minDiameter * 0.5d : 0);
            maxTargetRadius = (float)(maxDiameter > 0 ? maxDiameter * 0.5d : float.MaxValue);
        }

        private MyConcurrentPool<MyEntity> Models(out bool primeModel, out bool triggerModel, out string primeModelPath)
        {
            if (Values.Ammo.AreaEffect.AreaEffect > (AreaDamage.AreaEffectType)3 && IsField) triggerModel = true;
            else triggerModel = false;
            primeModel = Values.Graphics.ModelName != string.Empty;
            primeModelPath = primeModel ? Values.ModPath + Values.Graphics.ModelName : string.Empty;
            return primeModel ? new MyConcurrentPool<MyEntity>(256, PrimeEntityClear, 10000, PrimeEntityActivator) : null;
        }

        private MyEntity PrimeEntityActivator()
        {
            var ent = new MyEntity();
            ent.Init(null, ModelPath, null, null, null);
            ent.Render.CastShadows = false;
            ent.IsPreview = true;
            ent.Save = false;
            ent.SyncFlag = false;
            ent.NeedsWorldMatrix = false;
            ent.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(ent, false);

            //ent.PositionComp.SetWorldMatrix(MatrixD.Identity, null, false, false, false);
            //ent.InScene = false;
            //ent.Render.RemoveRenderObjects();
            return ent;
        }

        private static void PrimeEntityClear(MyEntity myEntity)
        {
            myEntity.PositionComp.SetWorldMatrix(MatrixD.Identity, null, false, false, false);
            myEntity.InScene = false;
            myEntity.Render.RemoveRenderObjects();
        }


        private void Beams(out bool isBeamWeapon, out bool virtualBeams, out bool rotateRealBeam, out bool convergeBeams, out bool oneHitParticle, out bool offsetEffect)
        {
            isBeamWeapon = Values.Ammo.Beams.Enable;
            virtualBeams = Values.Ammo.Beams.VirtualBeams && IsBeamWeapon;
            rotateRealBeam = Values.Ammo.Beams.RotateRealBeam && VirtualBeams;
            convergeBeams = !RotateRealBeam && Values.Ammo.Beams.ConvergeBeams && VirtualBeams;
            oneHitParticle = Values.Ammo.Beams.OneParticle && IsBeamWeapon;
            offsetEffect = Values.Graphics.Line.OffsetEffect.MaxOffset > 0;
        }

        private void CollisionShape(out bool collisionIsLine, out double collisionSize, out double tracerLength)
        {
            var isLine = Values.Ammo.Shape.Shape == ShapeDefinition.Shapes.Line;
            var size = Values.Ammo.Shape.Diameter;
            
            if (IsBeamWeapon)
                tracerLength = MaxTrajectory;
            else tracerLength = Values.Graphics.Line.Tracer.Length > 0 ? Values.Graphics.Line.Tracer.Length : 0.1;

            if (size <= 0)
            {
                if (!isLine) isLine = true;
                size = 1;
            }
            else if (!isLine) size = size * 0.5;

            collisionIsLine = isLine;
            collisionSize = size;
        }

        private void Sound()
        {
            HitSound = Values.Audio.Ammo.HitSound != string.Empty;
            AmmoTravelSound = Values.Audio.Ammo.TravelSound != string.Empty;
            WeaponReloadSound = Values.Audio.HardPoint.ReloadSound != string.Empty;
            HardPointRotationSound = Values.Audio.HardPoint.HardPointRotationSound != string.Empty;
            BarrelRotationSound = Values.Audio.HardPoint.BarrelRotationSound != string.Empty;
            NoAmmoSound = Values.Audio.HardPoint.NoAmmoSound != string.Empty;
            var fSoundStart = Values.Audio.HardPoint.FiringSound;
            if (fSoundStart != string.Empty && Values.Audio.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.PerShot;
            else if (fSoundStart != string.Empty && !Values.Audio.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.WhenDone;
            else FiringSound = FiringSoundState.None;


            var fireSound = string.Concat(Arc, Values.Audio.HardPoint.FiringSound);
            var hitSound = string.Concat(Arc, Values.Audio.Ammo.HitSound);
            var travelSound = string.Concat(Arc, Values.Audio.Ammo.TravelSound);
            var reloadSound = string.Concat(Arc, Values.Audio.HardPoint.ReloadSound);
            var barrelSound = string.Concat(Arc, Values.Audio.HardPoint.BarrelRotationSound);
            var hardPointSound = string.Concat(Arc, Values.Audio.HardPoint.HardPointRotationSound);
            var noAmmoSound = string.Concat(Arc, Values.Audio.HardPoint.NoAmmoSound);

            if (Values.Graphics.Particles.Barrel1.Extras.MaxDistance > HardPointAvMaxDistSqr)
                HardPointAvMaxDistSqr = Values.Graphics.Particles.Barrel1.Extras.MaxDistance;

            if (Values.Graphics.Particles.Barrel2.Extras.MaxDistance > HardPointAvMaxDistSqr)
                HardPointAvMaxDistSqr = Values.Graphics.Particles.Barrel2.Extras.MaxDistance;

            foreach (var def in Session.SoundDefinitions)
            {
                var id = def.Id.SubtypeId.String;
                if (FiringSound != FiringSoundState.None && id == fireSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) FiringSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (FiringSoundDistSqr > HardPointAvMaxDistSqr) HardPointAvMaxDistSqr = FiringSoundDistSqr;
                }
                if (HitSound && id == hitSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) HitSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (HitSoundDistSqr > AmmoSoundMaxDistSqr) AmmoSoundMaxDistSqr = HitSoundDistSqr;
                }
                else if (AmmoTravelSound && id == travelSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) AmmoTravelSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (AmmoTravelSoundDistSqr > AmmoSoundMaxDistSqr) AmmoSoundMaxDistSqr = AmmoTravelSoundDistSqr;
                }
                else if (WeaponReloadSound && id == reloadSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) ReloadSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (ReloadSoundDistSqr > HardPointAvMaxDistSqr) HardPointAvMaxDistSqr = ReloadSoundDistSqr;

                }
                else if (BarrelRotationSound && id == barrelSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) BarrelSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (BarrelSoundDistSqr > HardPointAvMaxDistSqr) HardPointAvMaxDistSqr = BarrelSoundDistSqr;
                }
                else if (HardPointRotationSound && id == hardPointSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) HardPointSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (HardPointSoundDistSqr > HardPointAvMaxDistSqr) HardPointAvMaxDistSqr = HardPointSoundDistSqr;
                }
                else if (NoAmmoSound && id == noAmmoSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) NoAmmoSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (NoAmmoSoundDistSqr > HardPointAvMaxDistSqr) HardPointAvMaxDistSqr = NoAmmoSoundDistSqr;
                }
            }
            if (FiringSoundDistSqr <= 0) FiringSoundDistSqr = Values.Ammo.Trajectory.MaxTrajectory * Values.Ammo.Trajectory.MaxTrajectory;
        }
    }

    public class WeaponStructure
    {
        public readonly Dictionary<MyStringHash, WeaponSystem> WeaponSystems;
        public readonly Dictionary<MyDefinitionId, List<int>> AmmoToWeaponIds;
        public readonly Dictionary<int, int> HashToId;

        public readonly MyStringHash[] MuzzlePartNames;
        public readonly bool MultiParts;
        public readonly int GridWeaponCap;
        public readonly Session Session;

        public WeaponStructure(Session session, KeyValuePair<string, Dictionary<string, MyTuple<string, string, string>>> tDef, List<WeaponDefinition> wDefList)
        {
            Session = session;
            var map = tDef.Value;
            var numOfParts = wDefList.Count;
            MultiParts = numOfParts > 1;
            var muzzlePartNames = new MyStringHash[numOfParts];
            var azimuthPartNames = new MyStringHash[numOfParts];
            var elevationPartNames = new MyStringHash[numOfParts];
            var mapIndex = 0;
            WeaponSystems = new Dictionary<MyStringHash, WeaponSystem>(MyStringHash.Comparer);
            AmmoToWeaponIds = new Dictionary<MyDefinitionId, List<int>>(MyDefinitionId.Comparer);
            HashToId = new Dictionary<int, int>();

            var gridWeaponCap = 0;
            foreach (var w in map)
            {
                var myMuzzleNameHash = MyStringHash.GetOrCompute(w.Key);
                var myAzimuthNameHash = MyStringHash.GetOrCompute(w.Value.Item2);
                var myElevationNameHash = MyStringHash.GetOrCompute(w.Value.Item3);

                muzzlePartNames[mapIndex] = myMuzzleNameHash;

                var typeName = w.Value.Item1;
                var weaponDef = new WeaponDefinition();

                foreach (var weapon in wDefList)
                    if (weapon.HardPoint.WeaponId == typeName) weaponDef = weapon;

                var ammoDefId = new MyDefinitionId();
                var ammoBlank = weaponDef.HardPoint.AmmoMagazineId == string.Empty || weaponDef.HardPoint.AmmoMagazineId == "Blank";
                foreach (var def in Session.AllDefinitions)
                {
                    if (ammoBlank && def.Id.SubtypeId.String == "Blank" || def.Id.SubtypeId.String == weaponDef.HardPoint.AmmoMagazineId) ammoDefId = def.Id;
                }

                var cap = weaponDef.HardPoint.GridWeaponCap;
                if (gridWeaponCap == 0 && cap > 0) gridWeaponCap = cap;
                else if (cap > 0 && gridWeaponCap > 0 && cap < gridWeaponCap) gridWeaponCap = cap;

                weaponDef.HardPoint.DeviateShotAngle = MathHelper.ToRadians(weaponDef.HardPoint.DeviateShotAngle);

                Session.AmmoInventoriesMaster[ammoDefId] = new ConcurrentDictionary<MyInventory, MyFixedPoint>();

                var weaponId = (tDef.Key + myElevationNameHash + myMuzzleNameHash + myAzimuthNameHash).GetHashCode();
                HashToId.Add(weaponId, mapIndex);
                WeaponSystems.Add(myMuzzleNameHash, new WeaponSystem(Session, myMuzzleNameHash, myAzimuthNameHash, myElevationNameHash, weaponDef, typeName, ammoDefId, weaponId));
                if (!ammoBlank)
                {
                    if (!AmmoToWeaponIds.ContainsKey(ammoDefId)) AmmoToWeaponIds[ammoDefId] = new List<int>();
                    AmmoToWeaponIds[ammoDefId].Add(mapIndex);
                }

                mapIndex++;
                /*
                  if (weaponDef.AmmoDef.RealisticDamage)
                  {
                      weaponDef.HasKineticEffect = weaponDef.AmmoDef.Mass > 0 && weaponDef.AmmoDef.DesiredSpeed > 0;
                      weaponDef.HasThermalEffect = weaponDef.AmmoDef.ThermalDamage > 0;
                      var kinetic = ((weaponDef.AmmoDef.Mass / 2) * (weaponDef.AmmoDef.DesiredSpeed * weaponDef.AmmoDef.DesiredSpeed) / 1000) * weaponDef.KeenScaler;
                      weaponDef.ComputedBaseDamage = kinetic + weaponDef.AmmoDef.ThermalDamage;
                  }
                  */
            }

            GridWeaponCap = gridWeaponCap;
            MuzzlePartNames = muzzlePartNames;
        }
    }
}
