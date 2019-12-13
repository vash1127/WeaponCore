using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        /*
        private void PowerInit()
        {
            SinkInfo = new MyResourceSinkInfo()
            {
                ResourceTypeId = GId,
                MaxRequiredInput = 0f,
                RequiredInputFunc = () => SinkPower,
            };
            MyCube.ResourceSink.Init(MyStringHash.GetOrCompute("Defense"), SinkInfo);
            MyCube.ResourceSink.TemporaryConnectedEntity = MyCube;
            //MyCube.ResourceSink.SetRequiredInputFuncByType(GId, () => SinkPower);
            //MyCube.ResourceSink.SetMaxRequiredInputByType(GId, 0);

            MyCube.ResourceSink.Update();
        }
        */
        private void PowerInit()
        {
            var resourceInfo = new MyResourceSinkInfo()
            {
                ResourceTypeId = GId,
                MaxRequiredInput = 0f,
                RequiredInputFunc = () => SinkPower,
            };
            MyCube.Components.TryGet(out Sink);
            var gId = GId;
            Sink.RemoveType(ref gId);
            Sink.Init(MyStringHash.GetOrCompute("Defense"), resourceInfo);
            Sink.AddType(ref resourceInfo);
            Sink.Update();
        }
        private bool EntityAlive()
        {
            if (Ai.MyGrid?.Physics == null) return false;
            //if (!_firstSync && _readyToSync) SaveAndSendAll();
            if (!_isDedicated && _count == 29) TerminalRefresh();

            if (!_allInited && !PostInit()) return false;

            if (ClientUiUpdate || SettingsUpdated) UpdateSettings();
            return true;
        }

        private bool PostInit()
        {
            if (!_isServer && _clientNotReady) return false;
            //Session.Instance.CreateLogicElements(Turret);
            //WepUi.CreateUi(Turret);
            if (_isServer && !IsFunctional) return false;

            if (_mpActive && _isServer) State.NetworkUpdate();

            _allInited = true;
            return true;
        }

        private void StorageSetup()
        {
            var isServer = MyAPIGateway.Multiplayer.IsServer;

            if (State == null)
            {
                //Log.Line($"State null");
                State = new CompState(this);
            }

            if (IsSorterTurret)
            {
                if (SorterBase.Storage == null)
                {
                    //Log.Line("Storage null");
                    State.StorageInit();
                }
            }
            else
            {
                if (MissileBase.Storage == null)
                {
                    //Log.Line("Storage null");
                    State.StorageInit();
                }
            }

            if (Set == null)
            {
                //Log.Line($"Settings null");
                Set = new CompSettings(this);
            }

            State.LoadState();

            Set.LoadSettings();
            if (!State.LoadState() && !isServer) _clientNotReady = true;
            UpdateSettings(Set.Value);
            if (isServer)
            {
                foreach (var w in State.Value.Weapons) {
                    w.Heat = 0;
                }
            }
        }

        private void DpsAndHeatInit(Weapon weapon)
        {
            MaxHeat += weapon.System.MaxHeat;
            weapon.RateOfFire = (int)(weapon.System.RateOfFire * Set.Value.RofModifier);
            weapon.BarrelSpinRate = (int)(weapon.System.BarrelSpinRate * Set.Value.RofModifier);
            if (weapon.System.EnergyAmmo)
                weapon.BaseDamage = weapon.System.BaseDamage * Set.Value.DpsModifier;
            else
                weapon.BaseDamage = weapon.System.BaseDamage;

            if (weapon.System.IsBeamWeapon)
                weapon.BaseDamage *= Set.Value.Overload;

            if (weapon.BaseDamage < 0)
                weapon.BaseDamage = 0;

            if (weapon.RateOfFire < 1)
                weapon.RateOfFire = 1;

            weapon.UpdateShotEnergy();
            weapon.UpdateRequiredPower();

            var mulitplier = (weapon.System.EnergyAmmo && weapon.System.BaseDamage > 0) ? weapon.BaseDamage / weapon.System.BaseDamage : 1;

            if (weapon.BaseDamage > weapon.System.BaseDamage)
                mulitplier *= mulitplier;

            weapon.HeatPShot = weapon.System.HeatPerShot * mulitplier;
            weapon.AreaEffectDmg = weapon.System.AreaEffectDamage * mulitplier;
            weapon.DetonateDmg = weapon.System.DetonationDamage * mulitplier;

            MaxRequiredPower -= weapon.RequiredPower;
            weapon.RequiredPower *= mulitplier;
            MaxRequiredPower += weapon.RequiredPower;

            weapon.TicksPerShot = (uint)(3600f / weapon.RateOfFire);
            weapon.TimePerShot = (3600d / weapon.RateOfFire);

            weapon.Dps = (60 / (float)weapon.TicksPerShot) * weapon.BaseDamage * weapon.System.BarrelsPerShot;

            if (weapon.System.Values.Ammo.AreaEffect.AreaEffect != AreaDamage.AreaEffectType.Disabled)
            {
                if (weapon.System.Values.Ammo.AreaEffect.Detonation.DetonateOnEnd)
                    weapon.Dps += (weapon.DetonateDmg / 2) * (weapon.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                        ? weapon.System.Values.Ammo.Trajectory.AccelPerSec /
                                        weapon.System.Values.Ammo.Trajectory.DesiredSpeed
                                        : 1);
                else
                    weapon.Dps += (weapon.AreaEffectDmg / 2) *
                                    (weapon.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                        ? weapon.System.Values.Ammo.Trajectory.AccelPerSec /
                                        weapon.System.Values.Ammo.Trajectory.DesiredSpeed
                                        : 1);
            }

            HeatPerSecond += (60 / (float)weapon.TicksPerShot) * weapon.HeatPShot * weapon.System.BarrelsPerShot;
            OptimalDps += weapon.Dps;

            HeatSinkRate += weapon.HsRate;
        }

        private void InventoryInit()
        {
            BlockInventory = (MyInventory)MyCube.GetInventoryBase();
            if (MyCube is IMyConveyorSorter) BlockInventory.Constraint = new MyInventoryConstraint("ammo");
            BlockInventory.Constraint.m_useDefaultIcon = false;
            BlockInventory.ResetVolume();
            BlockInventory.Refresh();

            if (Set.Value.Inventory != null)
                BlockInventory.Init(Set.Value.Inventory);
            
            foreach (var weapon in Platform.Weapons)
                MaxInventoryVolume += weapon.System.MaxAmmoVolume;

            if (MyCube.HasInventory)
            {
                BlockInventory.FixInventoryVolume(MaxInventoryVolume);

                BlockInventory.Constraint.Clear();

                foreach (var w in Platform.Weapons)
                {
                    var magId = w.System.MagazineDef.Id;
                    BlockInventory.Constraint.Add(magId);
                }
                BlockInventory.Refresh();
            }
            InventoryInited = true;
        }
    }
}
