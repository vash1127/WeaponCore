using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace WeaponCore.Support
{
    class Projectiles
    {
        internal readonly List<FiredBeam> FiredBeams = new List<FiredBeam>();

        internal readonly ConcurrentQueue<IThreadHits> Hits = new ConcurrentQueue<IThreadHits>();

        private readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> _segmentPool = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>();
        private readonly MyConcurrentPool<List<IMyEntity>> _checkPool = new MyConcurrentPool<List<IMyEntity>>();
        private readonly MyConcurrentPool<DamageInfo> _damagePool = new MyConcurrentPool<DamageInfo>();
        private readonly Dictionary<IMySlimBlock, DamageInfo> _hitBlocks = new Dictionary<IMySlimBlock, DamageInfo>();
        private readonly Dictionary<IMyEntity, DamageInfo> _hitEnts = new Dictionary<IMyEntity, DamageInfo>();
        private readonly ObjectsPool<Projectile> _projectiles = new ObjectsPool<Projectile>(8192, (Func<Projectile>)null);

        public void Add(FiredProjectile fired)
        {
            foreach (var f in fired.Projectiles)
            {
                Projectile projectile;
                _projectiles.AllocateOrCreate(out projectile);
                projectile.Start(f, fired.Logic);
            }
        }

        internal void Update()
        {
            foreach (var projectile in _projectiles.Active)
            {
                if (!projectile.Update())
                {
                    projectile.Close();
                    _projectiles.MarkForDeallocate(projectile); ;
                }
            }
            _projectiles.DeallocateAllMarked();
        }

        internal void RunBeams()
        {
            lock (FiredBeams)
            {
                MyAPIGateway.Parallel.ForEach(FiredBeams, fired =>
                {
                    for (int i = 0; i < fired.Beams.Count; i++)
                    {
                        var beam = fired.Beams[i];
                        var checkEnts = _checkPool.Get();

                        GetAllEntitiesInLine(checkEnts, fired, beam);
                        var hitInfo = GetHitEntities(checkEnts, beam);
                        GetDamageInfo(fired, beam, hitInfo, i);

                        checkEnts.Clear();
                        _checkPool.Return(checkEnts);
                    }
                    DamageEntities(fired);
                });
                FiredBeams.Clear();
            }
        }

        private void GetDamageInfo(FiredBeam fired, LineD beam, HitInfo hitInfo, int beamId)
        {
            DamageInfo damageInfo = null;
            if (hitInfo.HitPos != Vector3D.Zero)
            {
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
            }
            if (!Session.Instance.DedicatedServer && damageInfo == null) Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(fired.Logic, beamId, beam, Vector3D.Zero, Vector3D.Zero, null, false));
        }

        private void DamageEntities(FiredBeam fired)
        {
            foreach (var pair in _hitBlocks)
            {
                var ent = pair.Key;
                var info = pair.Value;
                info.HitPos /= info.HitCount;

                if (!Session.Instance.DedicatedServer)
                    foreach (var bPair in info.AllBeams) Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(fired.Logic, bPair.Key, bPair.Value, Vector3D.Zero, info.HitPos, ent.CubeGrid, info.PrimaryBeam()));

                if (Session.Instance.IsServer) Hits.Enqueue(new TurretGridEvent(pair.Key, pair.Value.HitCount, fired.Logic));
                info.Clean();
                _damagePool.Return(info);
            }

            foreach (var pair in _hitEnts)
            {
                var ent = pair.Key;
                var shield = ent as IMyTerminalBlock;
                var voxel = ent as MyVoxelBase;
                var destroyable = ent as IMyDestroyableObject;

                var parentEnt = shield != null ? shield.CubeGrid : ent;

                var info = pair.Value;
                info.HitPos /= info.HitCount;
                if (!Session.Instance.DedicatedServer)
                    foreach (var bPair in info.AllBeams) Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(fired.Logic, bPair.Key, bPair.Value, Vector3D.Zero, info.HitPos, parentEnt, info.PrimaryBeam()));

                if (Session.Instance.IsServer)
                {
                    if (shield != null) Hits.Enqueue(new TurretShieldEvent(shield, Session.Instance.SApi, info.HitPos / info.HitCount, info.HitCount, fired.Logic));
                    if (voxel != null) Hits.Enqueue(new TurretVoxelEvent());
                    if (destroyable != null) Hits.Enqueue(new TurretDestroyableEvent(destroyable, fired.Logic));
                }
                info.Clean();
                _damagePool.Return(info);
            }
            _hitBlocks.Clear();
            _hitEnts.Clear();
        }

        private void GetAllEntitiesInLine(List<IMyEntity> ents, FiredBeam fired, LineD beam)
        {
            var segmentList = _segmentPool.Get();
            MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, segmentList);
            foreach (var result in segmentList)
            {
                var ent = result.Element;
                if (ent == fired.Logic.MyCube || ent == fired.Logic.Turret.CubeGrid) continue;

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

        private HitInfo GetHitEntities(List<IMyEntity> ents, LineD beam)
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

        internal struct FiredBeam
        {
            public readonly List<LineD> Beams;
            public readonly Logic Logic;

            public FiredBeam(Logic logic, List<LineD> beams)
            {
                Logic = logic;
                Beams = beams;
            }
        }

        internal struct FiredProjectile
        {
            public readonly List<LineD> Projectiles;
            public readonly Logic Logic;

            public FiredProjectile(Logic logic, List<LineD> projectiles)
            {
                Logic = logic;
                Projectiles = projectiles;
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

        internal class DamageInfo
        {
            public readonly Dictionary<int, LineD> AllBeams = new Dictionary<int, LineD>();
            public LineD UpdatedBeam;
            public IMyEntity Entity;
            public IMySlimBlock Slim;
            public Vector3D HitPos;
            public int HitCount;
            public bool PrimeBeam = true;
            public void Update(int beamId, LineD updatedBeam, IMyEntity entity, IMySlimBlock slim, Vector3D hitPos, int hitCount)
            {
                AllBeams.Add(beamId, updatedBeam);
                UpdatedBeam = updatedBeam;
                Entity = entity;
                Slim = slim;
                HitPos += hitPos;
                HitCount += hitCount;
            }

            public bool PrimaryBeam()
            {
                if (PrimeBeam)
                {
                    PrimeBeam = false;
                    return true;
                }
                return false;
            }

            public void Clean()
            {
                AllBeams.Clear();
                PrimeBeam = true;
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



        internal interface IThreadHits
        {
            void Execute();
        }

        internal class TurretShieldEvent : IThreadHits
        {
            public readonly IMyTerminalBlock Shield;
            public readonly ShieldApi SApi;
            public readonly Vector3D HitPos;
            public readonly int Hits;
            public readonly Logic Logic;
            public TurretShieldEvent(IMyTerminalBlock shield, ShieldApi sApi, Vector3D hitPos, int hits, Logic logic)
            {
                Shield = shield;
                SApi = sApi;
                HitPos = hitPos;
                Hits = hits;
                Logic = logic;
            }

            public void Execute()
            {
                if (Shield == null || Logic == null || SApi == null) return;
                var damage = 100 * Hits;
                SApi.PointAttackShield(Shield, HitPos, Logic.Turret.EntityId, damage, true, false);
            }
        }

        internal class TurretGridEvent : IThreadHits
        {
            public readonly IMySlimBlock Block;
            public readonly int Hits;
            public readonly Logic Logic;
            public readonly MyStringHash TestDamage = MyStringHash.GetOrCompute("TestDamage");
            public TurretGridEvent(IMySlimBlock block, int hits, Logic logic)
            {
                Block = block;
                Hits = hits;
                Logic = logic;
            }

            public void Execute()
            {
                if (Block == null || Block.IsDestroyed || Block.CubeGrid.MarkedForClose) return;
                var damage = 1 * Hits;
                Block.DoDamage(damage, TestDamage, true, null, Logic.Turret.EntityId);
            }
        }

        internal class TurretDestroyableEvent : IThreadHits
        {
            public readonly IMyDestroyableObject DestObj;
            public readonly MyStringHash TestDamage = MyStringHash.GetOrCompute("TestDamage");
            public readonly Logic Logic;

            internal TurretDestroyableEvent(IMyDestroyableObject destObj, Logic logic)
            {
                DestObj = destObj;
                Logic = logic;
            }

            public void Execute()
            {
                if (DestObj == null) return;
                var damage = 100;
                DestObj.DoDamage(damage, TestDamage, true, null, Logic.Turret.EntityId);
            }
        }

        internal class TurretVoxelEvent : IThreadHits
        {
            public void Execute()
            {

            }
        }
    }
}
