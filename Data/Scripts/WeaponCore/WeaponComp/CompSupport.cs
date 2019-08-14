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
            if (update && InControlPanel && InThisTerminal)
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
                if (Platform.Weapons[i].IsShooting) shooting = true;
            }
            if (shooting)
            {
                if (Ai.ResetPower)
                {
                    ///Log.Line($"grid available: {gridAi.GridAvailablePower + gridAi.CurrentWeaponsDraw}");
                    Ai.WeaponCleanPower = Ai.GridMaxPower - (Ai.GridCurrentPower - Ai.CurrentWeaponsDraw);
                    Ai.ResetPower = false;
                }

                if (!Ai.AvailablePowerIncrease)
                {
                    SinkPower = CompPowerPerc * Ai.WeaponCleanPower;

                    DelayTicks += (uint)(5 * MaxRequiredPower / SinkPower) - DelayTicks;
                    ShootTick = DelayTicks + Ai.MySession.Tick;
                }
                else
                {
                    SinkPower = CurrentSinkPowerRequested;
                    DelayTicks = 0;
                    ShootTick = 0;
                }

                Sink.Update();
                TerminalRefresh();
            }
            Ai.RecalcDone = true;
        }

        internal void UpdatePivotPos(Weapon weapon)
        {
            var weaponPComp = weapon.EntityPart.PositionComp;
            var weaponCenter = weaponPComp.WorldMatrix.Translation;
            var weaponForward = weaponPComp.WorldMatrix.Forward;
            //var weaponBackward = weaponPComp.WorldMatrix.Backward;
            var weaponUp = weaponPComp.WorldMatrix.Up;

            var blockCenter = MyCube.PositionComp.WorldAABB.Center;
            var blockUp = MyCube.PositionComp.WorldMatrix.Up;
            MyPivotDir = weaponForward;
            MyPivotUp = weaponUp;
            MyPivotPos = UtilsStatic.GetClosestPointOnLine1(blockCenter, blockUp, weaponCenter, weaponForward);
            //MyPivotTestLine = new LineD(blockCenter, MyPivotPos);
            //MyBarrelTestLine = new LineD(weaponCenter + (weaponBackward * 5), weaponCenter + (weaponForward * 5));
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
