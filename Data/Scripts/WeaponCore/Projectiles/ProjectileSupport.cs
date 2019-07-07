using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        public static List<Vector3D> CreateRandomLineSegOffsets(double maxRange, double minForwardStep, double maxForwardStep, double maxOffset, ref List<Vector3D> offsetList)
        {
            double currentForwardDistance = 0;

            while (currentForwardDistance < maxRange)
            {
                currentForwardDistance += MyUtils.GetRandomDouble(minForwardStep, maxForwardStep);
                var lateralXDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                var lateralYDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                offsetList.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
            }
            return offsetList;
        }

        public static void DisplayLineOffsetEffect(MatrixD startMatrix, Vector3D endCoords, float beamRadius, Color color, List<Vector3D> offsetList, MyStringId offsetMaterial, bool isDedicated = false)
        {

            var maxDistance = Vector3D.Distance(startMatrix.Translation, endCoords);

            for (int i = 0; i < offsetList.Count; i++)
            {

                Vector3D fromBeam;
                Vector3D toBeam;

                if (i == 0)
                {
                    fromBeam = startMatrix.Translation;
                    toBeam = Vector3D.Transform(offsetList[i], startMatrix);
                }
                else
                {
                    fromBeam = Vector3D.Transform(offsetList[i - 1], startMatrix);
                    toBeam = Vector3D.Transform(offsetList[i], startMatrix);
                }

                var vectorColor = color.ToVector4();
                MySimpleObjectDraw.DrawLine(fromBeam, toBeam, offsetMaterial, ref vectorColor, beamRadius);

                if (Vector3D.Distance(startMatrix.Translation, toBeam) > maxDistance) break;
            }
        }

        private static void PrefetchVoxelPhysicsIfNeeded(Projectile p)
        {
            var ray = new LineD(p.Origin, p.Origin + p.Direction * p.MaxTrajectory, p.MaxTrajectory);
            var lineD = new LineD(new Vector3D(Math.Floor(ray.From.X) * 0.5, Math.Floor(ray.From.Y) * 0.5, Math.Floor(ray.From.Z) * 0.5), new Vector3D(Math.Floor(p.Direction.X * 50.0), Math.Floor(p.Direction.Y * 50.0), Math.Floor(p.Direction.Z * 50.0)));
            if (p.VoxelRayCache.IsItemPresent(lineD.GetHash(), (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds, true))
                return;
            using (MyUtils.ReuseCollection(ref p.EntityRaycastResult))
            {
                MyGamePruningStructure.GetAllEntitiesInRay(ref ray, p.EntityRaycastResult, MyEntityQueryType.Static);
                foreach (var segmentOverlapResult in p.EntityRaycastResult)
                    (segmentOverlapResult.Element as MyPlanet)?.PrefetchShapeOnRay(ref ray);
            }
        }

        private static MyEntity GetSubpartOwner(MyEntity entity)
        {
            if (entity == null)
                return null;
            if (!(entity is MyEntitySubpart))
                return entity;
            var myEntity = entity;
            while (myEntity is MyEntitySubpart)
                myEntity = myEntity.Parent;
            return myEntity ?? entity;
        }

        public static void ApplyProjectileForce(
          MyEntity entity,
          Vector3D intersectionPosition,
          Vector3 normalizedDirection,
          bool isPlayerShip,
          float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic)
                return;
            if (entity is IMyCharacter)
                impulse *= 100f;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero, new float?(), true, false);
        }

    }
}
