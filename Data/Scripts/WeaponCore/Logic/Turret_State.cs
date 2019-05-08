using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    partial class Logic
    {
        private bool EntityAlive()
        {
            _tick = Session.Instance.Tick;
            if (MyGrid?.Physics == null) return false;
            if (!_firstSync && _readyToSync) SaveAndSendAll();
            if (!_isDedicated && _count == 29) TerminalRefresh();

            if (!_allInited && !PostInit()) return false;

            if (ClientUiUpdate || SettingsUpdated) UpdateSettings();
            return true;
        }

        private Status WeaponState()
        {
            if (_isServer)
            {
                if (OverHeating()) return Status.OverHeating;
                if (WarmingUp()) return Status.WarmingUp;
                if (Offline()) return Status.Offline;
            }
            else if (!State.Value.Online) return Status.Offline;

            Online();
            return Status.Online;
        }

        private bool OverHeating()
        {
            return false;
        }

        private bool WarmingUp()
        {

            return false;
        }

        internal bool Offline()
        {
            return false;
        }

        internal void FailWeapon(Status state)
        {
            var on = state != Status.Offline;
            var cool = state != Status.OverHeating;
            var initing = state != Status.WarmingUp;
            var keepCharge = on && !cool;
            var clear = !on && !initing;
            OfflineWeapon(clear, state, keepCharge);
        }

        internal void Online()
        {
            _currentShootTime = Gun.GunBase.LastShootTime.Ticks;
            if ((_currentShootTime != _lastShootTime))
            {
                //Log.Line($"Shotting on Tick:{_tick} - ShootInv:{Gun.GunBase.ShootIntervalInMiliseconds} - {Gun.GunBase.GetMuzzleWorldPosition()}");
                Gun.GunBase.CurrentAmmo += 1;
                ShotsFired = true;
                _lastShootTick = _tick;
                _lastShootTime = _currentShootTime;
            }

            Turret.TrackTarget(MyAPIGateway.Session.Player.Character);

            if (Turret.HasTarget)
            {
                var targetPos = Turret.Target.PositionComp.WorldAABB.Center;

                var myPivotPos = Turret.WorldAABB.Center;
                //var upOffset = -(Vector3D.Normalize(Turret.WorldMatrix.Down - Turret.WorldMatrix.Up) * 1.04848f);
                //myPivotPos += upOffset;
                //var forwardOffset = (Vector3D.Normalize(Turret.WorldMatrix.Left - Turret.WorldMatrix.Right) * 0.37814f);
                //myPivotPos += forwardOffset;
                var len = Platform.Weapons[0].UpPivotOffsetLen;
                myPivotPos -= Vector3D.Normalize(Turret.WorldMatrix.Down - Turret.WorldMatrix.Up) * len;

                var myMatrix = Turret.WorldMatrix;
                GetTurretAngles(ref targetPos, ref myPivotPos, ref myMatrix, out _azimuth, out _elevation);
                var currentAz = Turret.Azimuth;
                var currentEl = Turret.Elevation;

                var stepaz = sameSign(currentAz, _azimuth);
                var stepel = sameSign(currentEl, _elevation);
                if (stepaz)
                {
                    var diffAz = MathHelper.Clamp(_azimuth - currentAz, -_step, _step);
                    Turret.Azimuth += (float)diffAz;
                }
                else Turret.Azimuth = (float)_azimuth;
                if (stepel)
                {
                    var diffEl = MathHelper.Clamp(_elevation - currentEl, -_step, _step);
                    Turret.Elevation += (float)diffEl;
                }
                else Turret.Elevation = (float)_elevation;
            }

            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = Targeting.TargetBlocks;
            foreach (var targets in allTargets)
            {
                if (targets.Key != null)
                {
                    foreach (var b in targets.Value)
                    {
                        // Log.Line($"{b.DebugName}");
                    }
                }
            }
            var parts = Platform.Weapons;
            for (int i = 0; i < parts.Length; i++)
            {
                //Dsutil1.Sw.Restart();
                var weapon = parts[i];

                if (ShotsFired && weapon.WeaponSystem.WeaponType.RotateAxis == 3) weapon.MovePart(0.3f, -1, false, false, true);
                if (weapon.PosChangedTick > weapon.PosUpdatedTick)
                {
                    for (int j = 0; j < weapon.Muzzles.Length; j++)
                    {
                        var dummy = weapon.Dummies[j];
                        var newInfo = dummy.Info;
                        weapon.Muzzles[j].Direction = newInfo.Direction;
                        weapon.Muzzles[j].Position = newInfo.Position;
                        weapon.Muzzles[j].LastPosUpdate = _tick;
                    }
                }

                if (_tick - weapon.PosChangedTick > 10)
                    weapon.PosUpdatedTick = _tick;
                //Dsutil1.StopWatchReport("test", -1);
                var cc = 0;
                foreach (var m in weapon.Muzzles)
                {
                    var color = Color.Red;
                    if (cc % 2 == 0) color = Color.Blue;
                    //Log.Line($"{m.Position} - {m.Direction}");
                    if (i == 0) DsDebugDraw.DrawLine(m.Position, m.Position + (m.Direction * 1000), color, 0.02f);
                    cc++;
                }
            }
        }

        internal void OfflineWeapon(bool clear, Status reason, bool keepCharge = false)
        {
            DefaultWeaponState(clear, keepCharge);

            if (_isServer) UpdateNetworkState();
            else TerminalRefresh();
        }

        private void DefaultWeaponState(bool clear, bool keepHeat)
        {
            var state = State;
            NotFailed = false;
            if (clear)
            {
                SinkPower = 0.001f;
                Sink.Update();

                if (_isServer && !keepHeat)
                {
                    state.Value.Online = false;
                    state.Value.Heat = 0;
                }
            }
            else if (_isServer)
                state.Value.Online = false;

            TerminalRefresh(false);
        }

        private void ComingOnline()
        {
            _firstLoop = false;
            NotFailed = true;
            WarmedUp = true;

            if (_isServer)
            {
                UpdateNetworkState();
            }
            else
            {
                TerminalRefresh();
            }
        }

        private bool ClientOfflineStates()
        {
            var shieldUp = State.Value.Online && !State.Value.Overload;

            if (!shieldUp)
            {
                if (_clientOn)
                {
                    //ClientDown();
                    _clientOn = false;
                    TerminalRefresh();
                }
                return true;
            }

            if (!_clientOn)
            {
                ComingOnline();
                _clientOn = true;
            }
            return false;
        }

        internal void UpdateSettings(LogicSettingsValues newSettings)
        {
            if (newSettings.MId > Set.Value.MId)
            {
                Set.Value = newSettings;
                SettingsUpdated = true;
                
            }
        }

        internal void UpdateState(LogicStateValues newState)
        {
            if (newState.MId > State.Value.MId)
            {
                if (!_isServer)
                {

                    if (State.Value.Message) BroadcastMessage();
                }
                State.Value = newState;
                _clientNotReady = false;
            }
        }

        private void UpdateSettings()
        {
            if (Session.Instance.Tick % 33 == 0)
            {
                if (SettingsUpdated)
                {

                    SettingsUpdated = false;
                    Set.SaveSettings();
                    if (_isServer)
                    {
                        UpdateNetworkState();
                    }
                }
            }
            else if (Session.Instance.Tick % 34 == 0)
            {
                if (ClientUiUpdate)
                {
                    ClientUiUpdate = false;
                    if (!_isServer) Set.NetworkUpdate();
                }
            }
        }

        internal void UpdateNetworkState()
        {
            if (Session.Instance.MpActive)
            {
                State.NetworkUpdate();
                if (_isServer) TerminalRefresh(false);
            }

            if (!_isDedicated && State.Value.Message) BroadcastMessage();

            State.Value.Message = false;
            State.SaveState();
        }
    }
}
