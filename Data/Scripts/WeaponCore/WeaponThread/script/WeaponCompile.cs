using System.Collections.Generic;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Platform.Weapon.EventTriggers;

namespace WeaponThread
{
    partial class Weapons
    {
        internal List<WeaponDefinition> Weapon = new List<WeaponDefinition>();
        internal void ConfigFiles(params WeaponDefinition[] defs)
        {
            foreach (var def in defs) Weapon.Add(def);
        }

        internal WeaponDefinition[] ReturnDefs()
        {
            var weaponDefinitions = new WeaponDefinition[Weapon.Count];
            for (int i = 0; i < Weapon.Count; i++) weaponDefinitions[i] = Weapon[i];
            Weapon.Clear();
            return weaponDefinitions;
        }

        internal ParticleOptions Options(bool loop, bool restart, float distance, float duration, float scale)
        {
            return new ParticleOptions
            {
                Loop = loop,
                Restart = restart,
                MaxDistance = distance,
                MaxDuration = duration,
                Scale = scale, 
            };
        }

        internal Detonate Options(bool detonateOnEnd, bool armOnlyOnHit, float detonationDamage, float detonationRadius)
        {
            return new Detonate
            {
                DetonateOnEnd = detonateOnEnd,
                ArmOnlyOnHit = armOnlyOnHit,
                DetonationDamage = detonationDamage,
                DetonationRadius = detonationRadius,
            };
        }

        internal Explosion Options(bool noVisuals, bool noSound, float scale, string customParticle, string customSound)
        {
            return new Explosion
            {
                NoVisuals = noVisuals,
                NoSound = noSound,
                Scale = scale,
                CustomParticle = customParticle,
                CustomSound = customSound,
            };
        }

        internal GridSizeDefinition Options(float largeGridModifier, float smallGridModifier)
        {
            return new GridSizeDefinition { Large = largeGridModifier, Small = smallGridModifier };
        }

        internal ObjectsHit Options(int maxObjectsHit, bool countBlocks)
        {
            return new ObjectsHit { MaxObjectsHit = maxObjectsHit, CountBlocks = countBlocks };
        }

        internal Shrapnel Options(float baseDamage, int fragments, float maxTrajectory, bool noAudioVisual, bool noGuidance, Shrapnel.ShrapnelShape shape)
        {
            return new Shrapnel { BaseDamage = baseDamage, Fragments = fragments, MaxTrajectory = maxTrajectory, NoAudioVisual = noAudioVisual, NoGuidance = noGuidance, Shape = shape};
        }

        internal CustomScalesDefinition SubTypeIds(bool ignoreOthers, params CustomBlocksDefinition[] customDefScale)
        {
            return new CustomScalesDefinition {IgnoreAllOthers = ignoreOthers, Types = customDefScale};
        }

        internal ArmorDefinition Options(float armor, float light, float heavy, float nonArmor)
        {
            return new ArmorDefinition { Armor = armor, Light = light, Heavy = heavy, NonArmor = nonArmor };
        }

        internal OffsetEffect Options(double maxOffset, double minLength, double maxLength)
        {
            return new OffsetEffect { MaxOffset = maxOffset, MinLength = minLength, MaxLength = maxLength};
        }

        internal ShieldDefinition Options(float modifier, ShieldDefinition.ShieldType type)
        {
            return new ShieldDefinition { Modifier = modifier, Type = type };
        }

        internal ShapeDefinition Options(ShapeDefinition.Shapes shape, double diameter)
        {
            return new ShapeDefinition { Shape = shape, Diameter = diameter };
        }

        internal Pulse Options(int interval, int pulseChance)
        {
            return new Pulse { Interval = interval, PulseChance = pulseChance };
        }

        internal EwarFields Options(int duration, bool stackDuration, bool depletable)
        {
            return new EwarFields { Duration = duration, StackDuration = stackDuration, Depletable = depletable};
        }

        internal TrailDefinition Options(bool enable, string material, int decayTime, Vector4 color)
        {
            return new TrailDefinition { Enable = enable, Material = material, DecayTime = decayTime, Color = color };
        }

