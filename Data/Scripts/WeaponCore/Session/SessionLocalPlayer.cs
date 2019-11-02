using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
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
                var gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId);
                foreach (var t in gpsList)
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

        internal void GetTargetInfo2(GridAi ai, out double speed, out bool intercept, out int shield, out float threat)
        {
            var target = ai.PrimeTarget;
            var targetVel = target.Physics?.LinearVelocity ?? Vector3.Zero;
            if (MyUtils.IsZero(targetVel, 1E-02F)) targetVel = Vector3.Zero;
            var targetDir = Vector3D.Normalize(targetVel);
            var targetPos = target.PositionComp.WorldAABB.Center;
            var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
            var myHeading = Vector3D.Normalize(myPos - targetPos);

            intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, ApproachDegrees);

            speed = Math.Round(target.Physics?.Speed ?? 0, 1);

            IMyTerminalBlock shieldBlock = null;
            if (ShieldApiLoaded) shieldBlock = SApi.GetShieldBlock(target);
            if (shieldBlock != null)
            {
                var shieldPercent = SApi.GetShieldPercent(shieldBlock);
                if (shieldPercent > 66) shield = 0;
                else if (shieldPercent > 33) shield = 1;
                else if (shieldPercent > 0) shield = 2;
                else shield = -1;
            }
            else shield = -1;

            var grid = target as MyCubeGrid;
            var friend = false;
            if (grid != null && grid.BigOwners.Count != 0)
            {
                var relation = MyIDModule.GetRelationPlayerBlock(ai.MyOwner, grid.BigOwners[0], MyOwnershipShareModeEnum.Faction);
                if (relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.Friends) friend = true;
            }

            if (friend) threat = -1;
            else
            {
                var offenseRating = ai.Targets[target].OffenseRating;
                threat = offenseRating / 2;
            }
        }
    }
}
