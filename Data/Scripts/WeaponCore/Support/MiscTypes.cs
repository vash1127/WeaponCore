using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage.Collections;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Projectiles;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore.Support
{
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

    internal struct DeferedTypeCleaning
    {
        internal uint RequestTick;
        internal ConcurrentDictionary<BlockTypes, ConcurrentCachingList<MyCubeBlock>> Collection;
    }

    public class FatMap
    {
        public ConcurrentCachingList<MyCubeBlock> MyCubeBocks;
        public MyGridTargeting Targeting;
        public volatile bool Trash;
        public int MostBlocks;
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

}
