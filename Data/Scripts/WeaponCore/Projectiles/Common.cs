using System;
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
using static WeaponCore.Projectiles.Projectiles.HitEntity.Type;
namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private void GetEntitiesInBlastRadius(List<HitEntity> hitList, MyCubeBlock firingCube, WeaponSystem system, Vector3D position, Vector3D direction, int poolId)
        {
            var sphere = new BoundingSphereD(position, system.Values.Ammo.AreaEffectRadius);
            var entityList = MyEntityPool[poolId].Get();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entityList);
            foreach (var ent in entityList)
            {
                var blastLine = new LineD(position, ent.PositionComp.WorldAABB.Center);
                GetAllEntitiesInLine(firingCube, blastLine, null, hitList, poolId, true);
            }
            entityList.Clear();
            MyEntityPool[poolId].Return(entityList);
            if (hitList.Count <= 0)
            {
                var hitEntity = HitEntityPool[poolId].Get();
                hitEntity.Clean();
                hitEntity.EventType = Proximity;
                hitEntity.Hit = false;
                hitEntity.HitPos = position;
                hitList.Add(hitEntity);
                Hits.Enqueue(new Session.DamageEvent(system, direction, hitList, firingCube, poolId));
            }
            else if (Session.Instance.IsServer)
            {
                Hits.Enqueue(new Session.DamageEvent(system, direction, hitList, firingCube, poolId));
            }
        }

        internal void GetAllEntitiesInLine(MyCubeBlock firingCube, LineD beam, List<MyLineSegmentOverlapResult<MyEntity>> segmentList,  List<HitEntity> hitList, int poolId, bool quickCheck = false)
        {
            var listCnt = segmentList?.Count ?? hitList.Count;
            for (int i = 0; i < listCnt; i++)
            {
                var ent = segmentList != null ? segmentList[i].Element : hitList[i].Entity;
                if (ent == firingCube.CubeGrid) continue;
                //if (fired.Age < 30 && ent.PositionComp.WorldAABB.Intersects(fired.ReverseOriginRay).HasValue) continue;
                var shieldBlock = Session.Instance.SApi?.MatchEntToShieldFast(ent, true);
                if (shieldBlock != null)
                {
                    if (ent.Physics == null && shieldBlock.CubeGrid != firingCube.CubeGrid)
                    {
                        var hitEntity = HitEntityPool[poolId].Get();
                        hitEntity.Clean();
                        hitEntity.Entity = (MyEntity)shieldBlock;
                        hitEntity.Beam = beam;
                        hitEntity.EventType = Shield;
                        if (quickCheck)
                        {
                            hitEntity.HitPos = Session.Instance.SApi.LineIntersectShield(shieldBlock, beam);
                            hitEntity.Hit = true;
                        }
                        hitList.Add(hitEntity);
                    }
                    else continue;
                }

                var extFrom = beam.From - (beam.Direction * (ent.PositionComp.WorldVolume.Radius * 2));
                var extBeam = new LineD(extFrom, beam.To);
                var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                var dist = obb.Intersects(ref extBeam);
                if (dist == null && !quickCheck) continue;

                if (ent.Physics != null && (ent is MyCubeGrid || ent is MyVoxelBase || ent is IMyDestroyableObject))
                {
                    var hitEntity = HitEntityPool[poolId].Get();
                    hitEntity.Clean();
                    hitEntity.Entity = ent;
                    hitEntity.Beam = beam;
                    hitEntity.EventType = Grid;
                    if (quickCheck)
                    {
                        var tmpDist = dist ?? 0;
                        hitEntity.HitPos = (beam.From + (beam.Direction * tmpDist));
                        hitEntity.Hit = true;
                    }
                    hitList.Add(hitEntity);
                }
            }
        }

        internal HitEntity GenerateHitInfo(List<HitEntity> ents, int poolId)
        {
            var count = ents.Count;

            if (count > 1) ents.Sort(GetEntityCompareDist);
            else GetEntityCompareDist(ents[0], null);

            var afterSort = ents.Count;
            var endOfIndex = ents.Count - 1;
            var lastValidEntry = endOfIndex;

            for (int i = endOfIndex; i >= 0; i--)
            {
                if (ents[i].Hit)
                {
                    lastValidEntry = i;
                    //Log.Line($"lastValidEntry:{lastValidEntry} - endOfIndex:{endOfIndex}");
                    break;
                }
            }
            var howManyToRemove = endOfIndex - lastValidEntry;
            while (howManyToRemove-- > 0)
            {
                //Log.Line($"removing: {endOfIndex} - hit:{ents[endOfIndex].Hit}");
                var ent = ents[endOfIndex];
                ents.RemoveAt(endOfIndex);
                HitEntityPool[poolId].Return(ent);
                endOfIndex--;
            }
            HitEntity hitEnt = null;
            if (ents.Count > 0)
            {
                hitEnt = ents[0];

                //Log.Line($"start:{count} - ASort:{afterSort} - Final:{ents.Count} - howMany:{howManyToRemove} - hit:{ents[0].Hit} - hitPos:{ents[0].HitPos.HasValue} - {ents[0].Entity.DebugName} ");
            }
            else Log.Line($"no entities to sort");
            return hitEnt;
        }

        internal int GetEntityCompareDist(HitEntity x, HitEntity y)
        {
            var xDist = double.MaxValue;
            var yDist = double.MaxValue;
            var beam = x.Beam;
            var count = y != null ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                var isX = i == 0;

                MyEntity ent;
                HitEntity hitEnt;
                if (isX)
                {
                    hitEnt = x;
                    ent = hitEnt.Entity;
                }
                else
                {
                    hitEnt = y;
                    ent = hitEnt.Entity;
                }
                //hitEnt.Hit = false;

                var shield = ent as IMyTerminalBlock;
                var grid = ent as MyCubeGrid;
                var voxel = ent as MyVoxelBase;

                var dist = double.MaxValue;
                if (shield != null)
                {
                    var hitPos = Session.Instance.SApi.LineIntersectShield(shield, beam);
                    if (hitPos != null)
                    {
                        dist = Vector3D.Distance(hitPos.Value, beam.From);
                        hitEnt.Hit = true;
                        hitEnt.HitPos = hitPos.Value;
                    }
                }
                else if (grid != null)
                {
                    var testBlocks = new List<Vector3I>();
                    grid.RayCastCells(beam.From, beam.To, testBlocks, null, true, true);
                    var closestBlockFound = false;
                    for (int j = 0; j < testBlocks.Count; j++)
                    {
                        var firstBlock = grid.GetCubeBlock(testBlocks[j]) as IMySlimBlock;
                        if (firstBlock != null && !firstBlock.IsDestroyed)
                        {
                            hitEnt.Blocks.Add(firstBlock);
                            if (closestBlockFound) continue;
                            hitEnt.Hit = true;
                            Vector3D center;
                            firstBlock.ComputeWorldCenter(out center);

                            Vector3 halfExt;
                            firstBlock.ComputeScaledHalfExtents(out halfExt);

                            var blockBox = new BoundingBoxD(-halfExt, halfExt);
                            var rotMatrix = Quaternion.CreateFromRotationMatrix(firstBlock.CubeGrid.WorldMatrix);
                            var obb = new MyOrientedBoundingBoxD(center, blockBox.HalfExtents, rotMatrix);
                            dist = obb.Intersects(ref beam) ?? Vector3D.Distance(beam.From, center);

                            hitEnt.HitPos = beam.From + (beam.Direction * dist);
                            closestBlockFound = true;
                        }
                    }
                    //Log.Line($"testBlockCount:{testBlocks.Count} - closestFound:{closestBlockFound}");
                }
                else if (voxel != null)
                {
                    Vector3D? t;
                    voxel.GetIntersectionWithLine(ref beam, out t, true, IntersectionFlags.DIRECT_TRIANGLES);
                    if (t != null)
                    {
                        Log.Line($"voxel hit: {t.Value}");
                        hitEnt.Hit = true;
                        hitEnt.HitPos = beam.From + (beam.Direction * dist);
                    }
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
                    dist = obb.Intersects(ref beam) ?? double.MaxValue;
                    if (dist < double.MaxValue)
                    {
                        hitEnt.Hit = true;
                        hitEnt.HitPos = beam.From + (beam.Direction * dist);
                    }
                }
                else Log.Line($"no hit");

                if (isX) xDist = dist;
                else yDist = dist;
            }
            return xDist.CompareTo(yDist);
        }
        /*
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
                            nearestHit = new HitInfo((Vector3D)hitPos, new LineD(beam.From, (Vector3D)hitPos), (MyEntity)shield, null);
                        }
                    }
                }
                else if (grid != null)
                {
                    var pos3I = grid.RayCastBlocks(beam.From, beam.To);
                    if (pos3I != null)
                    {
                        var firstBlock = grid.GetCubeBlock(pos3I.Value);
                        if (firstBlock.IsDestroyed) continue;
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
                            var hitPos = beam.From + (beam.Direction * dist);
                            nearestHit = new HitInfo(hitPos, new LineD(beam.From, hitPos), null, firstBlock);
                        }
                    }
                }
                else if (voxel != null)
                {
                    Vector3D? t;
                    voxel.GetIntersectionWithLine(ref beam, out t, true, IntersectionFlags.DIRECT_TRIANGLES);
                    if (t != null) Log.Line($"voxel hit: {t.Value}");
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

                if (Session.Instance.IsServer) Hits.Enqueue(new Session.DamageEvent(Session.DamageEvent.Type.Grid, fired.System, info.HitPos, fired.Direction, pair.Value.HitCount, (MyEntity)pair.Key.CubeGrid, pair.Key, fired.FiringCube, ));
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
                    if (shield != null) Hits.Enqueue(new Session.DamageEvent(Session.DamageEvent.Type.Shield,fired.System, info.HitPos, fired.Direction, info.HitCount, (MyEntity)pair.Key, null, fired.FiringCube));
                    if (voxel != null) Hits.Enqueue(new Session.DamageEvent(Session.DamageEvent.Type.Voxel, fired.System, info.HitPos, fired.Direction, info.HitCount, (MyEntity)pair.Key, null, fired.FiringCube));
                    if (destroyable != null) Hits.Enqueue(new Session.DamageEvent(Session.DamageEvent.Type.Destroyable, fired.System, info.HitPos, fired.Direction, info.HitCount, (MyEntity)pair.Key, null, fired.FiringCube));
                }
                info.Clean();
                damagePool.Return(info);
            }
            hitBlocks.Clear();
            hitEnts.Clear();
        }
        */
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

        internal class HitEntity
        {
            internal enum Type
            {
                Shield,
                Grid,
                Voxel,
                Proximity,
                Destroyable,
                Stale,
            }

            public readonly List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
            public MyEntity Entity;
            public LineD Beam;
            public bool Hit;
            public Vector3D? HitPos;
            public Type EventType;

            public HitEntity(MyEntity entity, LineD beam)
            {
                Entity = entity;
                Beam = beam;
            }

            public HitEntity()
            {
            }

            public void Clean()
            {
                Entity = null;
                Beam.Length = 0;
                Beam.Direction = Vector3D.Zero;
                Beam.From = Vector3D.Zero;
                Beam.To = Vector3D.Zero;
                Blocks.Clear();
                Hit = false;
                HitPos = null;
                EventType = Type.Stale;
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