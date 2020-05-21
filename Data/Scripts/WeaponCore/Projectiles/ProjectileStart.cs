using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectile;
using static WeaponCore.Support.NewProjectile;
using static WeaponCore.Support.AvShot;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef;

namespace WeaponCore.Projectiles
{
    public partial class Projectiles
    {
        private void GenProjectiles()
        {
            for (int i = 0; i < NewProjectiles.Count; i++)
            {
                var gen = NewProjectiles[i];
                var w = gen.Weapon;
                var a = gen.AmmoDef;
                var t = gen.Type;
                var virts = gen.NewVirts;
                var muzzle = gen.Muzzle;
                var firingPlayer = w.Comp.State.Value.CurrentPlayerControl.PlayerId == w.Comp.Session.PlayerId;
                var patternCycle = gen.PatternCycle;
                var targetable = w.ActiveAmmoDef.AmmoDef.Health > 0 && !w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon;
                var p = Session.Projectiles.ProjectilePool.Count > 0 ? Session.Projectiles.ProjectilePool.Pop() : new Projectile();
                
                p.Info.Id = Session.Projectiles.CurrentProjectileId++;
                p.Info.System = w.System;
                p.Info.Ai = w.Comp.Ai;
                p.Info.IsFiringPlayer = firingPlayer;
                p.Info.ClientSent = t == Kind.Client;
                p.Info.AmmoDef = a;
                p.Info.AmmoInfo = w.AmmoInfos[a.Const.AmmoIdxPos];
                p.Info.Overrides = w.Comp.Set.Value.Overrides;
                p.Info.Target.Entity = t != Kind.Client ? w.Target.Entity : gen.TargetEnt;
                p.Info.Target.Projectile = w.Target.Projectile;
                p.Info.Target.IsProjectile = w.Target.Projectile != null;
                p.Info.Target.IsFakeTarget = w.Comp.TrackReticle;
                p.Info.Target.FiringCube = w.Comp.MyCube;

                p.Info.WeaponId = w.WeaponId;
                p.Info.BaseDamagePool = w.BaseDamage;
                p.Info.EnableGuidance = w.Comp.Set.Value.Guidance;
                p.Info.WeaponCache = w.WeaponCache;
                p.Info.WeaponRng = w.Comp.WeaponValues.WeaponRandom[w.WeaponId];
                p.Info.LockOnFireState = w.LockOnFireState;
                p.Info.ShooterVel = w.Comp.Ai.GridVel;

                p.Info.OriginUp = t != Kind.Client ? w.MyPivotUp : gen.OriginUp;
                p.Info.MaxTrajectory = t != Kind.Client ? a.Const.MaxTrajectoryGrows && w.FireCounter < a.Trajectory.MaxTrajectoryTime ? a.Const.TrajectoryStep * w.FireCounter : a.Const.MaxTrajectory : gen.MaxTrajectory;
                p.Info.MuzzleId = t != Kind.Virtual ? muzzle.MuzzleId : -1;
                p.Info.WeaponCache.VirutalId = t != Kind.Virtual ? -1 : p.Info.WeaponCache.VirutalId;
                p.Info.Origin = t != Kind.Client ? t != Kind.Virtual ? muzzle.Position : w.MyPivotPos : gen.Origin;
                p.Info.Direction = t != Kind.Client ? t != Kind.Virtual ? muzzle.DeviatedDir : w.MyPivotDir : gen.Direction;
                if (t == Kind.Client) p.Velocity = gen.Velocity;

                float shotFade;
                if (a.Const.HasShotFade && !a.Const.VirtualBeams)
                {
                    if (patternCycle > a.AmmoGraphics.Lines.Tracer.VisualFadeStart)
                        shotFade = MathHelper.Clamp(((patternCycle - a.AmmoGraphics.Lines.Tracer.VisualFadeStart)) * a.Const.ShotFadeStep, 0, 1);
                    else if (w.System.DelayCeaseFire && w.CeaseFireDelayTick != Session.Tick)
                        shotFade = MathHelper.Clamp(((Session.Tick - w.CeaseFireDelayTick) - a.AmmoGraphics.Lines.Tracer.VisualFadeStart) * a.Const.ShotFadeStep, 0, 1);
                    else shotFade = 0;
                }
                else shotFade = 0;
                p.Info.ShotFade = shotFade;
                p.PredictedTargetPos = w.Target.TargetPos;
                p.DeadSphere.Center = w.MyPivotPos;
                p.DeadSphere.Radius = w.Comp.Ai.MyGrid.GridSizeHalf + 0.1;

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
                            p.Info.Origin = v.Origin;
                            p.Info.Direction = v.Dir;
                            p.Info.WeaponCache.VirutalId = v.VirtualId;
                        }
                    }
                    virts.Clear();
                    VirtInfoPools.Return(virts);
                }

                Session.Projectiles.ActiveProjetiles.Add(p);
                p.Start();
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

                        if (Vector3.Dot(p.Info.Direction, p.Info.Origin - targetAi.MyGrid.PositionComp.WorldMatrixRef.Translation) < 0)
                        {

                            var targetSphere = targetAi.MyGrid.PositionComp.WorldVolume;
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
