﻿using System;
using System.Collections;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.AreaDamageDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.ShapeDef.Shapes;
namespace CoreSystems.Support
{
    public class AmmoConstants
    {
        public enum Texture
        {
            Normal,
            Cycle,
            Chaos,
            Resize,
            Wave,
        }

        private const string Arc = "Arc";

        public readonly MyConcurrentPool<MyEntity> PrimeEntityPool;
        public readonly Dictionary<MyDefinitionBase, float> CustomBlockDefinitionBasesToScales;
        public readonly Stack<MySoundPair> HitDefaultSoundPairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> HitShieldSoundPairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> HitVoxelSoundPairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> HitPlayerSoundPairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> HitFloatingSoundPairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> TravelSoundPairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> CustomSoundPairs = new Stack<MySoundPair>();
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly AmmoDef[] AmmoPattern;
        public readonly MyStringId[] TracerTextures;
        public readonly MyStringId[] TrailTextures;
        public readonly MyStringId[] SegmentTextures;
        public readonly MyPhysicalInventoryItem AmmoItem;
        public readonly MyPhysicalInventoryItem EjectItem;
        public readonly AreaEffectType AreaEffect;
        public readonly Texture TracerMode;
        public readonly Texture TrailMode;
        public readonly string ModelPath;

        public readonly int MaxObjectsHit;
        public readonly int TargetLossTime;
        public readonly int MaxLifeTime;
        public readonly int MinArmingTime;
        public readonly int MaxTargets;
        public readonly int PulseInterval;
        public readonly int PulseChance;
        public readonly int PulseGrowTime;
        public readonly int EnergyMagSize;
        public readonly int ShrapnelId = -1;
        public readonly int MaxChaseTime;
        public readonly int MagazineSize;
        public readonly int PatternIndexCnt;
        public readonly int AmmoIdxPos;

        public readonly bool HasEjectEffect;
        public readonly bool Pulse;
        public readonly bool PrimeModel;
        public readonly bool TriggerModel;
        public readonly bool CollisionIsLine;
        public readonly bool SelfDamage;
        public readonly bool VoxelDamage;
        public readonly bool OffsetEffect;
        public readonly bool Trail;
        public readonly bool IsMine;
        public readonly bool IsField;
        public readonly bool AmmoParticle;
        public readonly bool HitParticle;
        public readonly bool FieldParticle;
        public readonly bool AmmoAreaEffect;
        public readonly bool AmmoSkipAccel;
        public readonly bool LineWidthVariance;
        public readonly bool LineColorVariance;
        public readonly bool SegmentWidthVariance;
        public readonly bool SegmentColorVariance;
        public readonly bool OneHitParticle;
        public readonly bool DamageScaling;
        public readonly bool ArmorScaling;
        public readonly bool FallOffScaling;
        public readonly bool CustomDamageScales;
        public readonly bool SpeedVariance;
        public readonly bool RangeVariance;
        public readonly bool VirtualBeams;
        public readonly bool IsBeamWeapon;
        public readonly bool ConvergeBeams;
        public readonly bool RotateRealBeam;
        public readonly bool AmmoParticleShrinks;
        public readonly bool FieldParticleShrinks;
        public readonly bool HitParticleShrinks;
        public readonly bool DrawLine;
        public readonly bool Ewar;
        public readonly bool EwarEffect;
        public readonly bool TargetOffSet;
        public readonly bool HasBackKickForce;
        public readonly bool BurstMode;
        public readonly bool EnergyAmmo;
        public readonly bool Reloadable;
        public readonly bool MustCharge;
        public readonly bool HasShotReloadDelay;
        public readonly bool HitSound;
        public readonly bool AltHitSounds;
        public readonly bool AmmoTravelSound;
        public readonly bool IsHybrid;
        public readonly bool IsTurretSelectable;
        public readonly bool CanZombie;
        public readonly bool FeelsGravity;
        public readonly bool MaxTrajectoryGrows;
        public readonly bool HasShotFade;
        public readonly bool CustomExplosionSound;
        public readonly bool GuidedAmmoDetected;
        public readonly bool AlwaysDraw;
        public readonly bool FixedFireAmmo;
        public readonly bool ClientPredictedAmmo;
        public readonly bool IsCriticalReaction;
        public readonly float ChargSize;
        public readonly float RealShotsPerMin;
        public readonly float TargetLossDegree;
        public readonly float TrailWidth;
        public readonly float ShieldDamageBypassMod;
        public readonly float MagMass;
        public readonly float MagVolume;
        public readonly float BaseDamage;
        public readonly float AreaEffectDamage;
        public readonly float DetonationDamage;
        public readonly float DetonationRadius;
        public readonly float DesiredProjectileSpeed;
        public readonly float HitSoundDistSqr;
        public readonly float AmmoTravelSoundDistSqr;
        public readonly float AmmoSoundMaxDistSqr;
        public readonly float BaseDps;
        public readonly float AreaDps;
        public readonly float EffectiveDps;
        public readonly float DetDps;
        public readonly float PeakDps;
        public readonly float ShotsPerSec;
        public readonly float MaxTrajectory;
        public readonly float ShotFadeStep;
        public readonly float TrajectoryStep;
        public readonly double AreaRadiusSmall;
        public readonly double AreaRadiusLarge;
        public readonly double AreaEffectSize;
        public readonly double DetonateRadiusSmall;
        public readonly double DetonateRadiusLarge;
        public readonly double ShieldModifier;
        public readonly double MaxLateralThrust;
        public readonly double EwarTriggerRange;
        public readonly double TracerLength;
        public readonly double CollisionSize;
        public readonly double SmartsDelayDistSqr;
        public readonly double SegmentStep;
        public readonly double HealthHitModifier;
        public readonly double VoxelHitModifier;

