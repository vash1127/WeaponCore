using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using IMyLargeTurretBase = Sandbox.ModAPI.IMyLargeTurretBase;

namespace WeaponCore
{
    public class Weapon
    {
        public Weapon(IMyEntity entity)
        {
            EntityPart = entity;
            LocalTranslation = entity.LocalMatrix.Translation;
            PivotOffsetVec = (Vector3.Transform(entity.PositionComp.LocalAABB.Center, entity.PositionComp.LocalMatrix) - entity.GetTopMostParent(typeof(MyCubeBlock)).PositionComp.LocalAABB.Center);
            UpPivotOffsetLen = PivotOffsetVec.Length();
        }

        public IMyEntity EntityPart;
        public WeaponSystem WeaponSystem;
        public WeaponDefinition WeaponType;
        public Dummy[] Dummies;
        public Muzzle[] Muzzles;
        public Logic Logic;
        public MyEntity Target { get; set; }
        public IMyLargeTurretBase BaseTurret;
        public Random Rnd = new Random(902138212);

        public uint PosUpdatedTick = uint.MinValue;
        public uint PosChangedTick = 1;
        public MatrixD WeaponMatrix;
        public MatrixD OldWeaponMatrix;
        public Vector3D WeaponPosition;
        public Vector3D OldWeaponPosition;
        public Vector3 LocalTranslation;
        public Vector3 PivotOffsetVec;
        public float UpPivotOffsetLen;
        public bool TurretMode;
        public bool TrackTarget;
        public bool FirstRun = true;
        internal uint TargetTick;
        private double _step = 0.05d;

        public bool ReadyToTrack => Target != null && BaseTurret.Target != Target && _azOk && _elOk;
        public bool ReadyToShoot => Target != null && BaseTurret.Target == Target;
        //public bool TargetSwap => (Target != null || !BaseTurret.HasTarget) && TargetTick++ > 240;
        public bool TargetSwap
        {
            get { return (Target != null || !BaseTurret.HasTarget) && TargetTick++ > 240 || FirstRun; }
        }
        private double _azimuth;
        private double _elevation;
        private double _desiredAzimuth;
        private double _desiredElevation;
        private bool _azOk;
        private bool _elOk;

        public void PositionChanged(MyPositionComponentBase pComp)
        {
            PosChangedTick = Session.Instance.Tick;
        }

        private int RotationTime { get; set; }

        internal void Shoot()
        {
            var tick = Session.Instance.Tick;
            if (WeaponType.RotateBarrelAxis == 3) MovePart(0.2f, -1, false, false, true);
            if (PosChangedTick > PosUpdatedTick)
            {
                for (int j = 0; j < Muzzles.Length; j++)
                {
                    var dummy = Dummies[j];
                    var newInfo = dummy.Info;
                    Muzzles[j].Direction = newInfo.Direction;
                    Muzzles[j].Position = newInfo.Position;
                    Muzzles[j].LastPosUpdate = tick;
                }
            }

            if (tick - PosChangedTick > 10) PosUpdatedTick = tick;

            var cc = 0;
            foreach (var m in Muzzles)
            {
                var color = Color.Red;
                if (cc % 2 == 0) color = Color.Blue;
                //Log.Line($"{m.Position} - {m.Direction}");
                DsDebugDraw.DrawLine(m.Position, m.Position + (m.Direction * 1000), color, 0.02f);
                cc++;
            }
        }

        public void MovePart(float radians, int time, bool xAxis, bool yAxis, bool zAxis)
        {
            MatrixD rotationMatrix;
            if (xAxis) rotationMatrix = MatrixD.CreateRotationX(radians * RotationTime);
            else if (yAxis) rotationMatrix = MatrixD.CreateRotationY(radians * RotationTime);
            else if (zAxis) rotationMatrix = MatrixD.CreateRotationZ(radians * RotationTime);
            else return;

            RotationTime += time;
            rotationMatrix.Translation = LocalTranslation;
            EntityPart.PositionComp.LocalMatrix = rotationMatrix;
        }

