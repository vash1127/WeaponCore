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
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles.HitEntity.Type;
namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private void GetEntitiesInBlastRadius(List<HitEntity> hitList, MyCubeBlock firingCube, Projectile projectile, Vector3D position, Vector3D direction, int poolId)
        {
            var sphere = new BoundingSphereD(position, projectile.System.Values.Ammo.AreaEffectRadius);
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
                Hits.Enqueue(new Session.DamageEvent(projectile, direction, hitList, firingCube));
            }
            else if (Session.Instance.IsServer)
            {
                Hits.Enqueue(new Session.DamageEvent(projectile, direction, hitList, firingCube));
            }
        }

        internal bool GetAllEntitiesInLine(MyCubeBlock firingCube, LineD beam, List<MyLineSegmentOverlapResult<MyEntity>> segmentList,  List<HitEntity> hitList, int poolId, bool quickCheck = false)
        {
            var listCnt = segmentList?.Count ?? hitList.Count;
            var found = false;
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
                        if (quickCheck)
                        {
                            hitEntity.HitPos = Session.Instance.SApi.LineIntersectShield(shieldBlock, beam);
                            hitEntity.Hit = true;
                        }
                        found = true;
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
                    if (quickCheck)
                    {
                        var tmpDist = dist ?? 0;
                        hitEntity.HitPos = (beam.From + (beam.Direction * tmpDist));
                        hitEntity.Hit = true;
                    }
                    found = true;
                    hitList.Add(hitEntity);
                }
            }
            return found;
        }

        internal HitEntity GenerateHitInfo(List<HitEntity> ents, int poolId)
        {
            var count = ents.Count;

            if (count > 1) ents.Sort(GetEntityCompareDist);
            else GetEntityCompareDist(ents[0], null);

            //var afterSort = ents.Count;
            var endOfIndex = ents.Count - 1;
            var lastValidEntry = int.MaxValue;

            for (int i = endOfIndex; i >= 0; i--)
            {
                if (ents[i].Hit)
                {
                    lastValidEntry = i + 1;
                    //Log.Line($"lastValidEntry:{lastValidEntry} - endOfIndex:{endOfIndex}");
                    break;
                }
            }

            if (lastValidEntry == int.MaxValue) lastValidEntry = 0;
            var howManyToRemove = count - lastValidEntry;
            var howMany = howManyToRemove;
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
                //Log.Line($"start:{count} - ASort:{afterSort} - Final:{ents.Count} - howMany:{howMany} - hit:{ents[0].Hit} - hitPos:{ents[0].HitPos.HasValue} - {ents[0].Entity.DebugName} ");
            }
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
                        hitEnt.EventType = Shield;
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
                            hitEnt.EventType = Grid;
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
                        hitEnt.EventType = Voxel;
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
                        hitEnt.EventType = Destroyable;
                    }
                }
                else Log.Line($"no hit");

                if (isX) xDist = dist;
                else yDist = dist;
            }
            return xDist.CompareTo(yDist);
        }

        internal struct DrawProjectile
        {
            internal readonly WeaponSystem System;
            internal readonly int WeaponId;
            internal readonly int MuzzleId;
            internal readonly MyCubeBlock FiringCube;
            internal readonly MyEntity Entity;
            internal readonly MatrixD EntityMatrix;
            internal readonly HitEntity HitEntity;
            internal readonly Trajectile Trajectile;
            internal readonly double LineReSizeLen;
            internal readonly float LineWidth;
            internal readonly int ReSizeSteps;
            internal readonly bool Shrink;
            internal readonly bool Last;
            internal readonly bool OnScreen;
            internal readonly Vector4 Color;

            internal DrawProjectile(WeaponSystem system, MyCubeBlock firingCube, int weaponId, int muzzleId, MyEntity entity, MatrixD entityMatrix, HitEntity hitEntity, Trajectile trajectile, double lineReSizeLen, int reSizeSteps, bool shrink, bool last, bool onScreen)
            {
                System = system;
                FiringCube = firingCube;
                WeaponId = weaponId;
                MuzzleId = muzzleId;
                Entity = entity;
                EntityMatrix = entityMatrix;
                Trajectile = trajectile;
                HitEntity = hitEntity;
                LineReSizeLen = lineReSizeLen;
                ReSizeSteps = reSizeSteps;
                Shrink = shrink;
                Last = last;
                OnScreen = onScreen;
                var color = System.Values.Graphics.Line.Color;
                if (System.LineColorVariance)
                {
                    var cv = System.Values.Graphics.Line.ColorVariance;
                    var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                    color.X *= randomValue;
                    color.Y *= randomValue;
                    color.Z *= randomValue;
                }

                var width = System.Values.Graphics.Line.Width;
                if (System.LineWidthVariance)
                {
                    var wv = System.Values.Graphics.Line.WidthVariance;
                    var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                    width += randomValue;
                }

                LineWidth = width;
                Color = color;
            }
        }

        internal struct Trajectile
        {
            internal readonly Vector3D PrevPosition;
            internal readonly Vector3D Position;
            internal readonly Vector3D Direction;
            internal readonly double Length;
            internal Trajectile(Vector3D prevPosition, Vector3D position, Vector3D direction, double length)
            {
                PrevPosition = prevPosition;
                Position = position;
                Direction = direction;
                Length = length;
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
    }
}