using Sandbox.Game.AI;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.NewProjectile;
using static WeaponCore.Support.PartDefinition.AmmoDef.TrajectoryDef;

namespace WeaponCore.Projectiles
{
    public partial class Projectiles
    {
        private void GenProjectiles()
        {
            for (int i = 0; i < NewProjectiles.Count; i++)
            {
                var gen = NewProjectiles[i];
                var u = gen.Muzzle.Part;
                var a = gen.AmmoDef;
                var t = gen.Type;
                var virts = gen.NewVirts;
                var muzzle = gen.Muzzle;
                var firingPlayer =  u.Comp.Data.Repo.Base.State.PlayerId == u.Comp.Session.PlayerId || u.ClientStaticShot;
                u.ClientStaticShot = false;

                var patternCycle = gen.PatternCycle;
                var targetable = u.ActiveAmmoDef.AmmoDef.Health > 0 && !u.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon;
                var p = Session.Projectiles.ProjectilePool.Count > 0 ? Session.Projectiles.ProjectilePool.Pop() : new Projectile();
                p.Info.Id = Session.Projectiles.CurrentProjectileId++;
                p.Info.System = u.System;
                p.Info.Ai = u.Comp.Ai;
                p.Info.IsFiringPlayer = firingPlayer;
                p.Info.ClientSent = t == Kind.Client;
                p.Info.AmmoDef = a;
                p.Info.Overrides = u.Comp.Data.Repo.Base.Set.Overrides;
                p.Info.Target.TargetEntity = t != Kind.Client ? u.Target.TargetEntity : gen.TargetEnt;
                p.Info.Target.Projectile = u.Target.Projectile;
                p.Info.Target.IsProjectile = u.Target.Projectile != null;
                p.Info.Target.IsFakeTarget = u.Comp.Data.Repo.Base.State.TrackingReticle;
                p.Info.Target.CoreEntity = u.Comp.CoreEntity;
                p.Info.Target.CoreCube = u.Comp.Cube;
                p.Info.Target.CoreParent = u.Comp.TopEntity;
                p.Info.Target.CoreIsCube = u.Comp.Cube != null;

                p.Info.DummyTarget = u.Comp.Data.Repo.Base.State.TrackingReticle ? u.Comp.Session.PlayerDummyTargets[u.Comp.Data.Repo.Base.State.PlayerId] : null;

                p.Info.WeaponId = u.WeaponId;
                p.Info.BaseDamagePool = a == u.ActiveAmmoDef.AmmoDef ? u.BaseDamage : a.BaseDamage;
                p.Info.EnableGuidance = u.Comp.Data.Repo.Base.Set.Guidance;
                p.Info.WeaponCache = u.WeaponCache;
                p.Info.WeaponRng = u.TargetData.WeaponRandom;
                p.Info.LockOnFireState = u.LockOnFireState;
                p.Info.ShooterVel = u.Comp.Ai.GridVel;

                p.Info.OriginUp = t != Kind.Client ? u.MyPivotUp : gen.OriginUp;
                p.Info.MaxTrajectory = t != Kind.Client ? a.Const.MaxTrajectoryGrows && u.FireCounter < a.Trajectory.MaxTrajectoryTime ? a.Const.TrajectoryStep * u.FireCounter : a.Const.MaxTrajectory : gen.MaxTrajectory;
                p.Info.MuzzleId = t != Kind.Virtual ? muzzle.MuzzleId : -1;
                p.Info.UniqueMuzzleId = muzzle.UniqueId;
                p.Info.WeaponCache.VirutalId = t != Kind.Virtual ? -1 : p.Info.WeaponCache.VirutalId;
                p.Info.Origin = t != Kind.Client ? t != Kind.Virtual ? muzzle.Position : u.MyPivotPos : gen.Origin;
                p.Info.Direction = t != Kind.Client ? t != Kind.Virtual ? gen.Direction : u.MyPivotFwd : gen.Direction;
                if (t == Kind.Client) p.Velocity = gen.Velocity;

                float shotFade;
                if (a.Const.HasShotFade && !a.Const.VirtualBeams)
                {
                    if (patternCycle > a.AmmoGraphics.Lines.Tracer.VisualFadeStart)
                        shotFade = MathHelper.Clamp(((patternCycle - a.AmmoGraphics.Lines.Tracer.VisualFadeStart)) * a.Const.ShotFadeStep, 0, 1);
                    else if (u.System.DelayCeaseFire && u.CeaseFireDelayTick != Session.Tick)
                        shotFade = MathHelper.Clamp(((Session.Tick - u.CeaseFireDelayTick) - a.AmmoGraphics.Lines.Tracer.VisualFadeStart) * a.Const.ShotFadeStep, 0, 1);
                    else shotFade = 0;
                }
                else shotFade = 0;
                p.Info.ShotFade = shotFade;
                p.PredictedTargetPos = u.Target.TargetPos;
                p.DeadSphere.Center = u.MyPivotPos;
                p.DeadSphere.Radius = u.Comp.Ai.DeadSphereRadius;

                if (a.Const.FeelsGravity && u.System.Session.Tick - u.GravityTick > 60)
                {
                    u.GravityTick = u.System.Session.Tick;
                    float interference;
                    u.GravityPoint = Session.Physics.CalculateNaturalGravityAt(p.Position, out interference);
                }

                p.Gravity = u.GravityPoint;

                if (t != Kind.Virtual)
                {
                    p.Info.PrimeEntity = a.Const.PrimeModel ? a.Const.PrimeEntityPool.Get() : null;
                    p.Info.TriggerEntity = a.Const.TriggerModel ? Session.TriggerEntityPool.Get() : null;

                    if (targetable)
                        Session.Projectiles.AddTargets.Add(p);
                }
                else
                {
                    p.Info.WeaponCache.VirtualHit = false;
                    p.Info.WeaponCache.Hits = 0;
                    p.Info.WeaponCache.HitEntity.Entity = null;
                    for (int j = 0; j < virts.Count; j++)
                    {
                        var v = virts[j];
                        p.VrPros.Add(v.Info);
                        if (!a.Const.RotateRealBeam) p.Info.WeaponCache.VirutalId = 0;
                        else if (v.Rotate)
                        {
                            p.Info.Origin = v.Muzzle.Position;
                            p.Info.Direction = v.Muzzle.Direction;
                            p.Info.WeaponCache.VirutalId = v.VirtualId;
                        }
                    }
                    virts.Clear();
                    VirtInfoPools.Return(virts);
                }

                Session.Projectiles.ActiveProjetiles.Add(p);
                p.Start();

                p.Info.Monitors = u.Monitors;
                if (p.Info.Monitors?.Count > 0) {
                    Session.MonitoredProjectiles[p.Info.Id] = p;
                    for (int j = 0; j < p.Info.Monitors.Count; j++)
                        p.Info.Monitors[j].Invoke(u.Comp.CoreEntity.EntityId, u.WeaponId, p.Info.Id, p.Info.Target.TargetId, p.Position, true);
                }

            }
            NewProjectiles.Clear();
        }

