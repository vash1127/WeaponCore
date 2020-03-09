using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.Platform.Weapon.TerminalActionState;
using static WeaponCore.Platform.MyWeaponPlatform.PlatformState;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    internal class ApiBackend
    {
        private readonly Session _session;
        internal readonly Dictionary<string, Delegate> ModApiMethods;
        private readonly Dictionary<string, Delegate> _terminalPbApiMethods;

        internal ApiBackend(Session session)
        {
            _session = session;

            ModApiMethods = new Dictionary<string, Delegate>()
            {
                ["GetAllCoreWeapons"] = new Func<List<MyDefinitionId>>(GetAllCoreWeapons),
                ["GetCoreStaticLaunchers"] = new Func<List<MyDefinitionId>>(GetCoreStaticLaunchers),
                ["GetCoreTurrets"] = new Func<IList<MyDefinitionId>>(GetCoreTurrets),
                ["SetTargetEntity"] = new Action<IMyEntity, IMyEntity, int>(SetTargetEntity),
                ["FireOnce"] = new Action<IMyTerminalBlock, bool, int>(FireOnce),
                ["ToggleFire"] = new Action<IMyTerminalBlock, bool, bool, int>(ToggleFire),
                ["WeaponReady"] = new Func<IMyTerminalBlock, int, bool, bool, bool>(WeaponReady),
                ["GetMaxRange"] = new Func<IMyTerminalBlock,int, float>(GetMaxRange),
                ["GetTurretTargetTypes"] = new Func<IMyTerminalBlock, IList<IList<string>>>(GetTurretTargetTypes),
                ["SetTurretTargetTypes"] = new Action<IMyTerminalBlock, IList<IList<string>>>(SetTurretTargetTypes),
                ["SetTurretRange"] = new Action<IMyTerminalBlock, float>(SetBlockTrackingRange),
                ["GetTargetedEntity"] = new Func<IMyTerminalBlock, IList<IMyEntity>>(GetTargetedEntity),
                ["IsTargetAligned"] = new Func<IMyTerminalBlock, IMyEntity, int, bool>(IsTargetAligned),
                ["GetPredictedTargetPosition"] = new Func<IMyTerminalBlock, IMyEntity, int, Vector3D?>(GetPredictedTargetPosition),
                ["GetHeatLevel"] = new Func<IMyTerminalBlock, float>(GetHeatLevel),
                ["CurrentPower"] = new Func<IMyTerminalBlock, float>(CurrentPower),
                ["MaxPower"] = new Func<MyDefinitionId, float>(MaxPower),
                ["DisableRequiredPower"] = new Action<IMyTerminalBlock>(DisableRequiredPower),
                ["GetAllWeaponDefinitions"] = new Action<IList<byte[]>>(GetAllWeaponDefinitions),
                ["GetBlockWeaponMap"] = new Func<IMyTerminalBlock, IDictionary<string, int>, bool>(GetBlockWeaponMap),
            };

            _terminalPbApiMethods = new Dictionary<string, Delegate>()
            {
                ["SetTargetEntity"] = new Action<IMyEntity, IMyEntity, int>(SetTargetEntity),
                ["FireOnce"] = new Action<IMyTerminalBlock, bool, int>(FireOnce),
                ["ToggleFire"] = new Action<IMyTerminalBlock, bool, bool, int>(ToggleFire),
                ["WeaponReady"] = new Func<IMyTerminalBlock, int, bool, bool, bool>(WeaponReady),
                ["GetMaxRange"] = new Func<IMyTerminalBlock, int, float>(GetMaxRange),
                ["GetTurretTargetTypes"] = new Func<IMyTerminalBlock, IList<IList<string>>>(GetTurretTargetTypes),
                ["SetTurretTargetTypes"] = new Action<IMyTerminalBlock, IList<IList<string>>>(SetTurretTargetTypes),
                ["SetTurretRange"] = new Action<IMyTerminalBlock, float>(SetBlockTrackingRange),
                ["GetTargetedEntity"] = new Func<IMyTerminalBlock, IList<IMyEntity>>(GetTargetedEntity),
                ["IsTargetAligned"] = new Func<IMyTerminalBlock, IMyEntity, int, bool>(IsTargetAligned),
                ["GetPredictedTargetPosition"] = new Func<IMyTerminalBlock, IMyEntity, int, Vector3D?>(GetPredictedTargetPosition),
                ["GetHeatLevel"] = new Func<IMyTerminalBlock, float>(GetHeatLevel),
                ["CurrentPower"] = new Func<IMyTerminalBlock, float>(CurrentPower),
                ["MaxPower"] = new Func<MyDefinitionId, float>(MaxPower)
            };
        }

        internal void Init()
        {
            var pb = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("WeaponCorePbAPI");
            pb.Getter = (b) => _terminalPbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyProgrammableBlock>(pb);
        }

        private void GetAllWeaponDefinitions(IList<byte[]> collection)
        {
            foreach (var wepDef in _session.WeaponDefinitions)
                collection.Add(MyAPIGateway.Utilities.SerializeToBinary(wepDef));
        }

        private void GetAllCoreWeapons(IList<MyDefinitionId> collection)
        {
            foreach (var def in _session.WeaponCoreBlockDefs.Values)
                collection.Add(def);
        }

        private void GetCoreStaticLaunchers(IList<MyDefinitionId> collection)
        {
            foreach (var def in _session.WeaponCoreFixedBlockDefs)
                collection.Add(def);
        }

        private void GetCoreTurrets(IList<MyDefinitionId> collection)
        {
            foreach (var def in _session.WeaponCoreTurretBlockDefs)
                collection.Add(def);
        }

        private bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];
                    collection.Add(w.System.WeaponName, w.WeaponId);
                }
                return true;
            }
            return false;
        }


        private MyTuple<bool, int, int> ProjectilesLockedOn(IMyEntity victim)
        {
            var grid = victim.GetTopMostParent() as MyCubeGrid;
            GridAi gridAi;
            MyTuple<bool, int, int> tuple;
            if (grid != null && _session.GridTargetingAIs.TryGetValue(grid, out gridAi))
            {
                var count = gridAi.LiveProjectile.Count;
                tuple = count > 0 ? new MyTuple<bool, int, int>(true, count, (int) (_session.Tick - gridAi.LiveProjectileTick)) : new MyTuple<bool, int, int>(false, 0, -1);
            }
            else tuple = new MyTuple<bool, int, int>(false, 0, -1);
            return tuple;
        }

        private void GetSortedThreats(IMyEntity shooter, IDictionary<IMyEntity, float> collection)
        {
            var grid = shooter.GetTopMostParent() as MyCubeGrid;
            GridAi gridAi;
            if (grid != null && _session.GridTargetingAIs.TryGetValue(grid, out gridAi))
            {
                for (int i = 0; i < gridAi.SortedTargets.Count; i++)
                {
                    var targetInfo = gridAi.SortedTargets[i];
                    collection.Add(targetInfo.Target, targetInfo.OffenseRating);
                }
            }
        }

        private IMyEntity GetAiFocus(IMyEntity shooter, int priority = 0)
        {
            var shootingGrid = shooter.GetTopMostParent() as MyCubeGrid;

            if (shootingGrid != null)
            {
                GridAi ai;
                if (_session.GridTargetingAIs.TryGetValue(shootingGrid, out ai))
                    return ai.Focus.Target[priority];
            }
            return null;
        }

        private bool SetAiFocus(IMyEntity shooter, IMyEntity target, int priority = 0)
        {
            var shootingGrid = shooter.GetTopMostParent() as MyCubeGrid;

            if (shootingGrid != null)
            {
                GridAi ai;
                if (_session.GridTargetingAIs.TryGetValue(shootingGrid, out ai))
                {
                    ai.Focus.ReassignTarget((MyEntity)target, priority, ai);
                    return true;
                }
            }
            return false;
        }

        private static MyTuple<bool, bool, bool, IMyEntity> GetWeaponTarget(IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                if (weapon.Target.IsFakeTarget)
                    return new MyTuple<bool, bool, bool, IMyEntity>(true, false, true, null);
                if (weapon.Target.IsProjectile)
                    return new MyTuple<bool, bool, bool, IMyEntity>(true, true, false, null);
                return new MyTuple<bool, bool, bool, IMyEntity>(weapon.Target.Entity != null, false, false, weapon.Target.Entity);
            }

            return new MyTuple<bool, bool, bool, IMyEntity>(false, false, false, null);
        }


        private static void SetWeaponTarget(IMyTerminalBlock weaponBlock, IMyEntity target, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                GridAi.AcquireTarget(comp.Platform.Weapons[weaponId], false, (MyEntity)target);
        }

        private static void FireOnce(IMyTerminalBlock weaponBlock, bool allWeapons = true, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (!allWeapons && i != weaponId) continue;

                    var w = comp.Platform.Weapons[i];
                    
                    if (w.State.ManualShoot != ShootOff)
                        w.State.ManualShoot = ShootOnce;
                    else
                        w.State.ManualShoot = ShootOnce;
                }
            }
        }

        private static void ToggleFire(IMyTerminalBlock weaponBlock, bool on, bool allWeapons = true, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (!allWeapons && i != weaponId) continue;

                    var w = comp.Platform.Weapons[i];

                    if (!on && w.State.ManualShoot == ShootOn)
                    {
                        w.State.ManualShoot = ShootOff;
                        w.StopShooting();
                    }
                    else if (on && w.State.ManualShoot != ShootOff)
                        w.State.ManualShoot = ShootOn;
                    else if (on)
                        w.State.ManualShoot = ShootOn;
                }
            }
        }

        private static bool WeaponReady(IMyTerminalBlock weaponBlock, int weaponId = 0, bool anyWeaponReady = true, bool shotReady = false)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.State.Value.Online && comp.Set.Value.Overrides.Activate)
            {
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (!anyWeaponReady && i != weaponId) continue;
                    var w = comp.Platform.Weapons[i];
                    if (w.ShotReady) return true;
                }
            }

            return false;
        }

        private static float GetMaxRange(IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                return (float)comp.Platform.Weapons[weaponId].ActiveAmmoDef.Const.MaxTrajectory;

            return 0f;
        }

        private static bool GetTurretTargetTypes(IMyTerminalBlock weaponBlock, IList<string> collection, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                var weapon = comp.Platform.Weapons[weaponId];
                var threats = weapon.System.Values.Targeting.Threats;
                for (int i = 0; i < threats.Length; i++) collection.Add(threats[i].ToString());
                return true;
            }
            return false;
        }

        private static void SetTurretTargetTypes(IMyTerminalBlock weaponBlock, IList<string> collection, int weaponId = 0)
        {

        }

        private static void SetBlockTrackingRange(IMyTerminalBlock weaponBlock, float range)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                var maxTrajectory = GetMaxRange(weaponBlock);

                comp.Set.Value.Range = range > maxTrajectory ? maxTrajectory : range;
            }
        }

        private static bool IsTargetAligned(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                var w = comp.Platform.Weapons[weaponId];

                w.NewTarget.Entity = (MyEntity) targetEnt;

                Vector3D targetPos;
                return Weapon.TargetAligned(w, w.NewTarget, out targetPos);
            }
            return false;
        }

        private static bool CanShootTarget(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                if (comp.Platform.State != Ready) return false;
                var w = comp.Platform.Weapons[weaponId];
                var topMost = targetEnt.GetTopMostParent();
                var targetVel = topMost.Physics?.LinearVelocity ?? Vector3.Zero;
                var targetAccel = topMost.Physics?.AngularAcceleration ?? Vector3.Zero;

                return Weapon.CanShootTargetObb(w, (MyEntity)targetEnt, targetVel, targetAccel);
            }
            return false;
        }

        private static Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                var w = comp.Platform.Weapons[weaponId];
                w.NewTarget.Entity = (MyEntity)targetEnt;

                Vector3D targetPos;
                Weapon.TargetAligned(w, w.NewTarget, out targetPos);
                return targetPos;
            }
            return null;
        }

        private static float GetHeatLevel(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {

                if (comp.Platform.State != Ready || comp.MaxHeat <= 0) return 0f;

                return comp.State.Value.Heat / comp.MaxHeat;

            }

            return 0f;
        }

        private static float CurrentPower(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                return comp.SinkPower;

            return 0f;
        }

        private float MaxPower(MyDefinitionId weaponDef)
        {
            float power = 0f;
            WeaponStructure weapons;
            if (_session.WeaponPlatforms.TryGetValue(weaponDef.SubtypeId, out weapons))
            {
                foreach(var systems in weapons.WeaponSystems)
                {
                    var system = systems.Value;

                    /*
                    if (!system.EnergyAmmo && !system.IsHybrid)
                        power += 0.001f;
                    else
                    {
                        var ewar = (int)system.Values.Ammo.AreaEffect.AreaEffect > 3;
                        var shotEnergyCost = ewar ? system.Values.HardPoint.EnergyCost * system.Values.Ammo.AreaEffect.AreaEffectDamage : system.Values.HardPoint.EnergyCost * system.Values.Ammo.BaseDamage;

                        power += ((shotEnergyCost * (system.RateOfFire * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * system.Values.HardPoint.Loading.BarrelsPerShot) * system.Values.HardPoint.Loading.TrajectilesPerBarrel;
                    }
                    */

                }
            }
            return power;
        }

        private static void DisableRequiredPower(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                comp.UnlimitedPower = true;
        }

        private bool HasGridAi(IMyEntity entity)
        {
            var grid = entity.GetTopMostParent() as MyCubeGrid;

            return grid != null && _session.GridTargetingAIs.ContainsKey(grid);
        }

        private static bool HasCoreWeapon(IMyTerminalBlock weaponBlock)
        {
            return weaponBlock.Components.Has<WeaponComponent>();
        }
    }
}