        internal Mines Options(double detectRadius, double deCloakRadius, int fieldTime, bool cloak, bool persist)
        {
            return new Mines {  DetectRadius = detectRadius, DeCloakRadius = deCloakRadius, FieldTime = fieldTime, Cloak = cloak, Persist = persist};
        }

        internal CustomBlocksDefinition Block(string subTypeId, float modifier)
        {
            return new CustomBlocksDefinition { SubTypeId = subTypeId, Modifier = modifier };
        }

        internal TracerBaseDefinition Base(bool enable, float length, float width, Vector4 color)
        {
            return new TracerBaseDefinition { Enable = enable, Length = length, Width = width, Color = color};
        }

        internal AimControlDefinition AimControl(bool trackTargets, bool turretAttached, bool turretController, float rotateRate, float elevateRate, Vector3D offset, bool debug)
        {
            return new AimControlDefinition { TrackTargets = trackTargets, TurretAttached = turretAttached, TurretController = turretController, RotateRate = rotateRate, ElevateRate = elevateRate, Offset = offset, Debug = debug};
        }

        internal UiDefinition Display(bool rateOfFire, bool damageModifier, bool toggleGuidance, bool enableOverload)
        {
            return new UiDefinition { RateOfFire = rateOfFire, DamageModifier = damageModifier, ToggleGuidance = toggleGuidance, EnableOverload = enableOverload };
        }

        internal TargetingDefinition.BlockTypes[] Priority(params TargetingDefinition.BlockTypes[] systems)
        {
            return systems;
        }

        internal TargetingDefinition.Threat[] Valid(params TargetingDefinition.Threat[] threats)
        {
            return threats;
        }

        internal Randomize Random(float start, float end)
        {
            return new Randomize { Start = start, End = end };
        }

        internal Vector4 Color(float red, float green, float blue, float alpha)
        {
            return new Vector4(red, green, blue, alpha);
        }

        internal Vector3D Vector(double x, double y, double z)
        {
            return new Vector3D(x, y, z);
        }

        internal MountPoint MountPoint(string subTypeId, string aimPartId, string muzzlePartId, string azimuthPartId = "", string elevationPartId = "")
        {
            return new MountPoint { SubtypeId = subTypeId, AimPartId = aimPartId, MuzzlePartId = muzzlePartId, AzimuthPartId = azimuthPartId, ElevationPartId = elevationPartId };
        }

        internal EventTriggers[] Events(params EventTriggers[] events)
        {
            return events;
        }

        internal XYZ Transformation(double X, double Y, double Z)
        {
            return new XYZ { x = X, y = Y, z = Z };
        }

        internal Dictionary<EventTriggers, uint> Delays(uint FiringDelay = 0, uint ReloadingDelay = 0, uint OverheatedDelay = 0, uint TrackingDelay = 0, uint LockedDelay =0, uint OnDelay = 0, uint OffDelay = 0, uint BurstReloadDelay = 0, uint OutOfAmmoDelay = 0, uint PreFireDelay = 0)
        {
            return new Dictionary<EventTriggers, uint>
            {
                [Firing] = FiringDelay,
                [Reloading] = ReloadingDelay,
                [Overheated] = OverheatedDelay,
                [Tracking] = TrackingDelay,
                [TurnOn] = OnDelay,
                [TurnOff] = OffDelay,
                [BurstReload] = BurstReloadDelay,
                [OutOfAmmo] = OutOfAmmoDelay,
                [PreFire] = PreFireDelay,
                [EmptyOnGameLoad] = 0,
            };
        }

        internal WeaponEmissive Emissive(string EmissiveName, bool CycleEmissiveParts, bool LeavePreviousOn, Vector4[] Colors, float IntensityFrom, float IntensityTo, string[] EmissivePartNames)
        {
            return new WeaponEmissive()
            {
                EmissiveName = EmissiveName,
                Colors = Colors,
                CycleEmissivesParts = CycleEmissiveParts,
                LeavePreviousOn = LeavePreviousOn,
                EmissivePartNames = EmissivePartNames,
                IntensityRange = new []{IntensityFrom,IntensityTo}
            };
        }

        internal string[] Names(params string[] names)
        {
            return names;
        }
    }
}
