using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRageMath;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        internal readonly ConcurrentQueue<IThreadHits> Hits = new ConcurrentQueue<IThreadHits>();
        internal readonly MyConcurrentPool<List<IMyEntity>> CheckPool = new MyConcurrentPool<List<IMyEntity>>();

        private readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> _segmentPool = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>();
        private readonly MyConcurrentPool<DamageInfo> _damagePool = new MyConcurrentPool<DamageInfo>();
        private readonly Dictionary<IMySlimBlock, DamageInfo> _hitBlocks = new Dictionary<IMySlimBlock, DamageInfo>();
        private readonly Dictionary<IMyEntity, DamageInfo> _hitEnts = new Dictionary<IMyEntity, DamageInfo>();

        internal void GetAllEntitiesInLine(List<IMyEntity> ents, FiredBeam fired, LineD beam)
        {
            var segmentList = _segmentPool.Get();
            MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, segmentList);
            foreach (var result in segmentList)
            {
                var ent = result.Element;
                if (ent == fired.Weapon.Logic.MyCube || ent == fired.Weapon.Logic.Turret.CubeGrid) continue;

                var shieldBlock = Session.Instance.SApi?.MatchEntToShieldFast(ent, true);
                if (shieldBlock != null)
                {
                    if (ent.Physics == null) ents.Add(shieldBlock);
                    else continue;
                }
                var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                if (obb.Intersects(ref beam) == null) continue;

                if (ent.Physics != null && (ent is MyCubeGrid || ent is MyVoxelBase || ent is IMyDestroyableObject))
                    ents.Add(ent);
            }
            segmentList.Clear();
            _segmentPool.Return(segmentList);
        }

        internal void GetAllEntitiesInLine2(List<IMyEntity> ents, FiredBeam fired, LineD beam, List<MyLineSegmentOverlapResult<MyEntity>> segmentList)
        {
            foreach (var result in segmentList)
            {
                var ent = result.Element;
                if (ent == fired.Weapon.Logic.MyCube || ent == fired.Weapon.Logic.Turret.CubeGrid) continue;

                var shieldBlock = Session.Instance.SApi?.MatchEntToShieldFast(ent, true);
                if (shieldBlock != null)
                {
                    if (ent.Physics == null) ents.Add(shieldBlock);
                    else continue;
                }
                var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                if (obb.Intersects(ref beam) == null) continue;

                if (ent.Physics != null && (ent is MyCubeGrid || ent is MyVoxelBase || ent is IMyDestroyableObject))
                    ents.Add(ent);
            }
        }

        internal HitInfo GetHitEntities(List<IMyEntity> ents, LineD beam)
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

        internal bool GetDamageInfo(FiredBeam fired, LineD beam, HitInfo hitInfo, int beamId, bool draw)
        {
            if (hitInfo.HitPos != Vector3D.Zero)
            {
                DamageInfo damageInfo = null;
                if (hitInfo.Slim != null)
                {
                    _hitBlocks.TryGetValue(hitInfo.Slim, out damageInfo);
                    if (damageInfo == null) damageInfo = _damagePool.Get();
                    damageInfo.Update(beamId, hitInfo.NewBeam, null, hitInfo.Slim, hitInfo.HitPos, 1);
                    _hitBlocks[hitInfo.Slim] = damageInfo;
                }
                else
                {
                    _hitEnts.TryGetValue(hitInfo.Entity, out damageInfo);
                    if (damageInfo == null) damageInfo = _damagePool.Get();
                    damageInfo.Update(beamId, hitInfo.NewBeam, hitInfo.Entity, null, hitInfo.HitPos, 1);
                    _hitEnts[hitInfo.Entity] = damageInfo;
                }

                return true;
            }
            if (draw && !Session.Instance.DedicatedServer) Session.Instance.DrawBeams.Enqueue(new Session.DrawProjectile(fired.Weapon, beamId, beam, Vector3D.Zero, Vector3D.Zero, null, false));
            return false;
        }

        internal void DamageEntities(FiredBeam fired)
        {
            foreach (var pair in _hitBlocks)
            {
                var info = pair.Value;
                info.HitPos /= info.HitCount;

                if (Session.Instance.IsServer) Hits.Enqueue(new TurretGridEvent(pair.Key, pair.Value.HitCount, fired.Weapon));
                info.Clean();
                _damagePool.Return(info);
            }

            foreach (var pair in _hitEnts)
            {
                var ent = pair.Key;
                var shield = ent as IMyTerminalBlock;
                var voxel = ent as MyVoxelBase;
                var destroyable = ent as IMyDestroyableObject;

                var info = pair.Value;
                info.HitPos /= info.HitCount;

                if (Session.Instance.IsServer)
                {
                    if (shield != null) Hits.Enqueue(new TurretShieldEvent(shield, Session.Instance.SApi, info.HitPos / info.HitCount, info.HitCount, fired.Weapon));
                    if (voxel != null) Hits.Enqueue(new TurretVoxelEvent());
                    if (destroyable != null) Hits.Enqueue(new TurretDestroyableEvent(destroyable, fired.Weapon));
                }
                info.Clean();
                _damagePool.Return(info);
            }
            _hitBlocks.Clear();
            _hitEnts.Clear();
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