        internal void SelectTarget()
        {
            if (Target == null) BaseTurret.ResetTargetingToDefault();

            TargetTick = 0;

            Target = GetTarget();

            if (Target != null)
            {
                FirstRun = false;
                var grid = Target as MyCubeGrid;
                if (grid == null)
                {
                    Log.Line($"found entityL {Target.DebugName}");
                    BaseTurret.TrackTarget(Target);
                }
                else
                {
                    var bCount = Logic.TargetBlocks.Count;
                    var found = false;
                    while (!found)
                    {
                        var next = Rnd.Next(0, bCount);
                        if (!Logic.TargetBlocks[next].MarkedForClose)
                        {
                            Target = Logic.TargetBlocks[next];
                            BaseTurret.TrackTarget(Target);
                            Log.Line($"found block - Block:{Logic.TargetBlocks[next].DebugName} - Target:{Target.DebugName} - random:{next} - bCount:{bCount}");
                            found = true;
                        }
                    }
                }
            }
        }

        internal void Rotate()
        {
            var myCube = Logic.MyCube;
            var myMatrix = myCube.PositionComp.WorldMatrix;
            var targetPos = Target.PositionComp.WorldAABB.Center;
            var myPivotPos = myCube.PositionComp.WorldAABB.Center;

            myPivotPos -= Vector3D.Normalize(myMatrix.Down - myMatrix.Up) * UpPivotOffsetLen;

            GetTurretAngles(ref targetPos, ref myPivotPos, BaseTurret, _step, out _azimuth, out _elevation, out _desiredAzimuth, out _desiredElevation);
            var azDiff = 100 * (_desiredAzimuth - _azimuth) / _azimuth;
            var elDiff = 100 * (_desiredElevation - _elevation) / _elevation;

            _azOk = azDiff > -101 && azDiff < -99 || azDiff > -1 && azDiff < 1;
            _elOk = elDiff > -101 && elDiff < -99 || elDiff > -1 && elDiff < 1;
            //Log.Line($"[{azDiff}]({_elOk}) - [{elDiff}]({_azOk})");

            BaseTurret.Azimuth = (float)_azimuth;
            BaseTurret.Elevation = (float)_elevation;
        }

        internal void GetTurretAngles(ref Vector3D targetPositionWorld, ref Vector3D turretPivotPointWorld, IMyLargeTurretBase turret, double maxAngularStep, out double azimuth, out double elevation, out double desiredAzimuth, out double desiredElevation)
        {

            // Get current turret facing
            Vector3D currentVector;
            Vector3D.CreateFromAzimuthAndElevation(turret.Azimuth, turret.Elevation, out currentVector);
            currentVector = Vector3D.Rotate(currentVector, turret.WorldMatrix);

            var up = turret.WorldMatrix.Up;
            var left = Vector3D.Cross(up, currentVector);
            if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                left.Normalize();
            var forward = Vector3D.Cross(left, up);

            var matrix = new MatrixD()
            {
                Forward = forward,
                Left = left,
                Up = up,
            };

            // Get desired angles
            var targetDirection = targetPositionWorld - turretPivotPointWorld;
            GetRotationAngles(ref targetDirection, ref matrix, out desiredAzimuth, out desiredElevation);

            // Get control angles
            azimuth = turret.Azimuth + MathHelper.Clamp(desiredAzimuth, -maxAngularStep, maxAngularStep);
            elevation = turret.Elevation + MathHelper.Clamp(desiredElevation - turret.Elevation, -maxAngularStep, maxAngularStep);
        }

        /*
        /// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
        Dependencies: AngleBetween
        */

