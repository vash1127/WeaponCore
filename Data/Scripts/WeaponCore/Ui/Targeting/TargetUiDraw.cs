using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using WeaponCore.Support;
namespace WeaponCore
{
    internal partial class TargetUi
    {
        internal void DrawTargetUi()
        {
            var s = _session;
            
            DrawReticle = false;
            if (!s.InGridAiBlock && !s.UpdateLocalAiAndCockpit()) return;
            if (ActivateSelector()) DrawSelector();
            if (s.CheckTarget(s.TrackingAi) && GetTargetState(s)) DrawTarget();
        }

        private void DrawSelector()
        {
            var s = _session;

            if (!_cachedPointerPos) InitPointerOffset(0.05);
            if (!_cachedTargetPos) InitTargetOffset();
            var offetPosition = Vector3D.Transform(PointerOffset, _session.CameraMatrix);

            if (s.UiInput.FirstPersonView)
            {
                if (!MyUtils.IsZero(_pointerPosition.Y))
                {
                    _pointerPosition.Y = 0f;
                    InitPointerOffset(0.05);
                }
            }
            else if (s.UiInput.CtrlPressed)
            {
                if (s.UiInput.PreviousWheel != s.UiInput.CurrentWheel)
                {
                    var currentPos = _pointerPosition.Y;
                    if (s.UiInput.CurrentWheel > s.UiInput.PreviousWheel) currentPos += 0.05f;
                    else currentPos -= 0.05f;
                    var clampPos = MathHelper.Clamp(currentPos, -1.25f, 1.25f);
                    _3RdPersonPos.Y = clampPos;
                    InitPointerOffset(0.05);
                }
                if (!MyUtils.IsEqual(_pointerPosition, _3RdPersonPos))
                {
                    _pointerPosition = _3RdPersonPos;
                    InitPointerOffset(0.05);
                }
            }
            else if (!MyUtils.IsEqual(_pointerPosition, _3RdPersonPos))
            {
                _pointerPosition = _3RdPersonPos;
                InitPointerOffset(0.05);
            }
            
            SelectTarget(manualSelect: false);

            if (s.Tick - _lastDrawTick > 1 && _delay++ < 10) return;
            _delay = 0;
            _lastDrawTick = s.Tick;

            MyTransparentGeometry.AddBillboardOriented(_cross, _reticleColor, offetPosition, s.CameraMatrix.Left, s.CameraMatrix.Up, (float)PointerAdjScale, BlendTypeEnum.PostPP);
            DrawReticle = true;
        }

        private void DrawTarget()
        {
            var s = _session;
            var focus = s.TrackingAi.Construct.Data.Repo.FocusData;
            for (int i = 0; i < s.TrackingAi.TargetState.Length; i++) {

                if (focus.Target[i] <= 0) continue;
                var lockMode = focus.Locked[i];

                var targetState = s.TrackingAi.TargetState[i];
                var displayCount = 0;

                foreach (var icon in _targetIcons.Keys) {

                    int iconLevel;
                    if (!IconStatus(icon, targetState, out iconLevel)) continue;

                    Vector3D offset;
                    float scale;
                    MyStringId textureName;
                    var iconInfo = _targetIcons[icon][iconLevel];
                    iconInfo.GetTextureInfo(i, displayCount, s, out textureName, out scale, out offset);

                    var color = Color.White;

                    switch (lockMode) {
                        case FocusData.LockModes.None:
                            color = Color.White;
                            break;
                        case FocusData.LockModes.Locked:
                            color = s.Count < 60 ? Color.White : new Color(255, 255, 255, 64);
                            break;
                        case FocusData.LockModes.ExclusiveLock:
                            color = s.SCount < 30 ? Color.White : new Color(255, 255, 255, 64);
                            break;
                            
                    }

                    var skipSize = !s.Settings.ClientConfig.ShowHudTargetSizes && icon.Equals("size");

                    if (!skipSize) 
                        MyTransparentGeometry.AddBillboardOriented(textureName, color, offset, s.CameraMatrix.Left, s.CameraMatrix.Up, scale, BlendTypeEnum.PostPP);

                    if (focus.ActiveId == i && displayCount == 0) {

                        if (!skipSize) {
                            var focusTexture = focus.ActiveId == 0 ? _focus : _focusSecondary;
                            MyTransparentGeometry.AddBillboardOriented(focusTexture, color, offset, s.CameraMatrix.Left, s.CameraMatrix.Up, scale, BlendTypeEnum.PostPP);
                        }
                        else {

                            var focusColor = focus.ActiveId == 0 ? Color.Red : Color.LightBlue;
                            MyQuadD quad;
                            var up = (Vector3)s.CameraMatrix.Up;
                            var left = (Vector3)s.CameraMatrix.Left;

                            MyUtils.GetBillboardQuadOriented(out quad, ref offset, 0.002f, 0.002f, ref left, ref up);
                            MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point1, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, FocusTextureMap.P0, FocusTextureMap.P1, FocusTextureMap.P3, FocusTextureMap.Material, 0, offset, focusColor, BlendTypeEnum.PostPP);
                            MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point3, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, FocusTextureMap.P0, FocusTextureMap.P2, FocusTextureMap.P3, FocusTextureMap.Material, 0, offset, focusColor, BlendTypeEnum.PostPP);
                        }
                    }

                    displayCount++;
                }