        internal AmmoConstants(WeaponSystem.AmmoType ammo, WeaponDefinition wDef, Session session, WeaponSystem system, int ammoIndex)
        {
            AmmoIdxPos = ammoIndex;
            MyInventory.GetItemVolumeAndMass(ammo.AmmoDefinitionId, out MagMass, out MagVolume);
            MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(ammo.AmmoDefinitionId);

            IsCriticalReaction = wDef.HardPoint.HardWare.CriticalReaction.Enable;

            ComputeTextures(ammo, out TracerTextures, out SegmentTextures, out TrailTextures, out TracerMode, out TrailMode);

            if (ammo.AmmoDefinitionId.SubtypeId.String != "Energy" || ammo.AmmoDefinitionId.SubtypeId.String == string.Empty) AmmoItem = new MyPhysicalInventoryItem { Amount = 1, Content = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_AmmoMagazine>(ammo.AmmoDefinitionId.SubtypeName) };

            if (!string.IsNullOrEmpty(ammo.EjectionDefinitionId.SubtypeId.String))
            {
                var itemEffect = ammo.AmmoDef.Ejection.Type == AmmoDef.EjectionDef.SpawnType.Item;
                if (itemEffect)
                    EjectItem = new MyPhysicalInventoryItem { Amount = 1, Content = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Component>(ammo.EjectionDefinitionId.SubtypeId.String) };
                HasEjectEffect = itemEffect && EjectItem.Content != null;
            }
            else if (ammo.AmmoDef.Ejection.Type == AmmoDef.EjectionDef.SpawnType.Particle && !string.IsNullOrEmpty(ammo.AmmoDef.AmmoGraphics.Particles.Eject.Name))
                HasEjectEffect = true;

            if (AmmoItem.Content != null && !session.AmmoItems.ContainsKey(AmmoItem.ItemId))
                session.AmmoItems[AmmoItem.ItemId] = AmmoItem;

            var guidedAmmo = false;
            for (int i = 0; i < wDef.Ammos.Length; i++)
            {
                var ammoType = wDef.Ammos[i];
                if (ammoType.Trajectory.Guidance != None)
                    guidedAmmo = true;

                if (ammoType.AmmoRound.Equals(ammo.AmmoDef.Fragment.AmmoRound))
                    ShrapnelId = i;
            }

            FixedFireAmmo = system.TurretMovement == WeaponSystem.TurretType.Fixed && ammo.AmmoDef.Trajectory.Guidance == None;
            IsMine = ammo.AmmoDef.Trajectory.Guidance == DetectFixed || ammo.AmmoDef.Trajectory.Guidance == DetectSmart || ammo.AmmoDef.Trajectory.Guidance == DetectTravelTo;
            IsField = ammo.AmmoDef.Trajectory.FieldTime > 0;
            IsHybrid = ammo.AmmoDef.HybridRound;
            IsTurretSelectable = !ammo.IsShrapnel && ammo.AmmoDef.HardPointUsable;

            AmmoParticleShrinks = ammo.AmmoDef.AmmoGraphics.Particles.Ammo.ShrinkByDistance;
            HitParticleShrinks = ammo.AmmoDef.AmmoGraphics.Particles.Hit.ShrinkByDistance;
            FieldParticleShrinks = ammo.AmmoDef.AreaEffect.Pulse.Particle.ShrinkByDistance;

            AmmoParticle = !string.IsNullOrEmpty(ammo.AmmoDef.AmmoGraphics.Particles.Ammo.Name);
            HitParticle = !string.IsNullOrEmpty(ammo.AmmoDef.AmmoGraphics.Particles.Hit.Name);
            FieldParticle = !string.IsNullOrEmpty(ammo.AmmoDef.AreaEffect.Pulse.Particle.Name);
            CustomExplosionSound = !string.IsNullOrEmpty(ammo.AmmoDef.AreaEffect.Explosions.CustomSound);
            DrawLine = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Enable;
            LineColorVariance = ammo.AmmoDef.AmmoGraphics.Lines.ColorVariance.Start > 0 && ammo.AmmoDef.AmmoGraphics.Lines.ColorVariance.End > 0;
            LineWidthVariance = ammo.AmmoDef.AmmoGraphics.Lines.WidthVariance.Start > 0 || ammo.AmmoDef.AmmoGraphics.Lines.WidthVariance.End > 0;
            SegmentColorVariance = TracerMode == Texture.Resize && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.ColorVariance.Start > 0 && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.ColorVariance.End > 0;
            SegmentWidthVariance = TracerMode == Texture.Resize && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.WidthVariance.Start > 0 || ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.WidthVariance.End > 0;

            SegmentStep = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Speed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            SpeedVariance = ammo.AmmoDef.Trajectory.SpeedVariance.Start > 0 || ammo.AmmoDef.Trajectory.SpeedVariance.End > 0;
            RangeVariance = ammo.AmmoDef.Trajectory.RangeVariance.Start > 0 || ammo.AmmoDef.Trajectory.RangeVariance.End > 0;
            TrailWidth = ammo.AmmoDef.AmmoGraphics.Lines.Trail.CustomWidth > 0 ? ammo.AmmoDef.AmmoGraphics.Lines.Trail.CustomWidth : ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Width;
            TargetOffSet = ammo.AmmoDef.Trajectory.Smarts.Inaccuracy > 0;
            TargetLossTime = ammo.AmmoDef.Trajectory.TargetLossTime > 0 ? ammo.AmmoDef.Trajectory.TargetLossTime : int.MaxValue;
            CanZombie = TargetLossTime > 0 && TargetLossTime != int.MaxValue && !IsMine;
            MaxLifeTime = ammo.AmmoDef.Trajectory.MaxLifeTime > 0 ? ammo.AmmoDef.Trajectory.MaxLifeTime : int.MaxValue;

            MaxChaseTime = ammo.AmmoDef.Trajectory.Smarts.MaxChaseTime > 0 ? ammo.AmmoDef.Trajectory.Smarts.MaxChaseTime : int.MaxValue;
            MaxObjectsHit = ammo.AmmoDef.ObjectsHit.MaxObjectsHit > 0 ? ammo.AmmoDef.ObjectsHit.MaxObjectsHit : int.MaxValue;
            BaseDamage = ammo.AmmoDef.Override.BaseDamage;
            MaxTargets = ammo.AmmoDef.Trajectory.Smarts.MaxTargets;
            TargetLossDegree = ammo.AmmoDef.Trajectory.TargetLossDegree > 0 ? (float)Math.Cos(MathHelper.ToRadians(ammo.AmmoDef.Trajectory.TargetLossDegree)) : 0;

            ShieldModifier = ammo.AmmoDef.Override.ShieldModifer;
            
            AmmoSkipAccel = ammo.AmmoDef.Trajectory.AccelPerSec <= 0;
            FeelsGravity = ammo.AmmoDef.Trajectory.GravityMultiplier > 0;

            MaxTrajectory = ammo.AmmoDef.Override.MaxTrajectory;
            HasBackKickForce = ammo.AmmoDef.BackKickForce > 0;

            MaxLateralThrust = MathHelperD.Clamp(ammo.AmmoDef.Trajectory.Smarts.MaxLateralThrust, 0.000001, 1);

            ComputeShieldBypass(ammo, out ShieldDamageBypassMod);

            ComputeAmmoPattern(ammo, wDef, guidedAmmo, out AmmoPattern, out PatternIndexCnt, out GuidedAmmoDetected);

            Fields(ammo.AmmoDef, out PulseInterval, out PulseChance, out Pulse, out PulseGrowTime);
            AreaEffects(ammo.AmmoDef, out AreaEffect, out AreaEffectDamage, out AreaEffectSize, out DetonationDamage, out DetonationRadius, out AmmoAreaEffect, out AreaRadiusSmall, out AreaRadiusLarge, out DetonateRadiusSmall, out DetonateRadiusLarge, out Ewar, out EwarEffect, out EwarTriggerRange, out MinArmingTime);

            DamageScales(ammo.AmmoDef, out DamageScaling, out FallOffScaling, out ArmorScaling, out CustomDamageScales, out CustomBlockDefinitionBasesToScales, out SelfDamage, out VoxelDamage, out HealthHitModifier, out VoxelHitModifier);
            Beams(ammo.AmmoDef, out IsBeamWeapon, out VirtualBeams, out RotateRealBeam, out ConvergeBeams, out OneHitParticle, out OffsetEffect);
            CollisionShape(ammo.AmmoDef, out CollisionIsLine, out CollisionSize, out TracerLength);
            SmartsDelayDistSqr = (CollisionSize * ammo.AmmoDef.Trajectory.Smarts.TrackingDelay) * (CollisionSize * ammo.AmmoDef.Trajectory.Smarts.TrackingDelay);
            PrimeEntityPool = Models(ammo.AmmoDef, wDef, out PrimeModel, out TriggerModel, out ModelPath);
            Energy(ammo, system, wDef, out EnergyAmmo, out MustCharge, out Reloadable, out EnergyMagSize, out ChargSize, out BurstMode, out HasShotReloadDelay);
            Sound(ammo.AmmoDef, session, out HitSound, out AltHitSounds, out AmmoTravelSound, out HitSoundDistSqr, out AmmoTravelSoundDistSqr, out AmmoSoundMaxDistSqr);
            MagazineSize = EnergyAmmo ? EnergyMagSize : MagazineDef.Capacity;
            GetPeakDps(ammo, system, wDef, out PeakDps, out EffectiveDps, out ShotsPerSec, out BaseDps, out AreaDps, out DetDps, out RealShotsPerMin);
            ClientPredictedAmmo = FixedFireAmmo && RealShotsPerMin <= 120;

            DesiredProjectileSpeed = (!IsBeamWeapon ? ammo.AmmoDef.Trajectory.DesiredSpeed : MaxTrajectory * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
            Trail = ammo.AmmoDef.AmmoGraphics.Lines.Trail.Enable;
            HasShotFade = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeStart > 0 && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeEnd > 1;
            MaxTrajectoryGrows = ammo.AmmoDef.Trajectory.MaxTrajectoryTime > 1;
            ComputeSteps(ammo, out ShotFadeStep, out TrajectoryStep, out AlwaysDraw);

            if (CollisionSize > 5 && !session.LocalVersion) Log.Line($"{ammo.AmmoDef.AmmoRound} has large largeCollisionSize: {CollisionSize} meters");
        }

        internal void ComputeShieldBypass(WeaponSystem.AmmoType ammo, out float shieldDamageBypassMod)
        {
            if (ammo.AmmoDef.DamageScales.Shields.BypassModifier <= 0)
                shieldDamageBypassMod = 0;
            else if (ammo.AmmoDef.DamageScales.Shields.BypassModifier >= 1)
                shieldDamageBypassMod = 0.00001f;
            else
                shieldDamageBypassMod = MathHelper.Clamp(1 - ammo.AmmoDef.DamageScales.Shields.BypassModifier, 0.00001f, 0.99999f);
        }

        internal void ComputeTextures(WeaponSystem.AmmoType ammo, out MyStringId[] tracerTextures, out MyStringId[] segmentTextures, out MyStringId[] trailTextures, out Texture tracerTexture, out Texture trailTexture)
        {
            var lineSegments = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Enable && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.SegmentLength > 0;

            if (lineSegments)
                tracerTexture = Texture.Resize;
            else if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.TextureMode == AmmoDef.GraphicDef.LineDef.Texture.Normal)
                tracerTexture = Texture.Normal;
            else if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.TextureMode == AmmoDef.GraphicDef.LineDef.Texture.Cycle)
                tracerTexture = Texture.Cycle;
            else if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.TextureMode == AmmoDef.GraphicDef.LineDef.Texture.Wave)
                tracerTexture = Texture.Wave;
            else tracerTexture = Texture.Chaos;
            trailTexture = (Texture)ammo.AmmoDef.AmmoGraphics.Lines.Trail.TextureMode;

