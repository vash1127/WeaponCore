using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.PartDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.PartDefinition.HardPointDef.HardwareDef;

namespace WeaponCore.Platform
{
    public partial class UPgrade : Part
    {
        internal int NextMuzzle;
        internal volatile bool Casting;
        private readonly int _numOfBarrels;
        private readonly int _numModelBarrels;
        private readonly HashSet<string> _muzzlesToFire = new HashSet<string>();
        private readonly HashSet<string> _muzzlesFiring = new HashSet<string>();
        internal readonly Dictionary<int, string> MuzzleIdToName = new Dictionary<int, string>();
        
        internal readonly CoreComponent Comp;
        internal readonly CoreSystem System;
        internal readonly WeaponFrameCache WeaponCache;
        internal readonly WeaponAcquire Acquire;
        internal readonly Target Target;
        internal readonly Target NewTarget;
        internal readonly PartInfo MuzzlePart;
        internal readonly Dummy[] Dummies;
        internal readonly Dummy Ejector;
        internal readonly Dummy Scope;
        internal readonly Muzzle[] Muzzles;
        internal readonly PartInfo AzimuthPart;
        internal readonly PartInfo ElevationPart;
        internal readonly Dictionary<EventTriggers, ParticleEvent[]> ParticleEvents;
        internal readonly List<Action<long, int, ulong, long, Vector3D, bool>> Monitors = new List<Action<long, int, ulong, long, Vector3D, bool>>();
        internal readonly uint[] MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];
        internal readonly uint WeaponCreatedTick;

        internal Action<object> CancelableReloadAction = (o) => {};

        private int _nextVirtual;
        private uint _ticksUntilShoot;
        private uint _spinUpTick;
        private uint _ticksBeforeSpinUp;

        internal bool HeatLoopRunning;
        internal bool PreFired;
        internal bool FinishBurst;
        internal bool LockOnFireState;
        internal bool ReloadSubscribed;
        internal bool ScheduleAmmoChange;
        internal uint LastMagSeenTick;
        internal uint GravityTick;
        internal uint ShootTick;
        internal uint LastShootTick;
        internal uint TicksPerShot;
        internal uint PosChangedTick;
        internal uint ElevationTick;
        internal uint AzimuthTick;
        internal uint FastTargetResetTick;
        
        internal float HeatPerc;

        internal int ShortLoadId;
        internal int BarrelRate;
        internal int ArmorHits;
        internal int ShotsFired;
        internal int LastMuzzle;
        internal int MiddleMuzzleIndex;
        internal List<MyEntity> HeatingParts;
        internal Vector3D GravityPoint;
        internal Vector3D MyPivotPos;
        internal Vector3D BarrelOrigin;
        internal Vector3D MyPivotFwd;
        internal Vector3D MyPivotUp;
        internal Vector3D AimOffset;
        internal Vector3D AzimuthInitFwdDir;
        internal MatrixD WeaponConstMatrix;

        internal LineD MyCenterTestLine;
        internal LineD MyBarrelTestLine;
        internal LineD MyPivotTestLine;
        internal LineD MyAimTestLine;
        internal LineD MyShootAlignmentLine;
        internal LineD AzimuthFwdLine;

        internal uint[] BeamSlot;

        internal MyOrientedBoundingBoxD TargetBox;
        internal LineD LimitLine;


        internal MathFuncs.Cone AimCone = new MathFuncs.Cone();
        internal Matrix[] BarrelRotationPerShot = new Matrix[10];
        internal MyParticleEffect[] BarrelEffects1;
        internal MyParticleEffect[] BarrelEffects2;
        internal MyParticleEffect[] HitEffects;
        internal MySoundPair ReloadSound;
        internal MySoundPair PreFiringSound;
        internal MySoundPair FiringSound;
        internal MySoundPair RotateSound;
        internal WeaponStateValues State;
        internal WeaponReloadValues Reload;
        internal TransferTarget TargetData;
        internal AmmoValues Ammo;
        internal CoreSystem.ConsumableTypes ActiveAmmoDef;
        internal int[] AmmoShufflePattern = {0};
        internal ParallelRayCallBack RayCallBack;

