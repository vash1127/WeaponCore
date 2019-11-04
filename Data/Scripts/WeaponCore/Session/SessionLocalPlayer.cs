using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        internal bool UpdateLocalAiAndCockpit()
        {
            ActiveCockPit = ControlledEntity as MyCockpit;
            InGridAiCockPit = false;
            if (ActiveCockPit != null && GridTargetingAIs.TryGetValue(ActiveCockPit.CubeGrid, out TrackingAi))
            {
                InGridAiCockPit = true;
                return true;
            }

            if (TrackingAi != null) TrackingAi.PrimeTarget = null;
            TrackingAi = null;
            ActiveCockPit = null;
            RemoveGps();
            return false;
        }

        private void PlayerReleasedControl()
        {
            UpdateLocalAiAndCockpit();
        }

        private void PlayerAcquiredControl()
        {
            UpdateLocalAiAndCockpit();
        }

        private void UpdatePlacer()
        {
            if (!Placer.Visible) Placer = null;
            if (!MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.HitInfo.HasValue)
            {
                var hit = MyCubeBuilder.Static.HitInfo.Value as IHitInfo;
                var grid = hit.HitEntity as MyCubeGrid;
                GridAi gridAi;
                if (grid != null && GridTargetingAIs.TryGetValue(grid, out gridAi))
                {
                    if (MyCubeBuilder.Static.CurrentBlockDefinition != null)
                    {
                        var subtypeIdHash = MyCubeBuilder.Static.CurrentBlockDefinition.Id.SubtypeId;
                        GridAi.WeaponCount weaponCount;
                        if (gridAi.WeaponCounter.TryGetValue(subtypeIdHash, out weaponCount))
                        {
                            if (weaponCount.Current >= weaponCount.Max && weaponCount.Max > 0)
                            {
                                MyCubeBuilder.Static.NotifyPlacementUnable();
                                MyCubeBuilder.Static.Deactivate();
                            }
                        }
                    }
                }
            }
        }

        internal bool PlayerInAiCockPit()
        {
            if (ActiveCockPit == null || ActiveCockPit.MarkedForClose || ((IMyControllerInfo)ActiveCockPit.ControllerInfo)?.ControllingIdentityId != MyAPIGateway.Session.Player.IdentityId) return false;
            return true;
        }

        internal void ResetGps()
        {
            if (TargetGps == null)
            {
                Log.Line("resetgps");
                MyVisualScriptLogicProvider.AddGPS("WEAPONCORE", "", Vector3D.MaxValue, Color.Red);
                foreach (var t in MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId))
                {
                    if (t.Name == "WEAPONCORE")
                    {
                        TargetGps = t;
                        break;
                    }
                }
                MyAPIGateway.Session.GPS.AddLocalGps(TargetGps);
                MyVisualScriptLogicProvider.SetGPSColor(TargetGps?.Name, Color.Yellow);
            }
        }

        internal void RemoveGps()
        {
            if (TargetGps != null)
            {
                Log.Line("remove gps");
                MyAPIGateway.Session.GPS.RemoveLocalGps(TargetGps);
                TargetGps = null;
            }
        }

        internal void SetGpsInfo(Vector3D pos, string name, double dist = 0)
        {
            if (TargetGps != null)
            {
                var newPos = dist > 0 ? pos + (Camera.WorldMatrix.Up * dist) : pos;
                TargetGps.Coords = newPos;
                TargetGps.Name = name;
            }
        }

        internal bool CheckTarget(GridAi ai)
        {
            if (ai.PrimeTarget == null)
                return false;

            if (ai.PrimeTarget.MarkedForClose || ai != TrackingAi)
            {
                Log.Line("resetting target");
                ai.PrimeTarget = null;
                TrackingAi = null;
                RemoveGps();
                return false;
            }
            return true;
        }

        internal void SetTarget(MyEntity entity, GridAi ai)
        {
            ai.PrimeTarget = entity;
            TrackingAi = ai;
            ai.TargetResetTick = Tick + 1;
            GridAi gridAi;
            TargetArmed = false;
            if (GridTargetingAIs.TryGetValue((MyCubeGrid)entity, out gridAi))
            {
                TargetArmed = true;
            }
            else
            {
                foreach (var info in ai.SortedTargets)
                {
                    if (info.Target != entity) continue;
                    ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>> typeDict;
                    if (info.IsGrid && ai.Session.GridToBlockTypeMap.TryGetValue((MyCubeGrid)info.Target, out typeDict))
                    {
                        MyConcurrentList<MyCubeBlock> fatList;
                        if (typeDict.TryGetValue(TargetingDefinition.BlockTypes.Offense, out fatList))
                            TargetArmed = fatList.Count > 0;
                        else TargetArmed = false;
                    }
                    else TargetArmed = false;
                    break;
                }
            }
        }

        internal void GetTargetInfo(GridAi ai, out double speed, out string armedStr, out string interceptStr, out string shieldedStr, out string threatStr)
        {
            var target = ai.PrimeTarget;
            var targetVel = target.Physics?.LinearVelocity ?? Vector3.Zero;
            if (MyUtils.IsZero(targetVel, 1E-02F)) targetVel = Vector3.Zero;
            var targetDir = Vector3D.Normalize(targetVel);
            var targetPos = target.PositionComp.WorldAABB.Center;
            var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
            var myHeading = Vector3D.Normalize(myPos - targetPos);
            var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, ApproachDegrees);
            var shielded = ShieldApiLoaded && SApi.ProtectedByShield(target);
            var grid = target as MyCubeGrid;
            var friend = false;
            if (grid != null && grid.BigOwners.Count != 0)
            {
                var relation = MyIDModule.GetRelationPlayerBlock(ai.MyOwner, grid.BigOwners[0], MyOwnershipShareModeEnum.Faction);
                if (relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.Friends) friend = true;
            }
            var threat = friend ? 0 : 1;
            shieldedStr = shielded ? "S" : "_";
            armedStr = TargetArmed ? "A" : "_";
            interceptStr = intercept ? "I" : "_";
            threatStr = threat > 0 ? "T" + threat : "__";
            speed = Math.Round(target.Physics?.Speed ?? 0, 1);
        }


        internal bool GetTargetState()
        {
            var ai = TrackingAi;
            var target = ai.PrimeTarget;
            GridAi.TargetInfo info;
            if (!ai.Targets.TryGetValue(target, out info)) return false;
            if (!Tick20 || _prevTargetId == info.EntInfo.EntityId) return true;
            Log.Line($"primeTarget: oRating:{info.OffenseRating} - blocks:{info.PartCount} - {info.Target.DebugName}");
            _prevTargetId = info.EntInfo.EntityId;
            var targetVel = target.Physics?.LinearVelocity ?? Vector3.Zero;
            if (MyUtils.IsZero(targetVel, 1E-02F)) targetVel = Vector3.Zero;
            var targetDir = Vector3D.Normalize(targetVel);
            var targetRevDir = -targetDir;
            var targetPos = target.PositionComp.WorldAABB.Center;
            var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
            var myHeading = Vector3D.Normalize(myPos - targetPos);

            var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, ApproachDegrees);
            var retreat = MathFuncs.IsDotProductWithinTolerance(ref targetRevDir, ref myHeading, ApproachDegrees);
            if (intercept) TargetState.Engagement = 0;
            else if (retreat) TargetState.Engagement = 1;
            else TargetState.Engagement = -1;

            var speed = Math.Round(target.Physics?.Speed ?? 0, 1);

            var distanceFromCenters = Vector3D.Distance(ai.GridCenter, target.PositionComp.WorldAABB.Center);
            distanceFromCenters -= ai.GridRadius;
            distanceFromCenters -= target.PositionComp.LocalVolume.Radius;
            distanceFromCenters = distanceFromCenters <= 0 ? 0 : distanceFromCenters;
            var distPercent = (distanceFromCenters / ai.MaxTargetingRange) * 100;

            if (distPercent < 100 && distPercent > 66)
                TargetState.Distance = 2;
            else if (distPercent > 33) TargetState.Distance = 1;
            else if (distPercent >= 0) TargetState.Distance = 0;
            else TargetState.Distance = -1;

            if (speed <= 0) TargetState.Speed = -1;
            else
            {
                var speedPercent = (speed / MaxEntitySpeed) * 100;
                if (speedPercent > 95) TargetState.Speed = 9;
                else if (speedPercent > 90) TargetState.Speed = 8;
                else if (speedPercent > 80) TargetState.Speed = 7;
                else if (speedPercent > 70) TargetState.Speed = 6;
                else if (speedPercent > 60) TargetState.Speed = 5;
                else if (speedPercent > 50) TargetState.Speed = 4;
                else if (speedPercent > 40) TargetState.Speed = 3;
                else if (speedPercent > 30) TargetState.Speed = 2;
                else if (speedPercent > 20) TargetState.Speed = 1;
                else if (speedPercent > 0) TargetState.Speed = 0;
                else TargetState.Speed = -1;
            }

            MyTuple<bool, bool, float, float, float, int> shieldInfo = new MyTuple<bool, bool, float, float, float, int>();
            if (ShieldApiLoaded) shieldInfo = SApi.GetShieldInfo(target);
            if (shieldInfo.Item1)
            {
                var shieldPercent = shieldInfo.Item5;
                if (shieldPercent > 66) TargetState.ShieldHealth = 2;
                else if (shieldPercent > 33) TargetState.ShieldHealth = 1;
                else if (shieldPercent > 0) TargetState.ShieldHealth = 0;
                else TargetState.ShieldHealth = -1;
            }
            else TargetState.ShieldHealth = -1;

            var grid = target as MyCubeGrid;
            var friend = false;
            if (grid != null && grid.BigOwners.Count != 0)
            {
                var relation = MyIDModule.GetRelationPlayerBlock(ai.MyOwner, grid.BigOwners[0], MyOwnershipShareModeEnum.Faction);
                if (relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.Friends) friend = true;
            }

            if (friend) TargetState.ThreatLvl = -1;
            else
            {
                var offenseRating = info.OffenseRating;
                if (offenseRating > 2.5) TargetState.ThreatLvl = 4;
                else if (offenseRating > 1.25) TargetState.ThreatLvl = 3;
                else if (offenseRating > 0.5) TargetState.ThreatLvl = 2;
                else if (offenseRating > 0.25) TargetState.ThreatLvl = 1;
                else if (offenseRating > 0) TargetState.ThreatLvl = 0;
                else TargetState.ThreatLvl = -1;
            }
            return true;
        }
    }
}