        void GetRotationAngles(ref Vector3D targetVector, ref MatrixD worldMatrix, out double yaw, out double pitch)
        {
            var localTargetVector = Vector3D.Rotate(targetVector, MatrixD.Transpose(worldMatrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            yaw = AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is negative

            if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
        }

        /// <summary>
        /// Computes angle between 2 vectors
        /// </summary>
        public static double AngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

        internal MyEntity GetTarget()
        {
            foreach (var ent in Logic.Targeting.TargetRoots)
            {
                if (Target == ent || Target?.Parent == ent) continue;

                var entInfo = MyDetectedEntityInfoHelper.Create(ent, Logic.Turret.OwnerId);
                if (entInfo.IsEmpty() || (entInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Owner)) continue;
                if (entInfo.Type == MyDetectedEntityType.SmallGrid || entInfo.Type == MyDetectedEntityType.LargeGrid)
                {
                    if (!GetTargetBlocks(ent)) return null;
                    return ent;
                }
                return ent;
            }

            return Target;
        }

        private bool GetTargetBlocks(MyEntity targetGrid)
        {
            Logic.TargetBlocks.Clear();
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = Logic.Targeting.TargetBlocks;
            var g = 0;
            var f = 0;
            foreach (var targets in allTargets)
            {
                var rootGrid = targets.Key;
                if (rootGrid != targetGrid) continue;
                if (rootGrid.MarkedForClose) return false;

                if (g++ > 0) break;
                foreach (var b in targets.Value)
                {
                    if (b == null) continue;
                    if (f++ > 9) return true;
                    Logic.TargetBlocks.Add(b);
                }
            }

            return f > 0;
        }

    }

    public class Muzzle
    {
        public Vector3D Position;
        public Vector3D Direction;
        public uint LastFireTick;
        public uint LastPosUpdate;
    }

    public class MyWeaponPlatform
    {
        public readonly Weapon[] Weapons;
        public readonly RecursiveSubparts SubParts = new RecursiveSubparts();
        public readonly WeaponStructure Structure;
        public uint[][] BeamSlot { get; set; }

        public MyWeaponPlatform(MyStringHash subTypeIdHash, IMyEntity entity, Logic logic)
        {
            Structure = Session.Instance.WeaponStructure[subTypeIdHash];
            //PartNames = Structure.PartNames;
            var subPartCount = Structure.PartNames.Length;

            Weapons = new Weapon[subPartCount];
            BeamSlot = new uint[subPartCount][];

            SubParts.Entity = entity;
            SubParts.CheckSubparts();
            for (int i = 0; i < subPartCount; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.PartNames[i]].Barrels.Length;
                IMyEntity subPartEntity;
                SubParts.NameToEntity.TryGetValue(Structure.PartNames[i].String, out subPartEntity);
                BeamSlot[i] = new uint[barrelCount];
                Weapons[i] = new Weapon(subPartEntity)
                {
                    Muzzles = new Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                    WeaponSystem = Structure.WeaponSystems[Structure.PartNames[i]],
                    WeaponType = Structure.WeaponSystems[Structure.PartNames[i]].WeaponType,
                    TurretMode = Structure.WeaponSystems[Structure.PartNames[i]].WeaponType.TurretMode,
                    TrackTarget = Structure.WeaponSystems[Structure.PartNames[i]].WeaponType.TrackTarget,
                    BaseTurret = entity as IMyLargeTurretBase,
                    Logic = logic,
                };
            }

            CompileTurret();
        }

        private void CompileTurret()
        {
            var c = 0;
            foreach (var m in Structure.WeaponSystems)
            {
                var subPart = SubParts.NameToEntity[m.Key.String];
                var barrelCount = m.Value.Barrels.Length;
                Weapons[c].EntityPart.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                for (int i = 0; i < barrelCount; i++)
                {
                    var barrel = m.Value.Barrels[i];
                    Weapons[c].Dummies[i] = new Dummy(subPart, barrel);
                    Weapons[c].Muzzles[i] = new Muzzle();
                }
                c++;
            }
        }
    }

}