        internal readonly MyEntity3DSoundEmitter ReloadEmitter;
        internal readonly MyEntity3DSoundEmitter PreFiringEmitter;
        internal readonly MyEntity3DSoundEmitter FiringEmitter;
        internal readonly MyEntity3DSoundEmitter RotateEmitter;
        internal readonly Dictionary<EventTriggers, PartAnimation[]> AnimationsSet;
        internal readonly Dictionary<string, PartAnimation> AnimationLookup = new Dictionary<string, PartAnimation>();

        internal readonly bool PrimaryWeaponGroup;
        internal readonly bool AiOnlyWeapon;

        internal IHitInfo LastHitInfo;
        internal EventTriggers LastEvent;
        internal float RequiredPower;
        internal float MaxCharge;
        internal float UseablePower;
        internal float OldUseablePower;
        internal float BaseDamage;
        internal float Dps;
        internal float ShotEnergyCost;
        internal float LastHeat;
        internal uint CeaseFireDelayTick = uint.MaxValue / 2;
        internal uint LastTargetTick;
        internal uint LastTrackedTick;
        internal uint LastMuzzleCheck;
        internal uint LastSmartLosCheck;
        internal uint LastLoadedTick;
        internal uint WeaponReadyTick;
        internal uint OffDelay;
        internal uint ChargeUntilTick;
        internal uint ChargeDelayTicks;
        internal uint AnimationDelayTick;
        internal uint LastHeatUpdateTick;
        internal uint LastInventoryTick;
        internal uint StopBarrelAvTick;
        internal int ProposedAmmoId = -1;
        internal int FireCounter;
        internal int UniqueId;
        internal int RateOfFire;
        internal int BarrelSpinRate;
        internal int WeaponId;
        internal int EnergyPriority;
        internal int LastBlockCount;
        internal int ClientStartId;
        internal int ClientEndId;
        internal int ClientMakeUpShots;
        internal float HeatPShot;
        internal float HsRate;
        internal float CurrentAmmoVolume;
        internal double Azimuth;
        internal double Elevation;
        internal double AimingTolerance;
        internal double RotationSpeed;
        internal double ElevationSpeed;
        internal double MaxAzToleranceRadians;
        internal double MinAzToleranceRadians;
        internal double MaxElToleranceRadians;
        internal double MinElToleranceRadians;
        internal double MaxAzimuthRadians;
        internal double MinAzimuthRadians;
        internal double MaxElevationRadians;
        internal double MinElevationRadians;
        internal double MaxTargetDistance;
        internal double MaxTargetDistanceSqr;
        internal double MinTargetDistance;
        internal double MinTargetDistanceSqr;
        internal double MinTargetDistanceBufferSqr;
        internal double MuzzleDistToBarrelCenter;
        internal bool ClientReloading;
        internal bool Rotating;
        internal bool IsTurret;
        internal bool TurretMode;
        internal bool TrackTarget;
        internal bool AiShooting;
        internal bool AiEnabled;
        internal bool IsShooting;
        internal bool PlayTurretAv;
        internal bool AvCapable;
        internal bool NoMagsToLoad;
        internal bool CurrentlyDegrading;
        internal bool FixedOffset;
        internal bool DrawingPower;
        internal bool RequestedPower;
        internal bool ResetPower;
        internal bool RecalcPower;
        internal bool ProjectilesNear;
        internal bool AcquiringTarget;
        internal bool BarrelSpinning;
        internal bool ReturingHome;
        internal bool IsHome = true;
        internal bool CanUseEnergyAmmo;
        internal bool CanUseHybridAmmo;
        internal bool CanUseChargeAmmo;
        internal bool CanUseBeams;
        internal bool PauseShoot;
        internal bool LastEventCanDelay;
        internal bool ShowBurstDelayAsReload;
        internal bool Reloading;
        internal bool Charging;
        internal bool ClientStaticShot;
        internal bool ShootOnce;
        internal bool ParentIsSubpart;
        internal bool AlternateForward;
        internal bool CheckInventorySystem = true;

        internal Upgrade()
        {

        }
    }
}
