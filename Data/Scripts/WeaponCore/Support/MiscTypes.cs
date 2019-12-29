using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Platform;
using static WeaponCore.Support.HitEntity.Type;
using static WeaponCore.Support.TargetingDefinition;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace WeaponCore.Support
{
    internal class ProInfo
    {
        internal readonly Target Target = new Target();
        internal readonly List<HitEntity> HitList = new List<HitEntity>();
        internal AvShot AvShot;
        internal WeaponSystem System;
        internal GridAi Ai;
        internal MyEntity PrimeEntity;
        internal MyEntity TriggerEntity;
        internal CompGroupOverrides Overrides;
        internal WeaponFrameCache WeaponCache;
        internal Vector3D ShooterVel;
        internal Vector3D Origin;
        internal Vector3D OriginUp;
        internal Vector3D Direction;
        internal int TriggerGrowthSteps;
        internal int WeaponId;
        internal int MuzzleId;
        internal int ObjectsHit;
        internal int Age;
        internal double DistanceTraveled;
        internal double PrevDistanceTraveled;
        internal double ProjectileDisplacement;
        internal float BaseDamagePool;
        internal float AreaEffectDamage;
        internal float DetonationDamage;
        internal float BaseHealthPool;
        internal bool IsShrapnel;
        internal bool EnableGuidance = true;
        internal bool LastHitShield;
        internal MatrixD TriggerMatrix = MatrixD.Identity;


        internal void InitVirtual(WeaponSystem system, GridAi ai, MyEntity primeEntity, MyEntity triggerEntity, Target target, int weaponId, int muzzleId, Vector3D origin, Vector3D direction)
        {
            System = system;
            Ai = ai;
            PrimeEntity = primeEntity;
            TriggerEntity = triggerEntity;
            Target.Entity = target.Entity;
            Target.Projectile = target.Projectile;
            Target.FiringCube = target.FiringCube;
            WeaponId = weaponId;
            MuzzleId = muzzleId;
            Direction = direction;
            Origin = origin;
        }

        internal void Clean()
        {
            Target.Reset();
            HitList.Clear();
            AvShot = null;
            System = null;
            PrimeEntity = null;
            TriggerEntity = null;
            Ai = null;
            WeaponCache = null;
            LastHitShield = false;
            IsShrapnel = false;
            TriggerGrowthSteps = 0;
            WeaponId = 0;
            MuzzleId = 0;
            Age = 0;
            ProjectileDisplacement = 0;
            EnableGuidance = true;
            Direction = Vector3D.Zero;
            Origin = Vector3D.Zero;
            ShooterVel = Vector3D.Zero;
            TriggerMatrix = MatrixD.Identity;
        }
    }

    internal struct VirtualProjectile
    {
        internal ProInfo Info;
        internal AvShot VisualShot;
    }

    internal class HitEntity
    {
        internal enum Type
        {
            Shield,
            Grid,
            Voxel,
            Destroyable,
            Stale,
            Projectile,
            Field,
            Effect,
        }

        public readonly List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
        public MyEntity Entity;
        internal Projectile Projectile;
        public ProInfo Info;
        public LineD Beam;
        public bool Hit;
        public bool SphereCheck;
        public bool DamageOverTime;
        public BoundingSphereD PruneSphere;
        public Vector3D? HitPos;
        public double? HitDist;
        public Type EventType;

        public void Clean()
        {
            Entity = null;
            Projectile = null;

            Beam.Length = 0;
            Beam.Direction = Vector3D.Zero;
            Beam.From = Vector3D.Zero;
            Beam.To = Vector3D.Zero;
            Blocks.Clear();
            Hit = false;
            HitPos = null;
            HitDist = null;
            Info = null;
            EventType = Stale;
            PruneSphere = new BoundingSphereD();
            SphereCheck = false;
            DamageOverTime = false;
        }
    }

    internal struct Hit
    {
        internal IMySlimBlock Block;
        internal MyEntity Entity;
        internal Projectile Projectile;
        internal Vector3D HitPos;
        internal Vector3D HitVelocity;
    }

    internal class Target
    {
        internal volatile Targets State = Targets.Expired;
        internal volatile bool IsProjectile;
        internal bool TargetLock;
        internal MyCubeBlock FiringCube;
        internal MyEntity Entity;
        internal Projectile Projectile;
        internal int[] TargetDeck = new int[0];
        internal int[] BlockDeck = new int[0];
        internal int TargetPrevDeckLen;
        internal int BlockPrevDeckLen;
        internal uint CheckTick;
        internal BlockTypes LastBlockType;
        internal Vector3D HitPos;
        internal double HitShortDist;
        internal double OrigDistance;
        internal long TopEntityId;
        internal readonly List<MyCubeBlock> Top5 = new List<MyCubeBlock>();

        public enum Targets
        {
            Expired,
            StillSeeking,
            Acquired,
        }

        internal Target(MyCubeBlock firingCube = null)
        {
            FiringCube = firingCube;
        }

        internal void TransferTo(Target target, bool reset = true)
        {
            target.Entity = Entity;
            target.Projectile = Projectile;
            target.IsProjectile = target.Projectile != null;
            target.HitPos = HitPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            target.State = State;
            if (reset) Reset();
        }

        internal void Set(MyEntity ent, Vector3D pos, double shortDist, double origDist, long topEntId, Projectile projectile = null)
        {
            Entity = ent;
            Projectile = projectile;
            IsProjectile = projectile != null;
            HitPos = pos;
            HitShortDist = shortDist;
            OrigDistance = origDist;
            TopEntityId = topEntId;
            State = Targets.Acquired;
        }

        internal void Reset(bool expire = true)
        {
            Entity = null;
            IsProjectile = false;
            Projectile = null;
            HitPos = Vector3D.Zero;
            HitShortDist = 0;
            OrigDistance = 0;
            TopEntityId = 0;
            if (expire)
            {
                CheckTick = 0;
                State = Targets.Expired;
            }
            TargetLock = false;
        }
    }

    internal class VoxelParallelHits
    {
        internal uint RequestTick;
        internal uint ResultTick;
        internal uint LastTick;
        internal IHitInfo HitInfo;
        private bool _start;
        private uint _startTick;
        private int _miss;
        private int _maxDelay;
        private bool _idle;
        private Vector3D _endPos = Vector3D.MinValue;

        internal bool Cached(LineD lineTest, ProInfo i)
        {
            double dist;
            Vector3D.DistanceSquared(ref _endPos, ref lineTest.To, out dist);

            _maxDelay = i.MuzzleId == -1 ? i.System.Barrels.Length : 1;
            
            var thisTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);
            _start = thisTick - LastTick > _maxDelay || dist > 5;

            LastTick = thisTick;

            if (_start)
            {
                _startTick = thisTick;
                _endPos = lineTest.To;
            }

            var runTime = thisTick - _startTick;

            var fastPath = runTime > (_maxDelay * 3) + 1;
            var useCache = runTime > (_maxDelay * 3) + 2;
            if (fastPath)
            {
                if (_miss > 1)
                {
                    if (_idle && _miss % 120 == 0) _idle = false;
                    else _idle = true;

                    //Log.Line($"{t.System.WeaponName} - idle:{_idle} - miss:{_miss} - runtime:{runTime} - {_idle} - {thisTick}");
                    if (_idle) return true;
                }
                RequestTick = thisTick;
                MyAPIGateway.Physics.CastRayParallel(ref lineTest.From, ref lineTest.To, CollisionLayers.VoxelCollisionLayer, Results);
            }
            //if (!useCache) Log.Line($"not using cache: {t.System.WeaponName} - miss:{_miss} - runTime:{runTime} - dist:{dist} - newDir:{_endDir} - oldDir:{oldDir}");
            return useCache;
        }

        internal void Results(IHitInfo info)
        {
            ResultTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);
            if (info == null)
            {
                _miss++;
                HitInfo = null;
                return;
            }

            var voxel = info.HitEntity as MyVoxelBase;
            if (voxel?.RootVoxel is MyPlanet)
            {
                HitInfo = info;
                _miss = 0;
                return;
            }
            _miss++;
            HitInfo = null;
        }

        internal bool NewResult(out IHitInfo cachedPlanetResult)
        {
            cachedPlanetResult = null;
            var thisTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);

            if (HitInfo == null)
            {
                _miss++;
                return false;
            }

            if (thisTick > RequestTick + _maxDelay)
                return false;

            //Log.Line($"newResult: {thisTick} - {RequestTick} - {_maxDelay} - {RequestTick + _maxDelay} - {thisTick - (RequestTick + _maxDelay)}");
            cachedPlanetResult = HitInfo;
            return true;
        }
    }

    internal class WeaponFrameCache
    {
        internal bool VirtualHit;
        internal int Hits;
        internal double HitDistance;
        internal HitEntity HitEntity = new HitEntity();
        internal IMySlimBlock HitBlock;
        internal int VirutalId = -1;
        internal VoxelParallelHits[] VoxelHits;

        internal WeaponFrameCache(int size)
        {
            VoxelHits = new VoxelParallelHits[size];
            for (int i = 0; i < size; i++) VoxelHits[i] = new VoxelParallelHits();
        }
    }

    internal class Fragments
    {
        internal List<Fragment> Sharpnel = new List<Fragment>();

        internal void Init(Projectile p, MyConcurrentPool<Fragment> fragPool)
        {
            for (int i = 0; i < p.Info.System.Values.Ammo.Shrapnel.Fragments; i++)
            {
                var frag = fragPool.Get();

                frag.System = p.Info.System;
                frag.Ai = p.Info.Ai;
                frag.Target = p.Info.Target.Entity;
                frag.WeaponId = p.Info.WeaponId;
                frag.MuzzleId = p.Info.MuzzleId;
                frag.FiringCube = p.Info.Target.FiringCube;
                frag.Guidance = p.Info.EnableGuidance;
                frag.Origin = p.Position;
                frag.OriginUp = p.Info.OriginUp;
                frag.PredictedTargetPos = p.PredictedTargetPos;
                frag.Velocity = p.Velocity;
                var dirMatrix = Matrix.CreateFromDir(p.Direction);
                var shape = p.Info.System.Values.Ammo.Shrapnel.Shape;
                float neg;
                float pos;
                switch (shape)
                {
                    case Shrapnel.ShrapnelShape.Cone:
                        neg = 0;
                        pos = 15;
                        break;
                    case Shrapnel.ShrapnelShape.HalfMoon:
                        neg = 90;
                        pos = 90;
                        break;
                    default:
                        neg = 180;
                        pos = 180;
                        break;
                }
                var negValue = MathHelper.ToRadians(neg);
                var posValue = MathHelper.ToRadians(pos);
                var randomFloat1 = MyUtils.GetRandomFloat(-negValue, posValue);
                var randomFloat2 = MyUtils.GetRandomFloat(0.0f, MathHelper.TwoPi);

                var shrapnelDir = Vector3.TransformNormal(-new Vector3(
                    MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2),
                    MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2),
                    MyMath.FastCos(randomFloat1)), dirMatrix);

                frag.Direction = shrapnelDir;

                Sharpnel.Add(frag);
            }
        }

        internal void Spawn()
        {
            Session session = null;
            for (int i = 0; i < Sharpnel.Count; i++)
            {
                var frag = Sharpnel[i];
                session = frag.Ai.Session;
                Projectile p;
                frag.Ai.Session.Projectiles.ProjectilePool.AllocateOrCreate(out p);
                p.Info.System = frag.System;
                p.Info.Ai = frag.Ai;
                p.Info.Target.Entity = frag.Target;
                p.Info.Target.FiringCube = frag.FiringCube;
                p.Info.IsShrapnel = true;
                p.Info.EnableGuidance = frag.Guidance;
                p.Info.WeaponId = frag.WeaponId;
                p.Info.MuzzleId = frag.MuzzleId;
                p.Info.Origin = frag.Origin;
                p.Info.OriginUp = frag.OriginUp;
                p.PredictedTargetPos = frag.PredictedTargetPos;
                p.Direction = frag.Direction;
                p.State = Projectiles.Projectile.ProjectileState.Start;

                p.StartSpeed = frag.Velocity;
                frag.Ai.Session.Projectiles.FragmentPool.Return(frag);
            }

            session?.Projectiles.ShrapnelPool.Return(this);
            Sharpnel.Clear();
        }
    }

    internal class Fragment
    {
        public WeaponSystem System;
        public GridAi Ai;
        public MyEntity Target;
        public MyCubeBlock FiringCube;
        public Vector3D Origin;
        public Vector3D OriginUp;
        public Vector3D Direction;
        public Vector3D Velocity;
        public Vector3D PredictedTargetPos;
        public int WeaponId;
        public int MuzzleId;
        internal bool Guidance;
    }
    /*
    public class Shrinking
    {
        internal WeaponSystem System;
        internal readonly Stack<AfterGlow> Glowers = new Stack<AfterGlow>();
        internal ProInfo Info;
        internal Vector3D HitPos;
        internal Vector3D BackOfTracer;
        internal Vector3D Direction;
        internal Vector3D ShooterVel;
        internal Vector3D ShooterDisp;
        internal double ResizeLen;
        internal double TracerSteps;
        internal double TracerLength;
        internal float LineScaler;
        internal float Thickness;
        internal int TailSteps;

        internal void Init(ProInfo i, float thickness)
        {
            System = i.System;
            Info = i;
            Thickness = thickness;
            HitPos = i.Position;
            Direction = i.Direction;
            ShooterVel = i.ShooterVel;
            //LineScaler = i.LineScaler;
            ResizeLen = (i.DistanceTraveled - i.PrevDistanceTraveled);
            TracerLength = i.Length;
            TracerSteps = i.System.TracerLength / ResizeLen;
            var frontOfTracer = (Info.LineStart + (Direction * ResizeLen));
            var tracerLength = Info.System.TracerLength;
            BackOfTracer = frontOfTracer + (-Direction * (tracerLength + ResizeLen));
        }

        internal Shrunk? GetLine()
        {
            if (TracerSteps-- > 0)
            {
                var stepLength = ResizeLen;

                BackOfTracer += ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
                HitPos += ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
                var backOfTail = BackOfTracer + (Direction * (TailSteps++ * stepLength));
                var newTracerBack = HitPos + -(Direction * TracerSteps * stepLength);
                var reduced = TracerSteps * stepLength;
                if (TracerSteps < 0) stepLength = Vector3D.Distance(backOfTail, HitPos);

                return new Shrunk(ref newTracerBack, ref backOfTail, reduced, stepLength);
            }
            return null;
        }

        internal void Clean()
        {
            System = null;
            Glowers.Clear();
            if (Info != null)
            {
                //Info.Shrinking = false;
                Info = null;
            }
        }
    }

    internal struct Shrunk
    {
        internal readonly Vector3D PrevPosition;
        internal readonly Vector3D BackOfTail;
        internal readonly double Reduced;
        internal readonly double StepLength;

        internal Shrunk(ref Vector3D prevPosition, ref Vector3D backOfTail, double reduced, double stepLength)
        {
            PrevPosition = prevPosition;
            BackOfTail = backOfTail;
            Reduced = reduced;
            StepLength = stepLength;
        }
    }
    */
    public struct RadiatedBlock
    {
        public Vector3I Center;
        public IMySlimBlock Slim;
        public Vector3I Position;
    }

    public class FatMap
    {
        public ConcurrentCachingList<MyCubeBlock> MyCubeBocks;
        public MyGridTargeting Targeting;
        public volatile bool Trash;
        public int MostBlocks;
    }

    public class Focus
    {
        internal Focus(int count)
        {
            Target = new MyEntity[count];
            SubSystem = new BlockTypes[count];
            TargetState = new TargetStatus[count];
            PrevTargetId = new long[count];
            for (int i = 0; i < TargetState.Length; i++)
                TargetState[i] = new TargetStatus();
        }

        internal readonly BlockTypes[] SubSystem;
        internal TargetStatus[] TargetState;
        internal readonly long[] PrevTargetId;
        internal MyEntity[] Target;
        internal int ActiveId;
        internal bool HasFocus;

        internal int FocusSlots()
        {
            return Target.Length;
        }

        internal void AddFocus(MyEntity target, GridAi ai)
        {
            Target[ActiveId] = target;
            ai.TargetResetTick = ai.Session.Tick + 1;
            foreach (var sub in ai.SubGrids)
            {
                GridAi gridAi;
                if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                {
                    gridAi.Focus.Target[ActiveId] = target;
                    gridAi.TargetResetTick = ai.Session.Tick + 1;
                }
            }
        }

        internal bool ReassignTarget(MyEntity target, int focusId, GridAi ai)
        {
            if (focusId >= Target.Length) return false;
            Target[focusId] = target;
            foreach (var sub in ai.SubGrids)
            {
                GridAi gridAi;
                if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                    gridAi.Focus.Target[focusId] = Target[ActiveId];
            }
            return true;
        }

        internal void NextActive(bool addSecondary, GridAi ai)
        {
            var prevId = ActiveId;
            var newActiveId = prevId;
            if (newActiveId + 1 > Target.Length - 1) newActiveId -= 1;
            else newActiveId += 1;

            if (addSecondary && Target[newActiveId] == null)
            {
                Target[newActiveId] = Target[prevId];
                ActiveId = newActiveId;
                foreach (var sub in ai.SubGrids)
                {
                    GridAi gridAi;
                    if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                        gridAi.Focus.Target[newActiveId] = Target[ActiveId];
                }
            }
            else if (!addSecondary && Target[newActiveId] != null)
                ActiveId = newActiveId;
        }

        internal bool IsFocused(GridAi ai)
        {
            HasFocus = false;
            for (int i = 0; i < Target.Length; i++)
            {
                if (Target[i] != null)
                {
                    if (!Target[i].MarkedForClose) HasFocus = true;
                    else
                    {
                        Target[i] = null;
                        foreach (var sub in ai.SubGrids)
                        {
                            GridAi gridAi;
                            if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                                gridAi.Focus.Target[i] = null;
                        }
                    }
                }

                if (Target[0] == null && HasFocus)
                {
                    Target[0] = Target[i];
                    Target[i] = null;
                    ActiveId = 0;

                    foreach (var sub in ai.SubGrids)
                    {
                        GridAi gridAi;
                        if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                        {
                            gridAi.Focus.Target[0] = Target[i];
                            gridAi.Focus.Target[i] = null;
                        }
                    }
                }
            }
            return HasFocus;
        }

        internal void ReleaseActive(GridAi ai)
        {
            Target[ActiveId] = null;

            foreach (var sub in ai.SubGrids)
            {
                GridAi gridAi;
                if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                    gridAi.Focus.Target[ActiveId] = null;
            }
        }

        internal void Clean()
        {
            Target = null;
            TargetState = null;
        }
    }

    public class TargetStatus
    {
        public int ShieldHealth;
        public int ThreatLvl;
        public int Size;
        public int Speed;
        public int Distance;
        public int Engagement;
    }

    public class IconInfo
    {
        private readonly MyStringId _textureName;
        private readonly Vector2D _screenPosition;
        private readonly double _definedScale;
        private readonly int _slotId;
        private readonly bool _canShift;
        private readonly float[] _adjustedScale;
        private readonly Vector3D[] _positionOffset;
        private readonly Vector3D[] _altPositionOffset;
        private readonly int[] _prevSlotId;

        public IconInfo(MyStringId textureName, double definedScale, Vector2D screenPosition, int slotId, bool canShift)
        {
            _textureName = textureName;
            _definedScale = definedScale;
            _screenPosition = screenPosition;
            _slotId = slotId;
            _canShift = canShift;
            _prevSlotId = new int[2];
            for (int i = 0; i < _prevSlotId.Length; i++)
                _prevSlotId[i] = -1;

            _adjustedScale = new float[2];
            _positionOffset = new Vector3D[2];
            _altPositionOffset = new Vector3D[2];
        }

        public void GetTextureInfo(int index, int displayCount, bool altPosition, Session session, out MyStringId textureName, out float scale, out Vector3D offset)
        {
            if (displayCount != _prevSlotId[index]) InitOffset(index, displayCount);
            textureName = _textureName;
            scale = _adjustedScale[index];
            offset = !altPosition ? Vector3D.Transform(_positionOffset[index], session.CameraMatrix) : Vector3D.Transform(_altPositionOffset[index], session.CameraMatrix);
            _prevSlotId[index] = displayCount;
        }

        private void InitOffset(int index, int displayCount)
        {
            var fov = MyAPIGateway.Session.Camera.FovWithZoom;
            var screenScale = 0.075 * Math.Tan(fov * 0.5);
            const float slotSpacing = 0.06f;
            var needShift = _slotId != displayCount;
            var shiftSize = _canShift && needShift ? -(slotSpacing * (_slotId - displayCount)) : 0;
            var position = new Vector3D(_screenPosition.X + shiftSize - (index * 0.45), _screenPosition.Y, 0);
            var altPosition = new Vector3D(_screenPosition.X + shiftSize - (index * 0.45), _screenPosition.Y - 0.75, 0);

            double aspectratio = MyAPIGateway.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;

            position.X *= screenScale * aspectratio;
            position.Y *= screenScale;
            altPosition.X *= screenScale * aspectratio;
            altPosition.Y *= screenScale;
            _adjustedScale[index] = (float)(_definedScale * screenScale);
            _positionOffset[index] = new Vector3D(position.X, position.Y, -.1);
            _altPositionOffset[index] = new Vector3D(altPosition.X, altPosition.Y, -.1);
        }
    }

    internal class GroupInfo
    {
        internal readonly HashSet<WeaponComponent> Comps = new HashSet<WeaponComponent>();

        internal readonly Dictionary<string, int> Settings = new Dictionary<string, int>()
        {
            {"Active", 1},
            {"Neutrals", 0},
            {"Projectiles", 0 },
            {"Biologicals", 0 },
            {"Meteors", 0 },
            {"Friendly", 0},
            {"Unowned", 0},
            {"ManualAim", 0},
            {"ManualFire", 0},
            {"FocusTargets", 0},
            {"FocusSubSystem", 0},
            {"SubSystems", 0},
        };

        internal string Name;
        internal ChangeStates ChangeState;
        internal enum ChangeStates
        {
            None,
            Add,
            Modify
        }

        internal void ApplySettings()
        {
            foreach (var comp in Comps)
            {
                var o = comp.Set.Value.Overrides;
                foreach (var setting in Settings)
                {
                    switch (setting.Key)
                    {
                        case "Active":
                            o.Activate = setting.Value > 0;
                            break;
                        case "SubSystems":
                            o.SubSystem = (BlockTypes)setting.Value;
                            break;
                        case "FocusSubSystem":
                            o.FocusSubSystem = setting.Value > 0;
                            break;
                        case "FocusTargets":
                            o.FocusTargets = setting.Value > 0;
                            break;
                        case "ManualFire":
                            o.ManualFire = setting.Value > 0;
                            break;
                        case "ManualAim":
                            o.ManualAim = setting.Value > 0;
                            break;
                        case "Unowned":
                            o.Unowned = setting.Value > 0;
                            break;
                        case "Friendly":
                            o.Friendly = setting.Value > 0;
                            break;
                        case "Meteors":
                            o.Meteors = setting.Value > 0;
                            break;
                        case "Biologicals":
                            o.Biologicals = setting.Value > 0;
                            break;
                        case "Projectiles":
                            o.Projectiles = setting.Value > 0;
                            break;
                        case "Neutrals":
                            o.Neutrals = setting.Value > 0;
                            break;
                    }
                }
            }
        }

        internal void SetValue(WeaponComponent comp, int value)
        {
            var o = comp.Set.Value.Overrides;
            foreach (var setting in Settings.Keys)
            {
                switch (setting)
                {
                    case "Active":
                        o.Activate = value > 0;
                        break;
                    case "SubSystems":
                        o.SubSystem = (BlockTypes)value;
                        break;
                    case "FocusSubSystem":
                        o.FocusSubSystem = value > 0;
                        break;
                    case "FocusTargets":
                        o.FocusTargets = value > 0;
                        break;
                    case "ManualFire":
                        o.ManualFire = value > 0;
                        break;
                    case "ManualAim":
                        o.ManualAim = value > 0;
                        break;
                    case "Unowned":
                        o.Unowned = value > 0;
                        break;
                    case "Friendly":
                        o.Friendly = value > 0;
                        break;
                    case "Meteors":
                        o.Meteors = value > 0;
                        break;
                    case "Biologicals":
                        o.Biologicals = value > 0;
                        break;
                    case "Projectiles":
                        o.Projectiles = value > 0;
                        break;
                    case "Neutrals":
                        o.Neutrals = value > 0;
                        break;
                }
            }
        }

        internal int GetCompSetting(string setting, WeaponComponent comp)
        {
            var value = 0;
            var o = comp.Set.Value.Overrides;
            switch (setting)
            {
                case "Active":
                    value = o.Activate ? 1 : 0;
                    break;
                case "SubSystems":
                    value = (int)o.SubSystem;
                    break;
                case "FocusSubSystem":
                    value = o.FocusSubSystem ? 1 : 0;
                    break;
                case "FocusTargets":
                    value = o.FocusTargets ? 1 : 0;
                    break;
                case "ManualFire":
                    value = o.ManualFire ? 1 : 0;
                    break;
                case "ManualAim":
                    value = o.ManualAim ? 1 : 0;
                    break;
                case "Unowned":
                    value = o.Unowned ? 1 : 0;
                    break;
                case "Friendly":
                    value = o.Friendly ? 1 : 0;
                    break;
                case "Meteors":
                    value = o.Meteors ? 1 : 0;
                    break;
                case "Biologicals":
                    value = o.Biologicals ? 1 : 0;
                    break;
                case "Projectiles":
                    value = o.Projectiles ? 1 : 0;
                    break;
                case "Neutrals":
                    value = o.Neutrals ? 1 : 0;
                    break;
            }
            return value;
        }
    }
}
