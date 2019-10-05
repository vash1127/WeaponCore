using Sandbox.ModAPI;
using VRage.Game.VisualScripting;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            Turret.RefreshCustomInfo();
            if (update && InControlPanel)
            {
                 MyCube.UpdateTerminal();
            }
        }

        private void SaveAndSendAll()
        {
            _firstSync = true;
            if (!_isServer) return;
            Set.SaveSettings();
            Set.NetworkUpdate();
            State.SaveState();
            State.NetworkUpdate();
        }

        internal void UpdateCompPower()
        {
            var shooting = false;
            for (int i = 0; i < Platform.Weapons.Length; i++)
            {
                if (Platform.Weapons[i].IsShooting && Platform.Weapons[i].System.EnergyAmmo) shooting = true;
            }
            if (shooting)
            {
                if (!Ai.AvailablePowerIncrease)
                {
                    if (Ai.ResetPower)
                    {
                        //Log.Line($"grid available: {Ai.GridAvailablePower + Ai.CurrentWeaponsDraw}");
                        Ai.WeaponCleanPower = Ai.GridMaxPower - (Ai.GridCurrentPower - Ai.CurrentWeaponsDraw);
                        Ai.ResetPower = false;
                    }

                    SinkPower = CompPowerPerc * Ai.WeaponCleanPower;

                    DelayTicks += (uint)(5 * MaxRequiredPower / SinkPower) - DelayTicks;
                    ShootTick = DelayTicks + Session.Instance.Tick;
                    Ai.RecalcDone = true;
                }
                else
                {
                    SinkPower = CurrentSinkPowerRequested;
                    Ai.ResetPower = true;
                }

                Sink.Update();
                TerminalRefresh();
            }            
        }
        
        internal void UpdatePivotPos(Weapon weapon)
        {
            if (MyCube == null || weapon.EntityPart == null || TrackingWeapon == null)
            {
                if (MyCube == null) Log.Line($"MyCube null in UpDatePivotPos");
                if (weapon.EntityPart == null) Log.Line($"EntityPart null in UpDatePivotPos");
                return;
            }
            var weaponPComp = weapon.EntityPart.PositionComp;
            Vector3D center;
            if (AimOffset != Vector3D.Zero)
            {
                var startCenter = !FixedOffset ?  MyCube.PositionComp.WorldAABB.Center : weaponPComp.WorldAABB.Center;
                center = startCenter + Vector3D.Rotate(weapon.System.Values.HardPoint.Block.Offset, MyCube.PositionComp.WorldMatrix);
            }
            else center = MyCube.PositionComp.WorldAABB.Center;

            var weaponCenter = weaponPComp.WorldMatrix.Translation;
            var weaponForward = weaponPComp.WorldMatrix.Forward;
            var weaponUp = weaponPComp.WorldMatrix.Up;
            var blockUp = MyCube.PositionComp.WorldMatrix.Up;
            MyPivotDir = weaponForward;
            MyPivotUp = weaponUp;
            MyPivotPos = !FixedOffset ? UtilsStatic.GetClosestPointOnLine1(center, blockUp, weaponCenter, weaponForward) : center;
            if (Debug)
            {
                var cubeleft = MyCube.PositionComp.WorldMatrix.Left;
                MyCenterTestLine = new LineD(center, center + (blockUp * 20));
                MyBarrelTestLine = new LineD(weaponCenter, weaponCenter + (weaponForward * 20));
                MyPivotTestLine = new LineD(MyPivotPos + (cubeleft * 10), MyPivotPos - (cubeleft * 10));
            }

            LastPivotUpdateTick = Session.Instance.Tick;
        }

        public void StopRotSound(bool force)
        {
            if (RotationEmitter != null)
            {
                if (!RotationEmitter.IsPlaying)
                    return;
                RotationEmitter.StopSound(force);
            }
        }

        public void StopAllSounds()
        {
            RotationEmitter?.StopSound(true, true);
            foreach (var w in Platform.Weapons)
            {
                w.StopReloadSound();
                w.StopRotateSound();
                w.StopShooting(true);
            }
        }

        public void StopAllGraphics()
        {
            foreach (var w in Platform.Weapons)
            {
                foreach (var barrels in w.BarrelAvUpdater)
                {
                    var id = barrels.Key.MuzzleId;
                    if (w.System.BarrelEffect1)
                    {
                        if (w.BarrelEffects1?[id] != null)
                        {
                            w.BarrelEffects1[id].Stop(true);
                            w.BarrelEffects1[id] = null;
                        }
                    }
                    if (w.System.BarrelEffect2)
                    {
                        if (w.BarrelEffects2?[id] != null)
                        {
                            w.BarrelEffects2[id].Stop(true);
                            w.BarrelEffects2[id] = null;
                        }
                    }
                    if (w.HitEffects?[id] != null)
                    {
                        w.HitEffects[id].Stop(true);
                        w.HitEffects[id] = null;
                    }
                }
            }
        }

        public void StopAllAv()
        {
            StopAllSounds();
            StopAllGraphics();
        }
    }
}
