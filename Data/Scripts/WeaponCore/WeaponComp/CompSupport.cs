using Sandbox.ModAPI;
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
            var weaponPComp = weapon.EntityPart.PositionComp;
            var weaponCenter = weaponPComp.WorldMatrix.Translation;
            var weaponForward = weaponPComp.WorldMatrix.Forward;
            var weaponUp = weaponPComp.WorldMatrix.Up;


            Vector3D center;
            if (AimOffset != Vector3D.Zero)
            {
                var blockCenter = MyCube.PositionComp.WorldAABB.Center;
                center = blockCenter + Vector3D.Rotate(TrackingWeapon.System.Values.HardPoint.Block.Offset, MyCube.CubeGrid.WorldMatrix);
            }
            else center = MyCube.PositionComp.WorldAABB.Center;

            var blockUp = MyCube.PositionComp.WorldMatrix.Up;
            MyPivotDir = weaponForward;
            MyPivotUp = weaponUp;
            MyPivotPos = UtilsStatic.GetClosestPointOnLine1(center, blockUp, weaponCenter, weaponForward);
            if (Debug)
            {
                var cubeleft = MyCube.PositionComp.WorldMatrix.Left;
                MyCenterTestLine = new LineD(center, center + (blockUp * 10));
                MyBarrelTestLine = new LineD(weaponCenter, weaponCenter + (weaponForward * 10));
                MyPivotTestLine = new LineD(MyPivotPos + (cubeleft * 5), MyPivotPos - (cubeleft * 5));
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