            if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures != null && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures.Length > 0)
            {
                tracerTextures = new MyStringId[ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures.Length];
                for (int i = 0; i < ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures.Length; i++)
                {
                    var value = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures[i];
                    if (string.IsNullOrEmpty(value))
                        value = ammo.AmmoDef.AmmoGraphics.Lines.TracerMaterial;
                    tracerTextures[i] = MyStringId.GetOrCompute(value);
                }
            }
            else tracerTextures = new[] { MyStringId.GetOrCompute(ammo.AmmoDef.AmmoGraphics.Lines.TracerMaterial) };

            if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures != null && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures.Length > 0)
            {
                segmentTextures = new MyStringId[ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures.Length];
                for (int i = 0; i < ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures.Length; i++)
                {
                    var value = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures[i];
                    if (string.IsNullOrEmpty(value))
                        value = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Material;
                    segmentTextures[i] = MyStringId.GetOrCompute(value);
                }
            }
            else segmentTextures = new[] { MyStringId.GetOrCompute(ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Material) };

            if (ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures != null && ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures.Length > 0)
            {
                trailTextures = new MyStringId[ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures.Length];
                for (int i = 0; i < ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures.Length; i++)
                {
                    var value = ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures[i];
                    if (string.IsNullOrEmpty(value))
                        value = ammo.AmmoDef.AmmoGraphics.Lines.Trail.Material;
                    trailTextures[i] = MyStringId.GetOrCompute(value);
                }
            }
            else trailTextures = new[] { MyStringId.GetOrCompute(ammo.AmmoDef.AmmoGraphics.Lines.Trail.Material) };
        }


        private void ComputeSteps(WeaponSystem.AmmoType ammo, out float shotFadeStep, out float trajectoryStep, out bool alwaysDraw)
        {
            var changeFadeSteps = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeEnd - ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeStart;
            shotFadeStep = 1f / changeFadeSteps;

            trajectoryStep = MaxTrajectoryGrows ? MaxTrajectory / ammo.AmmoDef.Trajectory.MaxTrajectoryTime : MaxTrajectory;
            alwaysDraw = (Trail || HasShotFade) && ShotsPerSec < 0.1;
        }

        private void ComputeAmmoPattern(WeaponSystem.AmmoType ammo, WeaponDefinition wDef, bool guidedAmmo, out AmmoDef[] ammoPattern, out int patternIndex, out bool guidedDetected)
        {
            var pattern = ammo.AmmoDef.Pattern;
            var indexPos = 0;

            int indexCount;
            if (!pattern.Enable)
                indexCount = 1;
            else
            {
                indexCount = pattern.Patterns.Length;
                if (!pattern.SkipParent) indexCount += 1;
            }

            patternIndex = indexCount;

            ammoPattern = new AmmoDef[indexCount];

            if (!pattern.Enable || !pattern.SkipParent)
                ammoPattern[indexPos++] = ammo.AmmoDef;

            if (pattern.Enable)
            {

                for (int i = 0; i < wDef.Ammos.Length; i++)
                {

                    var ammoDef = wDef.Ammos[i];
                    for (int j = 0; j < ammo.AmmoDef.Pattern.Patterns.Length; j++)
                    {

                        var aPattern = ammo.AmmoDef.Pattern.Patterns[j];

                        if (aPattern.Equals(ammoDef.AmmoRound))
                        {
                            ammoPattern[indexPos++] = ammoDef;
                            if (!guidedAmmo && ammoDef.Trajectory.Guidance != None)
                                guidedAmmo = true;
                        }
                    }
                }
            }
            guidedDetected = guidedAmmo;
        }

        internal void GetParticleInfo(WeaponSystem.AmmoType ammo, WeaponDefinition wDef, Session session)
        {
            var list = MyDefinitionManager.Static.GetAllSessionPreloadObjectBuilders();
            var comparer = new Session.HackEqualityComparer();
            for (int i = 0; i < list.Count; i++)
            {
                var tuple = (IStructuralEquatable)list[i];
                if (tuple != null)
                {
                    tuple.GetHashCode(comparer);
                    var hacked = comparer.Def;
                    if (hacked != null)
                    {
                        if (hacked.ParticleEffects != null)
                        {
                            foreach (var particle in hacked.ParticleEffects)
                            {
                                if (particle.Id.SubtypeId.Contains("Spark"))
                                    Log.Line($"test: {particle.Id.SubtypeId} - {ammo.AmmoDef.AmmoGraphics.Particles.Hit.Name}");
                            }
                        }
                    }
                }
            }
        }

        private int mexLogLevel = 0;
        private void GetPeakDps(WeaponSystem.AmmoType ammoDef, WeaponSystem system, WeaponDefinition wDef, out float peakDps, out float effectiveDps, out float shotsPerSec, out float baseDps, out float areaDps, out float detDps, out float realShotsPerMin)
        {
            var s = system;
            var a = ammoDef.AmmoDef;
            var hasShrapnel = ShrapnelId > -1;
            var l = wDef.HardPoint.Loading;


            if (mexLogLevel >= 1) Log.Line("-----");
            if (mexLogLevel >= 1) Log.Line($"Name = {s.PartName}"); //a.EnergyMagazineSize
            if (mexLogLevel >= 2) Log.Line($"EnergyMag = {a.EnergyMagazineSize}");

            var baselineRange = 1000;

            //Inaccuracy
            var inaccuracyRadius = Math.Tan(wDef.HardPoint.DeviateShotAngle / 2) * baselineRange;

            var inaccuracyScore = ((Math.PI * 10 * 10) / (Math.PI * inaccuracyRadius * inaccuracyRadius));
            inaccuracyScore = inaccuracyScore > 1 ? 1 : inaccuracyScore;
            inaccuracyScore = wDef.HardPoint.DeviateShotAngle <= 0 ? 1 : inaccuracyScore;


            //EffectiveRange
            var effectiveRangeScore = 1 / (baselineRange / a.Trajectory.DesiredSpeed);
            effectiveRangeScore = effectiveRangeScore > 1 ? 1 : effectiveRangeScore;
            effectiveRangeScore = a.Beams.Enable ? 1 : effectiveRangeScore;
            effectiveRangeScore = 1;


            //TrackingScore
            var coverageScore = ((Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation)) * ((Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth)))) / (360 * 90);
            coverageScore = coverageScore > 1 ? 1 : coverageScore;

            var speedEl = (wDef.HardPoint.HardWare.ElevateRate * (180 / Math.PI)) * 60;
            var coverageElevateScore = speedEl / (180d / 5d);
            var speedAz = (wDef.HardPoint.HardWare.RotateRate * (180 / Math.PI)) * 60;
            var coverageRotateScore = speedAz / (180d / 5d);

            var trackingScore = (coverageScore + ((coverageRotateScore + coverageElevateScore) * 0.5d)) * 0.5d;
            //if a sorter weapon use several barrels with only elevation or rotation the score should be uneffected since its designer to work
            if (MyUtils.IsZero(Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation)))
                trackingScore = (coverageScore + ((coverageRotateScore + 1) * 0.5d)) * 0.5d;

            if ((Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth)) == 0)
                trackingScore = (coverageScore + ((coverageElevateScore + 1) * 0.5d)) * 0.5d;

            if (MyUtils.IsZero(Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation) + (Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth))))
                trackingScore = 1.0d;

            trackingScore = trackingScore > 1 ? 1 : trackingScore;

            //FinalScore
            var effectiveModifier = ((effectiveRangeScore * inaccuracyScore) * trackingScore);

            // static weapons get a tracking score of 50%
            if (MyUtils.IsZero(Math.Abs(Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation))) || Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth) == 0)
                trackingScore = 0.5f;


            //Logs for effective dps
            if (mexLogLevel >= 2) Log.Line($"newInaccuracyRadius = {inaccuracyRadius}");
            if (mexLogLevel >= 2) Log.Line($"DeviationAngle = { wDef.HardPoint.DeviateShotAngle}");
            if (mexLogLevel >= 1) Log.Line($"InaccuracyScore = {inaccuracyScore}");
            if (mexLogLevel >= 1) Log.Line($"effectiveRangeScore = {effectiveRangeScore}");
            if (mexLogLevel >= 2) Log.Line($"coverageScore = {coverageScore}");
            if (mexLogLevel >= 2) Log.Line($"ElevateRate = {(wDef.HardPoint.HardWare.ElevateRate * (180 / Math.PI))}");
            if (mexLogLevel >= 2) Log.Line($"coverageElevate = {speedEl}");
            if (mexLogLevel >= 2) Log.Line($"coverageElevateScore = {coverageElevateScore}");
            if (mexLogLevel >= 2) Log.Line($"RotateRate = {(wDef.HardPoint.HardWare.RotateRate * (180 / Math.PI))}");
            if (mexLogLevel >= 2) Log.Line($"coverageRotate = {speedAz}");
            if (mexLogLevel >= 2) Log.Line($"coverageRotateScore = {coverageRotateScore}");

            if (mexLogLevel >= 2) Log.Line($"CoverageScore = {(coverageScore + ((coverageRotateScore + coverageElevateScore) * 0.5d)) * 0.5d}");
            if (mexLogLevel >= 1) Log.Line($"trackingScore = {trackingScore}");
            if (mexLogLevel >= 1) Log.Line($"effectiveModifier = {effectiveModifier}");


            //DPS Calc


            if (!EnergyAmmo && MagazineSize > 0 || IsHybrid)
            {
                shotsPerSec = GetShotsPerSecond(MagazineSize, s.RateOfFire, s.ReloadTime, s.BarrelsPerShot, l.TrajectilesPerBarrel, l.ShotsInBurst, l.DelayAfterBurst);
            }
            else if (EnergyAmmo && a.EnergyMagazineSize > 0)
            {
                shotsPerSec = GetShotsPerSecond(a.EnergyMagazineSize, s.RateOfFire, s.ReloadTime, s.BarrelsPerShot, l.TrajectilesPerBarrel, l.ShotsInBurst, l.DelayAfterBurst);
            }
            else
            {
                shotsPerSec = GetShotsPerSecond(1, s.RateOfFire, 0, s.BarrelsPerShot, l.TrajectilesPerBarrel, s.ShotsPerBurst, l.DelayAfterBurst);
            }
            var shotsPerSecPower = shotsPerSec; //save for power calc

            if (s.HeatPerShot > 0)
            {


                var heatGenPerSec = (l.HeatPerShot * shotsPerSec) - l.HeatSinkRate; //heat - cooldown



                if (heatGenPerSec > 0)
                {

                    var safeToOverheat = (l.MaxHeat - (l.MaxHeat * l.Cooldown)) / heatGenPerSec;
                    var cooldownTime = (l.MaxHeat - (l.MaxHeat * l.Cooldown)) / l.HeatSinkRate;

                    var timeHeatCycle = (safeToOverheat + cooldownTime);


                    shotsPerSec = ((safeToOverheat / timeHeatCycle) * shotsPerSec);

                    if ((mexLogLevel >= 1))
                    {
                        Log.Line($"Name = {s.PartName}");
                        Log.Line($"HeatPerShot = {l.HeatPerShot}");
                        Log.Line($"HeatGenPerSec = {heatGenPerSec}");

                        Log.Line($"WepCoolDown = {l.Cooldown}");

                        Log.Line($"safeToOverheat = {safeToOverheat}");
                        Log.Line($"cooldownTime = {cooldownTime}");


                        Log.Line($"timeHeatCycle = {timeHeatCycle}s");

                        Log.Line($"shotsPerSec wHeat = {shotsPerSec}");
                    }

                }

            }
            realShotsPerMin = (shotsPerSec * 60);
            baseDps = BaseDamage * shotsPerSec;
            areaDps = (GetAreaDmg(a) * shotsPerSec);
            detDps = (GetDetDmg(a) * shotsPerSec);
            if (mexLogLevel >= 1) Log.Line($"Got Area damage={GetAreaDmg(a)} det={GetDetDmg(a)} @ {shotsPerSec} areadps={areaDps} basedps={baseDps} detdps={detDps}");
            if (hasShrapnel)
            {
                var sAmmo = wDef.Ammos[ShrapnelId];
                var fragments = a.Fragment.Fragments;
                baseDps += (sAmmo.BaseDamage * fragments) * shotsPerSec;
                areaDps += (GetAreaDmg(sAmmo) * fragments) * shotsPerSec;
                detDps += (GetDetDmg(sAmmo) * fragments) * shotsPerSec;
            }
            peakDps = (baseDps + areaDps + detDps);
            effectiveDps = (float)(peakDps * effectiveModifier);
            if (mexLogLevel >= 1) Log.Line($"peakDps= {peakDps}");

            if (mexLogLevel >= 1) Log.Line($"Effective DPS(mult) = {effectiveDps}");
        }

        private float GetShotsPerSecond(int magCapacity, int rof, int reloadTime, int barrelsPerShot, int trajectilesPerBarrel, int shotsInBurst, int delayAfterBurst)
        {
            if (mexLogLevel > 0) Log.Line($"magCapacity={magCapacity} rof={rof} reloadTime={reloadTime} barrelsPerShot={barrelsPerShot} trajectilesPerBarrel={trajectilesPerBarrel} shotsInBurst={shotsInBurst} delayAfterBurst={delayAfterBurst}");
            var reloadsPerRoF = rof / (magCapacity / (float)barrelsPerShot);
            var burstsPerRoF = shotsInBurst == 0 ? 0 : rof / (float)shotsInBurst;
            var ticksReloading = reloadsPerRoF * reloadTime;

            var ticksDelaying = burstsPerRoF * delayAfterBurst;

            if (mexLogLevel > 0) Log.Line($"burstsPerRof={burstsPerRoF} reloadsPerRof={reloadsPerRoF} ticksReloading={ticksReloading} ticksDelaying={ticksDelaying}");
            float shotsPerSecond = rof / (60f + (ticksReloading / 60) + (ticksDelaying / 60));

            return shotsPerSecond * trajectilesPerBarrel * barrelsPerShot;
        }

        private float GetAreaDmg(AmmoDef a)
        {
            if (a.AreaEffect.AreaEffect == AreaEffectType.Disabled)
                return 0;

            var areaEffectDamage = a.Override.AreaEffectDamage;
            var areaEffectSize = a.AreaEffect.Base.Radius > 0 ? a.AreaEffect.Base.Radius : a.AreaEffect.AreaEffectRadius;
            if (a.AreaEffect.AreaEffect == AreaEffectType.Radiant)
            {
                return areaEffectDamage;
            }
            return (float)(areaEffectDamage * (areaEffectSize * 0.5d));
        }

        private float GetDetDmg(AmmoDef a)
        {
            if (!a.AreaEffect.Detonation.DetonateOnEnd || a.AreaEffect.AreaEffect == AreaEffectType.Disabled)
            {
                return 0;
            }
            if (a.AreaEffect.AreaEffect == AreaEffectType.Radiant)
            {
                return a.Override.DetonationDamage;
            }
            return (float)(a.AreaEffect.Detonation.DetonationDamage * (a.AreaEffect.Detonation.DetonationRadius * 0.5d));
        }

        private void Fields(AmmoDef ammoDef, out int pulseInterval, out int pulseChance, out bool pulse, out int growTime)
        {
            pulseInterval = ammoDef.AreaEffect.Pulse.Interval;
            growTime = ammoDef.AreaEffect.Pulse.GrowTime == 0 && pulseInterval > 0 ? 60 : ammoDef.AreaEffect.Pulse.GrowTime;
            pulseChance = ammoDef.AreaEffect.Pulse.PulseChance;
            pulse = pulseInterval > 0 && pulseChance > 0 && !ammoDef.Beams.Enable;
        }

        private void AreaEffects(AmmoDef ammoDef, out AreaEffectType areaEffect, out float areaEffectDamage, out double areaEffectSize, out float detonationDamage, out float detonationRadius, out bool ammoAreaEffect, out double areaRadiusSmall, out double areaRadiusLarge, out double detonateRadiusSmall, out double detonateRadiusLarge, out bool eWar, out bool eWarEffect, out double eWarTriggerRange, out int minArmingTime)
        {
            areaEffect = ammoDef.AreaEffect.AreaEffect;
            areaEffectDamage = ammoDef.Override.AreaEffectDamage;
            areaEffectSize = ammoDef.AreaEffect.Base.Radius > 0 ? ammoDef.AreaEffect.Base.Radius : ammoDef.AreaEffect.AreaEffectRadius;
            detonationDamage = ammoDef.Override.DetonationDamage;
            detonationRadius = ammoDef.AreaEffect.Detonation.DetonationRadius;

            ammoAreaEffect = ammoDef.AreaEffect.AreaEffect != AreaEffectType.Disabled;
            areaRadiusSmall = Session.ModRadius(areaEffectSize, false);
            areaRadiusLarge = Session.ModRadius(areaEffectSize, true);
            detonateRadiusSmall = Session.ModRadius(detonationRadius, false);
            detonateRadiusLarge = Session.ModRadius(detonationRadius, true);
            eWar = areaEffect > (AreaEffectType)2;
            eWarEffect = areaEffect > (AreaEffectType)3;
            eWarTriggerRange = eWar && Pulse && ammoDef.AreaEffect.EwarFields.TriggerRange > 0 ? ammoDef.AreaEffect.EwarFields.TriggerRange : 0;
            minArmingTime = ammoDef.AreaEffect.Detonation.MinArmingTime;
        }


        private MyConcurrentPool<MyEntity> Models(AmmoDef ammoDef, WeaponDefinition wDef, out bool primeModel, out bool triggerModel, out string primeModelPath)
        {
            if (ammoDef.AreaEffect.AreaEffect > (AreaEffectType)3 && IsField) triggerModel = true;
            else triggerModel = false;
            primeModel = ammoDef.AmmoGraphics.ModelName != string.Empty;
            primeModelPath = primeModel ? wDef.ModPath + ammoDef.AmmoGraphics.ModelName : string.Empty;
            return primeModel ? new MyConcurrentPool<MyEntity>(256, PrimeEntityClear, 10000, PrimeEntityActivator) : null;
        }


        private void Beams(AmmoDef ammoDef, out bool isBeamWeapon, out bool virtualBeams, out bool rotateRealBeam, out bool convergeBeams, out bool oneHitParticle, out bool offsetEffect)
        {
            isBeamWeapon = ammoDef.Beams.Enable;
            virtualBeams = ammoDef.Beams.VirtualBeams && IsBeamWeapon;
            rotateRealBeam = ammoDef.Beams.RotateRealBeam && VirtualBeams;
            convergeBeams = !RotateRealBeam && ammoDef.Beams.ConvergeBeams && VirtualBeams;
            oneHitParticle = ammoDef.Beams.OneParticle && IsBeamWeapon && VirtualBeams;
            offsetEffect = ammoDef.AmmoGraphics.Lines.OffsetEffect.MaxOffset > 0;
        }

        private void CollisionShape(AmmoDef ammoDef, out bool collisionIsLine, out double collisionSize, out double tracerLength)
        {
            var isLine = ammoDef.Shape.Shape == LineShape;
            var size = ammoDef.Shape.Diameter;

            if (IsBeamWeapon)
                tracerLength = MaxTrajectory;
            else tracerLength = ammoDef.AmmoGraphics.Lines.Tracer.Length > 0 ? ammoDef.AmmoGraphics.Lines.Tracer.Length : 0.1;

            if (size <= 0)
            {
                if (!isLine) isLine = true;
                size = 1;
            }
            else if (!isLine) size *= 0.5;
            collisionIsLine = isLine;
            collisionSize = size;
        }

        private void DamageScales(AmmoDef ammoDef, out bool damageScaling, out bool fallOffScaling, out bool armorScaling, out bool customDamageScales, out Dictionary<MyDefinitionBase, float> customBlockDef, out bool selfDamage, out bool voxelDamage, out double healthHitModifer, out double voxelHitModifer)
        {
            armorScaling = false;
            customDamageScales = false;
            fallOffScaling = false;
            var d = ammoDef.DamageScales;
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
            damageScaling = d.FallOff.MinMultipler > 0 || d.MaxIntegrity > 0 || d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0 || d.Grids.Large >= 0 || d.Grids.Small >= 0 || customDamageScales;
            if (damageScaling)
            {
                armorScaling = d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0;
                fallOffScaling = d.FallOff.MinMultipler > 0;
            }
            selfDamage = ammoDef.DamageScales.SelfDamage && !IsBeamWeapon;
            voxelDamage = ammoDef.DamageScales.DamageVoxels;
            healthHitModifer = ammoDef.DamageScales.HealthHitModifier > 0 ? ammoDef.DamageScales.HealthHitModifier : 1;
            voxelHitModifer = ammoDef.DamageScales.VoxelHitModifier > 0 ? ammoDef.DamageScales.VoxelHitModifier : 1;
        }

        private void Energy(WeaponSystem.AmmoType ammoPair, WeaponSystem system, WeaponDefinition wDef, out bool energyAmmo, out bool mustCharge, out bool reloadable, out int energyMagSize, out float chargeSize, out bool burstMode, out bool shotReload)
        {
            energyAmmo = ammoPair.AmmoDefinitionId.SubtypeId.String == "Energy" || ammoPair.AmmoDefinitionId.SubtypeId.String == string.Empty;
            mustCharge = (energyAmmo || IsHybrid);

            reloadable = !energyAmmo || mustCharge;

            burstMode = wDef.HardPoint.Loading.ShotsInBurst > 0 && (energyAmmo || MagazineDef.Capacity >= wDef.HardPoint.Loading.ShotsInBurst);

            shotReload = !burstMode && wDef.HardPoint.Loading.ShotsInBurst > 0 && wDef.HardPoint.Loading.DelayAfterBurst > 0;

            if (mustCharge)
            {
                var ewar = (int)ammoPair.AmmoDef.AreaEffect.AreaEffect > 3;
                var shotEnergyCost = ewar ? ammoPair.AmmoDef.EnergyCost * AreaEffectDamage : ammoPair.AmmoDef.EnergyCost * BaseDamage;
                var shotsPerTick = system.RateOfFire / MyEngineConstants.UPDATE_STEPS_PER_MINUTE;
                var energyPerTick = shotEnergyCost * shotsPerTick;
                var requiredPowerPerTick = (energyPerTick * wDef.HardPoint.Loading.BarrelsPerShot) * wDef.HardPoint.Loading.TrajectilesPerBarrel;

                var reloadTime = system.ReloadTime > 0 ? system.ReloadTime : 1;
                chargeSize = requiredPowerPerTick * reloadTime;
                var chargeCeil = (int)Math.Ceiling(requiredPowerPerTick * reloadTime);

                energyMagSize = ammoPair.AmmoDef.EnergyMagazineSize > 0 ? ammoPair.AmmoDef.EnergyMagazineSize : chargeCeil;
                return;
            }
            chargeSize = 0;
            energyMagSize = 0;
        }

        private void Sound(AmmoDef ammoDef, Session session, out bool hitSound, out bool altHitSounds, out bool ammoTravelSound, out float hitSoundDistSqr, out float ammoTravelSoundDistSqr, out float ammoSoundMaxDistSqr)
        {
            hitSound = ammoDef.AmmoAudio.HitSound != string.Empty;
            altHitSounds = true; //ammoDef.AmmoAudio.VoxelHitSound != string.Empty || ammoDef.AmmoAudio.PlayerHitSound != string.Empty || ammoDef.AmmoAudio.FloatingHitSound != string.Empty;
            ammoTravelSound = ammoDef.AmmoAudio.TravelSound != string.Empty;
            var hitSoundStr = string.Concat(Arc, ammoDef.AmmoAudio.HitSound);
            var travelSoundStr = string.Concat(Arc, ammoDef.AmmoAudio.TravelSound);
            hitSoundDistSqr = 0;
            ammoTravelSoundDistSqr = 0;
            ammoSoundMaxDistSqr = 0;

            foreach (var def in session.SoundDefinitions)
            {
                var id = def.Id.SubtypeId.String;
                if (HitSound && id == hitSoundStr)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) hitSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (hitSoundDistSqr > ammoSoundMaxDistSqr) ammoSoundMaxDistSqr = hitSoundDistSqr;
                }
                else if (AmmoTravelSound && id == travelSoundStr)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) ammoTravelSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (ammoTravelSoundDistSqr > ammoSoundMaxDistSqr) ammoSoundMaxDistSqr = ammoTravelSoundDistSqr;
                }
            }
        }

        private MyEntity PrimeEntityActivator()
        {
            var ent = new MyEntity();
            ent.Init(null, ModelPath, null, null);
            ent.Render.CastShadows = false;
            ent.IsPreview = true;
            ent.Save = false;
            ent.SyncFlag = false;
            ent.NeedsWorldMatrix = false;
            ent.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(ent, false);
            return ent;
        }

        private static void PrimeEntityClear(MyEntity myEntity)
        {
            myEntity.PositionComp.SetWorldMatrix(ref MatrixD.Identity, null, false, false, false);
            myEntity.InScene = false;
            myEntity.Render.RemoveRenderObjects();
        }

        internal void ReturnSoundPair(object o)
        {
            var pair = (MySoundPair)o;
            CustomSoundPairs.Push(pair);
        }
    }

    public class AmmoOverrides
    {
        public float BaseDamage;
        public float AreaEffectDamage;
        public float DetonationDamage;
        public float Health;
        public float MaxTrajectory;
        public float ShieldModifer;

        internal AmmoOverrides(WeaponSystem.AmmoType ammo)
        {
            BaseDamage = ammo.AmmoDef.BaseDamage;
            AreaEffectDamage = ammo.AmmoDef.AreaEffect.Base.EffectStrength > 0 ? ammo.AmmoDef.AreaEffect.Base.EffectStrength : ammo.AmmoDef.AreaEffect.AreaEffectDamage;
            DetonationDamage = ammo.AmmoDef.AreaEffect.Detonation.DetonationDamage;
            Health = ammo.AmmoDef.Health;
            MaxTrajectory = ammo.AmmoDef.Trajectory.MaxTrajectory;
            ShieldModifer = ammo.AmmoDef.DamageScales.Shields.Modifier > 0 ? ammo.AmmoDef.DamageScales.Shields.Modifier : 1;
        }
    }
}