        private void SpawnFragments()
        {
            if (Session.FragmentsNeedingEntities.Count > 0)
                PrepFragmentEntities();

            int spawned = 0;
            for (int j = 0; j < ShrapnelToSpawn.Count; j++)
            {
                int count;
                ShrapnelToSpawn[j].Spawn(out count);
                spawned += count;
            }
            ShrapnelToSpawn.Clear();
            
            if (AddTargets.Count > 0)
                AddProjectileTargets();

            UpdateState(ActiveProjetiles.Count - spawned);
        }

        internal void PrepFragmentEntities()
        {
            for (int i = 0; i < Session.FragmentsNeedingEntities.Count; i++)
            {
                var frag = Session.FragmentsNeedingEntities[i];
                if (frag.AmmoDef.Const.PrimeModel && frag.PrimeEntity == null) frag.PrimeEntity = frag.AmmoDef.Const.PrimeEntityPool.Get();
                if (frag.AmmoDef.Const.TriggerModel && frag.TriggerEntity == null) frag.TriggerEntity = Session.TriggerEntityPool.Get();
            }
            Session.FragmentsNeedingEntities.Clear();
        }

        internal void AddProjectileTargets() // This calls AI late for fragments need to fix
        {
            for (int i = 0; i < AddTargets.Count; i++)
            {
                var p = AddTargets[i];
                for (int t = 0; t < p.Info.Ai.TargetAis.Count; t++)
                {

                    var targetAi = p.Info.Ai.TargetAis[t];
                    var addProjectile = p.Info.AmmoDef.Trajectory.Guidance != GuidanceType.None && targetAi.PointDefense;
                    if (!addProjectile && targetAi.PointDefense)
                    {
                        if (Vector3.Dot(p.Info.Direction, p.Info.Origin - targetAi.TopEntity.PositionComp.WorldMatrixRef.Translation) < 0)
                        {

                            var targetSphere = targetAi.TopEntity.PositionComp.WorldVolume;
                            targetSphere.Radius *= 3;
                            var testRay = new RayD(p.Info.Origin, p.Info.Direction);
                            var quickCheck = Vector3D.IsZero(targetAi.GridVel, 0.025) && targetSphere.Intersects(testRay) != null;

                            if (!quickCheck)
                            {
                                var deltaPos = targetSphere.Center - p.Info.Origin;
                                var deltaVel = targetAi.GridVel - p.Info.Ai.GridVel;
                                var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, p.Info.AmmoDef.Const.DesiredProjectileSpeed);
                                var predictedPos = targetSphere.Center + (float)timeToIntercept * deltaVel;
                                targetSphere.Center = predictedPos;
                            }

                            if (quickCheck || targetSphere.Intersects(testRay) != null)
                                addProjectile = true;
                        }
                    }
                    if (addProjectile)
                    {
                        targetAi.DeadProjectiles.Remove(p);
                        targetAi.LiveProjectile.Add(p);
                        targetAi.LiveProjectileTick = Session.Tick;
                        targetAi.NewProjectileTick = Session.Tick;
                        p.Watchers.Add(targetAi);
                    }
                }
            }
            AddTargets.Clear();
        }
    }
}
