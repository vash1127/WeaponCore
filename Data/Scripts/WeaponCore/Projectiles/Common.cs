using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private void GetEntitiesInBlastRadius(Fired fired, Vector3D position, int poolId)
        {
            var entCheckList = CheckPool[poolId].Get();
            var entsFound = CheckPool[poolId].Get();
            var sphere = new BoundingSphereD(position, fired.System.Values.Ammo.AreaEffectRadius);
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entCheckList);
            foreach (var ent in entCheckList)
            {
                var blastLine = new LineD(position, ent.PositionComp.WorldAABB.Center);
                GetAllEntitiesInLine(entsFound, fired, blastLine, null, entCheckList);
            }
            entCheckList.Clear();
            CheckPool[poolId].Return(entCheckList);
            if (fired.System.Values.Ammo.DetonateOnEnd || entsFound.Count > 0)
                Hits.Enqueue(new ProximityEvent(fired, null, position, Session.Instance.SApi));

            foreach (var ent in entsFound)
            {
                if (Session.Instance.IsServer)
                    Hits.Enqueue(new ProximityEvent(fired, ent, position, Session.Instance.SApi));
            }
            entsFound.Clear();
            CheckPool[poolId].Return(entsFound);
        }

        internal void GetAllEntitiesInLine(List<MyEntity> ents, Fired fired, LineD beam, List<MyLineSegmentOverlapResult<MyEntity>> segmentList,  List<MyEntity> entList)
        {
            var listCnt = segmentList?.Count ?? entList.Count;
            for (int i = 0; i < listCnt; i++)
            {
                var ent = segmentList != null ? segmentList[i].Element : entList[i];
                if (ent == fired.FiringCube.CubeGrid) continue;
                //if (fired.Age < 30 && ent.PositionComp.WorldAABB.Intersects(fired.ReverseOriginRay).HasValue) continue;
                var shieldBlock = Session.Instance.SApi?.MatchEntToShieldFast(ent, true);
                if (shieldBlock != null)
                {
                    if (ent.Physics == null && shieldBlock.CubeGrid != fired.FiringCube.CubeGrid)
                        ents.Add((MyEntity) shieldBlock);
                    else continue;
                }

                var extFrom = beam.From - (beam.Direction * (ent.PositionComp.WorldVolume.Radius * 2));
                var extBeam = new LineD(extFrom, beam.To);
                var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                if (obb.Intersects(ref extBeam) == null) continue;

                if (ent.Physics != null && (ent is MyCubeGrid || ent is MyVoxelBase || ent is IMyDestroyableObject))
                    ents.Add(ent);
            }
        }

        internal HitInfo GetHitEntities(List<MyEntity> ents, Fired fired, LineD beam)
        {
            double nearestDist = double.MaxValue;
            HitInfo nearestHit = new HitInfo();
            foreach (var checkPair in ents)
            {
                var ent = checkPair;
                var shield = ent as IMyTerminalBlock;
                var grid = ent as IMyCubeGrid;
                var voxel = ent as MyVoxelBase;
                if (shield != null)
                {
                    var hitPos = Session.Instance.SApi.LineIntersectShield(shield, beam);
                    if (hitPos != null)
                    {
                        var dist = Vector3D.Distance(hitPos.Value, beam.From);
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestHit = new HitInfo((Vector3D)hitPos, new LineD(beam.From, (Vector3D)hitPos), shield, null);
                        }
                    }
                }
                else if (grid != null)
                {
                    var pos3I = grid.RayCastBlocks(beam.From, beam.To);
                    if (pos3I != null)
                    {
                        var firstBlock = grid.GetCubeBlock(pos3I.Value);
                        Vector3D center;
                        firstBlock.ComputeWorldCenter(out center);

                        Vector3 halfExt;
                        firstBlock.ComputeScaledHalfExtents(out halfExt);

                        var blockBox = new BoundingBoxD(-halfExt, halfExt);
                        var rotMatrix = Quaternion.CreateFromRotationMatrix(firstBlock.CubeGrid.WorldMatrix);
                        var obb = new MyOrientedBoundingBoxD(center, blockBox.HalfExtents, rotMatrix);
                        if (obb.Intersects(ref beam) == null) Log.Line("how???");
                        var dist = obb.Intersects(ref beam) ?? Vector3D.Distance(beam.From, center);
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            var hitPos = beam.From + (beam.Direction * (double)dist);
                            nearestHit = new HitInfo(hitPos, new LineD(beam.From, hitPos), null, firstBlock);
                        }
                    }
                }
                else if (voxel != null)
                {
                    Vector3D? t;
                    voxel.GetIntersectionWithLine(ref beam, out t, true, IntersectionFlags.DIRECT_TRIANGLES);
                    if (t != null) Log.Line($"voxel hit: {t.Value}");
                    /*
                    {
                        var hitInfoRet = new MyHitInfo
                        {
                            Position = t.Value,
                        };
                    }
                    */
                }
                else if (ent is IMyDestroyableObject)
                {
                    var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.PositionComp.WorldMatrix);
                    var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                    var dist = obb.Intersects(ref beam);
                    if (dist != null && dist < nearestDist)
                    {
                        nearestDist = dist.Value;
                        var hitPos = beam.From + (beam.Direction * (double)dist);
                        nearestHit = new HitInfo(hitPos, new LineD(beam.From, hitPos), ent, null);
                    }
                }
            }
            return nearestHit;
        }

        internal bool GetDamageInfo(
            Fired fired,
            MyEntity entity,
            MatrixD entityMatrix,
            LineD beam, 
            HitInfo hitInfo, 
            MyConcurrentDictionary<IMyEntity, DamageInfo> hitEnts, 
            MyConcurrentDictionary<IMySlimBlock, DamageInfo> hitBlocks, 
            MyConcurrentPool<DamageInfo> damagePool, 
            int beamId, 
            bool draw)
        {
            if (hitInfo.HitPos != Vector3D.Zero)
            {
                DamageInfo damageInfo = null;
                if (hitInfo.Slim != null)
                {
                    hitBlocks.TryGetValue(hitInfo.Slim, out damageInfo);
                    if (damageInfo == null) damageInfo = damagePool.Get();
                    damageInfo.Update(beamId, hitInfo.NewBeam, null, hitInfo.Slim, hitInfo.HitPos, 1);
                    hitBlocks[hitInfo.Slim] = damageInfo;
                }
                else
                {
                    hitEnts.TryGetValue(hitInfo.Entity, out damageInfo);
                    if (damageInfo == null) damageInfo = damagePool.Get();
                    damageInfo.Update(beamId, hitInfo.NewBeam, hitInfo.Entity, null, hitInfo.HitPos, 1);
                    hitEnts[hitInfo.Entity] = damageInfo;
                }
                return true;
            }
            if (draw && !Session.Instance.DedicatedServer) Session.Instance.DrawBeams.Enqueue(new DrawProjectile(ref fired, entity, entityMatrix, beamId, beam, Vector3D.Zero, Vector3D.Zero, null, false, 0,0, false, false, false));
            return false;
        }

        internal void DamageEntities(
            Fired fired, 
            MyConcurrentDictionary<IMyEntity, DamageInfo> hitEnts,
            MyConcurrentDictionary<IMySlimBlock, DamageInfo> hitBlocks,
            MyConcurrentPool<DamageInfo> damagePool)
        {
            foreach (var pair in hitBlocks)
            {
                var info = pair.Value;
                info.HitPos /= info.HitCount;

                if (Session.Instance.IsServer) Hits.Enqueue(new GridEvent(pair.Key, info.HitPos, pair.Value.HitCount, fired));
                info.Clean();
                damagePool.Return(info);
            }

            foreach (var pair in hitEnts)
            {
                var ent = pair.Key;
                var shield = ent as IMyTerminalBlock;
                var voxel = ent as MyVoxelBase;
                var destroyable = ent as IMyDestroyableObject;

                var info = pair.Value;
                info.HitPos /= info.HitCount;

                if (Session.Instance.IsServer)
                {
                    if (shield != null) Hits.Enqueue(new ShieldEvent(shield, Session.Instance.SApi, info.HitPos / info.HitCount, info.HitCount, fired));
                    if (voxel != null) Hits.Enqueue(new VoxelEvent());
                    if (destroyable != null) Hits.Enqueue(new DestroyableEvent(destroyable, fired));
                }
                info.Clean();
                damagePool.Return(info);
            }
            hitBlocks.Clear();
            hitEnts.Clear();
        }

        internal struct DrawProjectile
        {
            internal readonly Fired Fired;
            internal readonly int ProjectileId;
            internal readonly MyEntity Entity;
            internal readonly MatrixD EntityMatrix;
            internal readonly LineD Projectile;
            internal readonly Vector3D Speed;
            internal readonly Vector3D? HitPos;
            internal readonly IMyEntity HitEntity;
            internal readonly bool PrimeProjectile;
            internal readonly double LineReSizeLen;
            internal readonly float LineWidth;
            internal readonly int ReSizeSteps;
            internal readonly bool Shrink;
            internal readonly bool Last;
            internal readonly bool OnScreen;
            internal readonly Vector4 Color;

            internal DrawProjectile(ref Fired fired, MyEntity entity, MatrixD entityMatrix, int projectileId, LineD projectile, Vector3D speed, Vector3D? hitPos, IMyEntity hitEntity, bool primeProjectile, double lineReSizeLen, int reSizeSteps, bool shrink, bool last, bool onScreen)
            {
                Fired = fired;
                Entity = entity;
                EntityMatrix = entityMatrix;
                ProjectileId = projectileId;
                Projectile = projectile;
                Speed = speed;
                HitPos = hitPos;
                HitEntity = hitEntity;
                PrimeProjectile = primeProjectile;
                LineReSizeLen = lineReSizeLen;
                ReSizeSteps = reSizeSteps;
                Shrink = shrink;
                Last = last;
                OnScreen = onScreen;
                var color = Fired.System.Values.Graphics.Line.Color;
                if (Fired.System.LineColorVariance)
                {
                    var cv = Fired.System.Values.Graphics.Line.ColorVariance;
                    var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                    color.X *= randomValue;
                    color.Y *= randomValue;
                    color.Z *= randomValue;
                }

                var width = Fired.System.Values.Graphics.Line.Width;
                if (Fired.System.LineWidthVariance)
                {
                    var wv = Fired.System.Values.Graphics.Line.WidthVariance;
                    var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                    width += randomValue;
                }

                LineWidth = width;
                Color = color;
            }
        }

        internal struct HitInfo
        {
            public readonly Vector3D HitPos;
            public readonly LineD NewBeam;
            public readonly IMyEntity Entity;
            public readonly IMySlimBlock Slim;
            public HitInfo(Vector3D hitPos, LineD newBeam, IMyEntity entity, IMySlimBlock slim)
            {
                HitPos = hitPos;
                NewBeam = newBeam;
                Entity = entity;
                Slim = slim;
            }
        }

        internal struct Fired
        {
            public readonly List<LineD> Shots;
            public readonly WeaponSystem System;
            public readonly MyCubeBlock FiringCube;
            public readonly RayD ReverseOriginRay;
            public readonly Vector3D Direction;
            public readonly int WeaponId;
            public readonly int MuzzleId;
            public readonly bool IsBeam;

            public readonly int Age;



            public Fired(WeaponSystem system, List<LineD> shots, MyCubeBlock firingCube, RayD reverseOriginRay, Vector3D direction, int weaponId, int muzzleId, bool isBeam,  int age)
            {
                System = system;
                Shots = shots;
                FiringCube = firingCube;
                ReverseOriginRay = reverseOriginRay;
                Direction = direction;
                WeaponId = weaponId;
                MuzzleId = muzzleId;
                IsBeam = isBeam;
                Age = age;
            }
        }

        internal class DamageInfo
        {
            public readonly Dictionary<int, LineD> AllBeams = new Dictionary<int, LineD>();
            public LineD UpdatedBeam;
            public IMyEntity Entity;
            public IMySlimBlock Slim;
            public Vector3D HitPos;
            public int HitCount;
            public bool PrimeShot = true;
            public void Update(int beamId, LineD updatedBeam, IMyEntity entity, IMySlimBlock slim, Vector3D hitPos, int hitCount)
            {
                AllBeams.Add(beamId, updatedBeam);
                UpdatedBeam = updatedBeam;
                Entity = entity;
                Slim = slim;
                HitPos += hitPos;
                HitCount += hitCount;
            }

            public bool PrimaryShot()
            {
                if (PrimeShot)
                {
                    PrimeShot = false;
                    return true;
                }
                return false;
            }

            public void Clean()
            {
                AllBeams.Clear();
                PrimeShot = true;
                UpdatedBeam = new LineD();
                Entity = null;
                Slim = null;
                HitPos = Vector3D.Zero;
                HitCount = 0;
            }

            public DamageInfo()
            {
            }
        }

    }
}