                MyEntity target;
                if (i == focus.ActiveId && MyEntities.TryGetEntityById(focus.Target[focus.ActiveId], out target)) {

                    var targetSphere = target.PositionComp.WorldVolume;
                    var targetCenter = targetSphere.Center;
                    var screenPos = s.Camera.WorldToScreen(ref targetCenter);
                    var screenScale = 0.1 * s.ScaleFov;

                    if (Vector3D.Transform(targetCenter, s.Camera.ViewMatrix).Z > 0) {
                        screenPos.X *= -1;
                        screenPos.Y = -1;
                    }

                    var dotpos = new Vector2D(MathHelper.Clamp(screenPos.X, -0.98, 0.98), MathHelper.Clamp(screenPos.Y, -0.98, 0.98));

                    dotpos.X *= (float)(screenScale * _session.AspectRatio);
                    dotpos.Y *= (float)screenScale;
                    screenPos = Vector3D.Transform(new Vector3D(dotpos.X, dotpos.Y, -0.1), s.CameraMatrix);
                    MyTransparentGeometry.AddBillboardOriented(_active, Color.White, screenPos, s.CameraMatrix.Left, s.CameraMatrix.Up, (float)screenScale * 0.075f, BlendTypeEnum.PostPP);

                    if (s.Tick20) s.HudUi.AddText(text: $"RANGE: {targetState.RealDistance:#.0}  -  SIZE: {targetState.SizeExtended}", x: i == 0 ? 0f : -0.345f, y: 0.83f, name: Hud.ElementNames.Test1, ttl: 18, color: i == 0 ? Color.OrangeRed : Color.MediumOrchid, justify: Hud.Justify.Center, fontType: Hud.FontType.Shadow, fontSize: 5, heightScale: 0.75f);
                }
            }
        }

        private static bool IconStatus(string icon, TargetStatus targetState, out int iconLevel)
        {
            bool display;
            switch (icon)
            {
                case "speed":
                    display = targetState.Speed > -1;
                    iconLevel = !display ? 0 : targetState.Speed;
                    break;
                case "size":
                    display = targetState.Size > -1;
                    iconLevel = !display ? 0 : targetState.Size;
                    break;
                case "threat":
                    display = targetState.ThreatLvl > -1;
                    iconLevel = !display ? 0 : targetState.ThreatLvl;
                    break;
                case "shield":
                    display = targetState.ShieldHealth > -1;
                    iconLevel = !display ? 0 : targetState.ShieldHealth;
                    break;
                case "engagement":
                    display = targetState.Engagement > -1;
                    iconLevel = !display ? 0 : targetState.Engagement;
                    break;
                case "distance":
                    display = targetState.Distance > -1;
                    iconLevel = !display ? 0 : targetState.Distance;
                    break;
                default:
                    display = false;
                    iconLevel = 0;
                    break;
            }
            return display;
        }

        private void InitTargetOffset()
        {
            var position = new Vector3D(_targetDrawPosition.X, _targetDrawPosition.Y, 0);
            var scale = 0.075 * _session.ScaleFov;

            position.X *= scale * _session.AspectRatio;
            position.Y *= scale;

            AdjScale = 0.125 * scale;

            TargetOffset = new Vector3D(position.X, position.Y, -0.1);
            _cachedTargetPos = true;
        }

        internal bool GetTargetState(Session s)
        {
            var ai = s.TrackingAi;
            var validFocus = false;
            var size0 = s.Settings.Enforcement.ShipSizes[0];
            var size1 = s.Settings.Enforcement.ShipSizes[1];
            var size2 = s.Settings.Enforcement.ShipSizes[2];
            var size3 = s.Settings.Enforcement.ShipSizes[3];
            var size4 = s.Settings.Enforcement.ShipSizes[4];
            var size5 = s.Settings.Enforcement.ShipSizes[5];
            var size6 = s.Settings.Enforcement.ShipSizes[6];
            
            if (s.Tick - MasterUpdateTick > 300 || MasterUpdateTick < 300 && _masterTargets.Count == 0)
                BuildMasterCollections(ai);

            for (int i = 0; i < ai.Construct.Data.Repo.FocusData.Target.Length; i++)
            {
                var targetId = ai.Construct.Data.Repo.FocusData.Target[i];
                float offenseRating;
                MyEntity target;
                if (targetId <= 0 || !MyEntities.TryGetEntityById(targetId, out target) || !_masterTargets.TryGetValue(target, out offenseRating)) continue;
                validFocus = true;
                if (!s.Tick20) continue;
                var grid = target as MyCubeGrid;
                var partCount = 1;
                var largeGrid = false;
                var smallGrid = false;
                if (grid != null)  {

                    largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
                    smallGrid = !largeGrid;
                    GridAi targetAi;
                    GridMap gridMap;
                    if (s.GridToMasterAi.TryGetValue(grid, out targetAi))
                        partCount = targetAi.Construct.BlockCount;
                    else if (s.GridToInfoMap.TryGetValue(grid, out gridMap))
                        partCount = gridMap.MostBlocks;
                }

                var targetVel = target.Physics?.LinearVelocity ?? Vector3.Zero;
                if (MyUtils.IsZero(targetVel, 1E-01F)) targetVel = Vector3.Zero;
                var targetDir = Vector3D.Normalize(targetVel);
                var targetRevDir = -targetDir;
                var targetPos = target.PositionComp.WorldAABB.Center;
                var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
                var myHeading = Vector3D.Normalize(myPos - targetPos);

                if ((size6.LargeGrid && largeGrid || !size6.LargeGrid && smallGrid) && partCount > size6.BlockCount) ai.TargetState[i].Size = 6;
                else if ((size5.LargeGrid && largeGrid || !size5.LargeGrid && smallGrid) && partCount > size5.BlockCount) ai.TargetState[i].Size = 5;
                else if ((size4.LargeGrid && largeGrid || !size4.LargeGrid && smallGrid) && partCount > size4.BlockCount) ai.TargetState[i].Size = 4;
                else if ((size3.LargeGrid && largeGrid || !size3.LargeGrid && smallGrid) && partCount > size3.BlockCount) ai.TargetState[i].Size = 3;
                else if ((size2.LargeGrid && largeGrid || !size2.LargeGrid && smallGrid) && partCount > size2.BlockCount) ai.TargetState[i].Size = 2;
                else if ((size1.LargeGrid && largeGrid || !size1.LargeGrid && smallGrid) && partCount > size1.BlockCount) ai.TargetState[i].Size = 1;
                else ai.TargetState[i].Size = 0;

                ai.TargetState[i].SizeExtended = partCount / (largeGrid ? 100f : 500f);

                var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, s.ApproachDegrees);
                var retreat = MathFuncs.IsDotProductWithinTolerance(ref targetRevDir, ref myHeading, s.ApproachDegrees);
                if (intercept) ai.TargetState[i].Engagement = 0;
                else if (retreat) ai.TargetState[i].Engagement = 1;
                else ai.TargetState[i].Engagement = 2;

                var distanceFromCenters = Vector3D.Distance(ai.MyGrid.PositionComp.WorldAABB.Center, target.PositionComp.WorldAABB.Center);
                distanceFromCenters -= ai.MyGrid.PositionComp.LocalVolume.Radius;
                distanceFromCenters -= target.PositionComp.LocalVolume.Radius;
                distanceFromCenters = distanceFromCenters <= 0 ? 0 : distanceFromCenters;
                ai.TargetState[i].RealDistance = distanceFromCenters;

                var distPercent = (distanceFromCenters / ai.MaxTargetingRange) * 100;
                if (distPercent > 95) ai.TargetState[i].Distance = 9;
                else if (distPercent > 90) ai.TargetState[i].Distance = 8;
                else if (distPercent > 80) ai.TargetState[i].Distance = 7;
                else if (distPercent > 70) ai.TargetState[i].Distance = 6;
                else if (distPercent > 60) ai.TargetState[i].Distance = 5;
                else if (distPercent > 50) ai.TargetState[i].Distance = 4;
                else if (distPercent > 40) ai.TargetState[i].Distance = 3;
                else if (distPercent > 30) ai.TargetState[i].Distance = 2;
                else if (distPercent > 20) ai.TargetState[i].Distance = 1;
                else if (distPercent > 0) ai.TargetState[i].Distance = 0;
                else ai.TargetState[i].Distance = -1;

                var speed = Math.Round(target.Physics?.Speed ?? 0, 1);
                if (speed <= 0) ai.TargetState[i].Speed = -1;
                else
                {
                    var speedPercent = (speed / s.MaxEntitySpeed) * 100;
                    if (speedPercent > 95) ai.TargetState[i].Speed = 9;
                    else if (speedPercent > 90) ai.TargetState[i].Speed = 8;
                    else if (speedPercent > 80) ai.TargetState[i].Speed = 7;
                    else if (speedPercent > 70) ai.TargetState[i].Speed = 6;
                    else if (speedPercent > 60) ai.TargetState[i].Speed = 5;
                    else if (speedPercent > 50) ai.TargetState[i].Speed = 4;
                    else if (speedPercent > 40) ai.TargetState[i].Speed = 3;
                    else if (speedPercent > 30) ai.TargetState[i].Speed = 2;
                    else if (speedPercent > 20) ai.TargetState[i].Speed = 1;
                    else if (speedPercent > 0.3) ai.TargetState[i].Speed = 0;
                    else ai.TargetState[i].Speed = -1;
                }

                MyTuple<bool, bool, float, float, float, int> shieldInfo = new MyTuple<bool, bool, float, float, float, int>();
                if (s.ShieldApiLoaded) shieldInfo = s.SApi.GetShieldInfo(target);
                if (shieldInfo.Item1)
                {
                    var shieldPercent = shieldInfo.Item5;
                    if (shieldPercent > 95) ai.TargetState[i].ShieldHealth = 9;
                    else if (shieldPercent > 90) ai.TargetState[i].ShieldHealth = 8;
                    else if (shieldPercent > 80) ai.TargetState[i].ShieldHealth = 7;
                    else if (shieldPercent > 70) ai.TargetState[i].ShieldHealth = 6;
                    else if (shieldPercent > 60) ai.TargetState[i].ShieldHealth = 5;
                    else if (shieldPercent > 50) ai.TargetState[i].ShieldHealth = 4;
                    else if (shieldPercent > 40) ai.TargetState[i].ShieldHealth = 3;
                    else if (shieldPercent > 30) ai.TargetState[i].ShieldHealth = 2;
                    else if (shieldPercent > 20) ai.TargetState[i].ShieldHealth = 1;
                    else if (shieldPercent > 0) ai.TargetState[i].ShieldHealth = 0;
                    else ai.TargetState[i].ShieldHealth = -1;
                }
                else ai.TargetState[i].ShieldHealth = -1;

                var friend = false;
                if (grid != null && grid.BigOwners.Count != 0)
                {
                    var relation = MyIDModule.GetRelationPlayerBlock(ai.AiOwner, grid.BigOwners[0], MyOwnershipShareModeEnum.Faction);
                    if (relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.Friends) friend = true;
                }

                if (friend) ai.TargetState[i].ThreatLvl = -1;
                else
                {
                    int shieldBonus = 0;
                    if (s.ShieldApiLoaded)
                    {
                        var myShieldInfo = s.SApi.GetShieldInfo(ai.MyGrid);
                        if (shieldInfo.Item1 && myShieldInfo.Item1)
                            shieldBonus = shieldInfo.Item5 > myShieldInfo.Item5 ? 1 : -1;
                        else if (shieldInfo.Item1) shieldBonus = 1;
                        else if (myShieldInfo.Item1) shieldBonus = -1;
                    }

                    if (offenseRating > 5) ai.TargetState[i].ThreatLvl = shieldBonus < 0 ? 8 : 9;
                    else if (offenseRating > 4) ai.TargetState[i].ThreatLvl = 8 + shieldBonus;
                    else if (offenseRating > 3) ai.TargetState[i].ThreatLvl = 7 + shieldBonus;
                    else if (offenseRating > 2) ai.TargetState[i].ThreatLvl = 6 + shieldBonus;
                    else if (offenseRating > 1) ai.TargetState[i].ThreatLvl = 5 + shieldBonus;
                    else if (offenseRating > 0.5) ai.TargetState[i].ThreatLvl = 4 + shieldBonus;
                    else if (offenseRating > 0.25) ai.TargetState[i].ThreatLvl = 3 + shieldBonus;

                    else if (offenseRating > 0.125) ai.TargetState[i].ThreatLvl = 2 + shieldBonus;
                    else if (offenseRating > 0.0625) ai.TargetState[i].ThreatLvl = 1 + shieldBonus;
                    else if (offenseRating > 0) ai.TargetState[i].ThreatLvl = shieldBonus > 0 ? 1 : 0;
                    else ai.TargetState[i].ThreatLvl = -1;
                }
            }
            return validFocus;
        }

    }
}
