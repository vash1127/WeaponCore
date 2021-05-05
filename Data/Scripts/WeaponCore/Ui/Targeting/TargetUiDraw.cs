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
            if (s.CheckTarget(s.TrackingAi) && GetTargetState(s))
            {
                DrawTarget();
            }
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
            for (int i = 0; i < s.TrackingAi.TargetState.Length; i++)
            {

                if (focus.Target[i] <= 0) continue;
                var lockMode = focus.Locked[i];

                var targetState = s.TrackingAi.TargetState[i];
                var isActive = i == focus.ActiveId;
                var primary = i == 0;
                var shielded = targetState.ShieldHealth >= 0;

                var collection = primary ? _primaryTargetHuds : _secondaryTargetHuds;

                foreach (var hud in collection.Keys)
                {
                    if (isActive && (hud == InactiveShield || hud == InactiveNoShield))
                        continue;

                    if (!isActive && (hud == ActiveShield || hud == ActiveNoShield))
                        continue;

                    if (shielded && (hud == ActiveNoShield || hud == InactiveNoShield))
                        continue;
                    if (!shielded && (hud == ActiveShield || hud == InactiveShield))
                        continue;

                    Vector3D offset;
                    Vector2 localOffset;

                    float scale;
                    float screenScale;
                    float fontScale;
                    MyStringId textureName;
                    var hudInfo = collection[hud];
                    hudInfo.GetTextureInfo(s, out textureName, out scale, out screenScale, out fontScale, out offset, out localOffset);

                    var color = Color.White;
                    switch (lockMode)
                    {
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

                    MyTransparentGeometry.AddBillboardOriented(textureName, color, offset, s.CameraMatrix.Left, s.CameraMatrix.Up, screenScale, BlendTypeEnum.PostPP);
                    if (s.Tick20)
                    {
                        for (int j = 0; j < 11; j++)
                        {
                            string text;
                            Vector2 textOffset;
                            if (TextStatus(j, targetState, scale, localOffset, shielded, out text, out textOffset))
                            {
                                var textColor = Color.WhiteSmoke;
                                var fontSize = (float)Math.Round(22 * fontScale);
                                var fontHeight = 0.75f;
                                var fontAge = 18;
                                var fontJustify = Hud.Justify.None;
                                var fontType = Hud.FontType.Shadow;
                                var elementId = MathFuncs.UniqueId(i, j);

                                s.HudUi.AddText(text: text, x: textOffset.X, y: textOffset.Y, elementId: elementId, ttl: fontAge, color: textColor, justify: fontJustify, fontType: fontType, fontSize: fontSize, heightScale: fontHeight);
                            }
                        }
                    }
                }

                MyEntity target;
                if (isActive && MyEntities.TryGetEntityById(focus.Target[focus.ActiveId], out target))
                {

                    var targetSphere = target.PositionComp.WorldVolume;
                    var targetCenter = targetSphere.Center;
                    var screenPos = s.Camera.WorldToScreen(ref targetCenter);

                    if (Vector3D.Transform(targetCenter, s.Camera.ViewMatrix).Z > 0)
                    {
                        screenPos.X *= -1;
                        screenPos.Y = -1;
                    }

                    var dotpos = new Vector2D(MathHelper.Clamp(screenPos.X, -0.98, 0.98), MathHelper.Clamp(screenPos.Y, -0.98, 0.98));
                    var screenScale = 0.1 * s.ScaleFov;
                    dotpos.X *= (float)(screenScale * _session.AspectRatio);
                    dotpos.Y *= (float)screenScale;
                    screenPos = Vector3D.Transform(new Vector3D(dotpos.X, dotpos.Y, -0.1), s.CameraMatrix);
                    MyTransparentGeometry.AddBillboardOriented(_active, Color.White, screenPos, s.CameraMatrix.Left, s.CameraMatrix.Up, (float)screenScale * 0.075f, BlendTypeEnum.PostPP);

                }
            }
        }

        private bool TextStatus(int slot, TargetStatus targetState, float scale, Vector2 localOffset, bool shielded, out string textStr, out Vector2 textOffset)
        {
            var display = shielded || slot < 6 || slot == 10;
            if (!display)
            {
                textStr = string.Empty;
                textOffset = Vector2.Zero;
                return false;
            }

            textOffset = localOffset;

            var aspectScale = (2.37037f / _session.AspectRatio);

            var xOdd = 0.1875f;
            var xEven = 0.035f;
            var yStart = 0.45f;
            var yStep = 0.0755f;

            switch (slot)
            {
                case 0:
                    textStr = $"SIZE: {targetState.SizeExtended}";
                    textOffset.X -= xOdd * aspectScale;
                    textOffset.Y += yStart;
                    break;
                case 1:
                    var inKm = targetState.RealDistance >= 1000;
                    var unit = inKm ? "km" : "m";
                    var measure = inKm ? targetState.RealDistance / 1000 : targetState.RealDistance;
                    textStr = $"RANGE: {measure:#.0} {unit}";
                    textOffset.X += xEven * aspectScale;
                    textOffset.Y += yStart;
                    break;
                case 2:
                    textStr = $"THREAT: {targetState.ThreatLvl}";
                    textOffset.X -= xOdd * aspectScale;
                    textOffset.Y += yStart - (yStep * 1);
                    break;
                case 3:

                    if (targetState.Engagement == 0)
                        textStr = "INTERCEPT";
                    else if (targetState.Engagement == 1)
                        textStr = "RETREATING";
                    else
                        textStr = "STATIONARY";
                    textOffset.X += xEven * aspectScale;
                    textOffset.Y += yStart - (yStep * 1);
                    break;
                case 4:
                    var speed = MathHelper.Clamp(targetState.Speed, 0, int.MaxValue);
                    textStr = $"SPEED: {speed}";
                    textOffset.X -= xOdd * aspectScale;
                    textOffset.Y += yStart - (yStep * 2);
                    break;
                case 5:
                    textStr = targetState.IsFocused ? "FOCUSED" : "OBLIVIOUS";
                    textOffset.X += xEven * aspectScale;
                    textOffset.Y += yStart - (yStep * 2);
                    break;
                case 6:
                    var hp = targetState.ShieldHealth < 0 ? 0 : targetState.ShieldHealth;
                    textStr = $"SHIELD HP: {hp}%";
                    textOffset.X -= xOdd * aspectScale;
                    textOffset.Y += yStart - (yStep * 3);
                    break;
                case 7:
                    var type = targetState.ShieldMod > 0 ? "ENERGY" : targetState.ShieldMod < 0 ? "KINETIC" : "NEUTRAL";
                    var value = !MyUtils.IsZero(targetState.ShieldMod) ? Math.Round(1 / (2 - targetState.ShieldMod), 1) : 1;
                    textStr = $"{type}: {value}x";
                    textOffset.X += xEven * aspectScale;
                    textOffset.Y += yStart - (yStep * 3);
                    break;
                case 8:
                    textStr = "[F,B] [U] [L,R]";
                    textOffset.X -= xOdd * aspectScale;
                    textOffset.Y += yStart - (yStep * 4);
                    break;
                case 9:
                    var reduction = ExpChargeReductions[targetState.ShieldHeat];
                    textStr = $"CHARGE RATE: {Math.Round(1f / reduction, 1)}x";
                    textOffset.X += xEven * aspectScale;
                    textOffset.Y += yStart - (yStep * 4);
                    break;
                case 10:
                    textStr = targetState.Name;
                    textOffset.X -= 0.095f * aspectScale;
                    if (shielded)
                        textOffset.Y += yStart - (yStep * 5);
                    else
                        textOffset.Y += yStart - (yStep * 3);
                    break;
                default:
                    display = false;
                    textStr = string.Empty;
                    textOffset = Vector2.Zero;
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
            var maxNameLength = 18;

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
                var isFcused = false;
                if (grid != null)  {
                    largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
                    GridAi targetAi;
                    GridMap gridMap;
                    if (s.GridToMasterAi.TryGetValue(grid, out targetAi))
                        partCount = targetAi.Construct.BlockCount;
                    else if (s.GridToInfoMap.TryGetValue(grid, out gridMap))
                        partCount = gridMap.MostBlocks;

                    if (targetAi != null && targetAi.Construct.Data.Repo.FocusData.HasFocus)
                    {
                        var fd = targetAi.Construct.Data.Repo.FocusData;

                        foreach (var tId in fd.Target) {

                            if (isFcused)
                                break;

                            foreach (var sub in ai.SubGrids) {

                                if (sub.EntityId == tId) {
                                    isFcused = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                var state = ai.TargetState[i];

                state.IsFocused = isFcused;
                var displayName = target.DisplayName;
                var name = string.IsNullOrEmpty(displayName) ? string.Empty : displayName.Length <= maxNameLength ? displayName : displayName.Substring(0, maxNameLength);

                var targetVel = target.Physics?.LinearVelocity ?? Vector3.Zero;
                if (MyUtils.IsZero(targetVel, 1E-01F)) targetVel = Vector3.Zero;
                var targetDir = Vector3D.Normalize(targetVel);
                var targetRevDir = -targetDir;
                var targetPos = target.PositionComp.WorldAABB.Center;
                var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
                var myHeading = Vector3D.Normalize(myPos - targetPos);

                var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, s.ApproachDegrees);
                var retreat = MathFuncs.IsDotProductWithinTolerance(ref targetRevDir, ref myHeading, s.ApproachDegrees);

                var distanceFromCenters = Vector3D.Distance(ai.MyGrid.PositionComp.WorldAABB.Center, target.PositionComp.WorldAABB.Center);
                distanceFromCenters -= ai.MyGrid.PositionComp.LocalVolume.Radius;
                distanceFromCenters -= target.PositionComp.LocalVolume.Radius;
                distanceFromCenters = distanceFromCenters <= 0 ? 0 : distanceFromCenters;

                var speed = (float)Math.Round(target.Physics?.Speed ?? 0, 1);

                state.Name = name;

                state.RealDistance = distanceFromCenters;

                state.SizeExtended = (float)Math.Round(partCount / (largeGrid ? 100f : 500f), 1);

                state.Speed = speed;

                if (intercept) state.Engagement = 0;
                else if (retreat) state.Engagement = 1;
                else state.Engagement = 2;

                MyTuple<bool, bool, float, float, float, int> shieldInfo = new MyTuple<bool, bool, float, float, float, int>();
                if (s.ShieldApiLoaded) shieldInfo = s.SApi.GetShieldInfo(target);
                if (shieldInfo.Item1)
                {
                    state.ShieldHeat = shieldInfo.Item6 / 10;
                    var modInfo = s.SApi.GetModulationInfo(target);
                    var modValue = MyUtils.IsEqual(modInfo.Item3, modInfo.Item4) ? 0 : modInfo.Item3 > modInfo.Item4 ? modInfo.Item3 : -modInfo.Item4;
                    state.ShieldMod = modValue;
                    state.ShieldHealth = (float) Math.Round(shieldInfo.Item5);
                }
                else
                {
                    state.ShieldHeat = 0;
                    state.ShieldMod = 0;
                    state.ShieldHealth = -1;
                }

                var friend = false;
                if (grid != null && grid.BigOwners.Count != 0)
                {
                    var relation = MyIDModule.GetRelationPlayerBlock(ai.AiOwner, grid.BigOwners[0], MyOwnershipShareModeEnum.Faction);
                    if (relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.Friends) friend = true;
                }

                if (friend) state.ThreatLvl = -1;
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

                    if (offenseRating > 5) state.ThreatLvl = shieldBonus < 0 ? 8 : 9;
                    else if (offenseRating > 4) state.ThreatLvl = 8 + shieldBonus;
                    else if (offenseRating > 3) state.ThreatLvl = 7 + shieldBonus;
                    else if (offenseRating > 2) state.ThreatLvl = 6 + shieldBonus;
                    else if (offenseRating > 1) state.ThreatLvl = 5 + shieldBonus;
                    else if (offenseRating > 0.5) state.ThreatLvl = 4 + shieldBonus;
                    else if (offenseRating > 0.25) state.ThreatLvl = 3 + shieldBonus;
                    else if (offenseRating > 0.125) state.ThreatLvl = 2 + shieldBonus;
                    else if (offenseRating > 0.0625) state.ThreatLvl = 1 + shieldBonus;
                    else if (offenseRating > 0) state.ThreatLvl = shieldBonus > 0 ? 1 : 0;
                    else state.ThreatLvl = -1;
                }
            }
            return validFocus;
        }

    }
}
