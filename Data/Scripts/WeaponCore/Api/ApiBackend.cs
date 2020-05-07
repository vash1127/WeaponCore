using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using static WeaponCore.Platform.Weapon.ManualShootActionState;
using static WeaponCore.Platform.MyWeaponPlatform.PlatformState;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore.Api
{
    internal class ApiBackend
    {
        private readonly Session _session;
        internal readonly Dictionary<string, Delegate> ModApiMethods;

        internal ApiBackend(Session session)
        {
            _session = session;

            ModApiMethods = new Dictionary<string, Delegate>()
            {
                ["GetAllWeaponDefinitions"] = new Action<IList<byte[]>>(GetAllWeaponDefinitions),
                ["GetCoreWeapons"] = new Action<ICollection<MyDefinitionId>>(GetCoreWeapons),
                ["GetCoreStaticLaunchers"] = new Action<ICollection<MyDefinitionId>>(GetCoreStaticLaunchers),
                ["GetCoreTurrets"] = new Action<ICollection<MyDefinitionId>>(GetCoreTurrets),
                ["GetBlockWeaponMap"] = new Func<IMyTerminalBlock, IDictionary<string, int>, bool>(GetBlockWeaponMap),
                ["GetProjectilesLockedOn"] = new Func<IMyEntity, MyTuple<bool, int, int>>(GetProjectilesLockedOn),
                ["GetSortedThreats"] = new Action<IMyEntity, ICollection<MyTuple<IMyEntity, float>>>(GetSortedThreats),
                ["GetAiFocus"] = new Func<IMyEntity, int, IMyEntity>(GetAiFocus),
                ["SetAiFocus"] = new Func<IMyEntity, IMyEntity, int, bool>(SetAiFocus),
                ["GetWeaponTarget"] = new Func<IMyTerminalBlock, int, MyTuple<bool, bool, bool, IMyEntity>>(GetWeaponTarget),
                ["SetWeaponTarget"] = new Action<IMyTerminalBlock, IMyEntity, int>(SetWeaponTarget),
                ["FireWeaponOnce"] = new Action<IMyTerminalBlock, bool, int>(FireWeaponOnce),
                ["ToggleWeaponFire"] = new Action<IMyTerminalBlock, bool, bool, int>(ToggleWeaponFire),
                ["IsWeaponReadyToFire"] = new Func<IMyTerminalBlock, int, bool, bool, bool>(IsWeaponReadyToFire),
                ["GetMaxWeaponRange"] = new Func<IMyTerminalBlock,int, float>(GetMaxWeaponRange),
                ["GetTurretTargetTypes"] = new Func<IMyTerminalBlock, ICollection<string>, int, bool>(GetTurretTargetTypes),
                ["SetTurretTargetTypes"] = new Action<IMyTerminalBlock, ICollection<string>, int>(SetTurretTargetTypes),
                ["SetBlockTrackingRange"] = new Action<IMyTerminalBlock, float>(SetBlockTrackingRange),
                ["IsTargetAligned"] = new Func<IMyTerminalBlock, IMyEntity, int, bool>(IsTargetAligned),
                ["CanShootTarget"] = new Func<IMyTerminalBlock, IMyEntity, int, bool>(CanShootTarget),
                ["GetPredictedTargetPosition"] = new Func<IMyTerminalBlock, IMyEntity, int, Vector3D?>(GetPredictedTargetPosition),
                ["GetHeatLevel"] = new Func<IMyTerminalBlock, float>(GetHeatLevel),
                ["GetCurrentPower"] = new Func<IMyTerminalBlock, float>(GetCurrentPower),
                ["GetMaxPower"] = new Func<MyDefinitionId, float>(GetMaxPower),
                ["DisableRequiredPower"] = new Action<IMyTerminalBlock>(DisableRequiredPower),
                ["HasGridAi"] = new Func<IMyEntity, bool>(HasGridAi),
                ["HasCoreWeapon"] = new Func<IMyTerminalBlock, bool>(HasCoreWeapon),
                ["GetOptimalDps"] = new Func<IMyEntity, float>(GetOptimalDps),
                ["GetActiveAmmo"] = new Func<IMyTerminalBlock, int, string>(GetActiveAmmo),
                ["SetActiveAmmo"] = new Action<IMyTerminalBlock, int, string>(SetActiveAmmo),
                ["RegisterProjectileAdded"] = new Action<Action<Vector3, float>>(RegisterProjectileAddedCallback),
                ["UnRegisterProjectile"] = new Action<Action<Vector3, float>>(UnRegisterProjectileAddedCallback),
                ["GetConstructEffectiveDps"] = new Func<IMyEntity, float>(GetConstructEffectiveDps),
            };
        }

        private void GetAllWeaponDefinitions(IList<byte[]> collection)
        {
            foreach (var wepDef in _session.WeaponDefinitions)
                collection.Add(MyAPIGateway.Utilities.SerializeToBinary(wepDef));
        }

        private void GetCoreWeapons(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.WeaponCoreBlockDefs.Values)
                collection.Add(def);
        }

        private void GetCoreStaticLaunchers(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.WeaponCoreFixedBlockDefs)
                collection.Add(def);
        }

        private void GetCoreTurrets(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.WeaponCoreTurretBlockDefs)
                collection.Add(def);
        }

        private bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection)
        {
            WeaponStructure weaponStructure;
            if (_session.WeaponPlatforms.TryGetValue(weaponBlock.SlimBlock.BlockDefinition.Id.SubtypeId, out weaponStructure))
            {
                foreach (var weaponSystem in weaponStructure.WeaponSystems.Values)
                {
                    var system = weaponSystem;
                    collection.Add(system.WeaponName, system.WeaponId);
                }
                return true;
            }
            return false;
        }

        private MyTuple<bool, int, int> GetProjectilesLockedOn(IMyEntity victim)
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

        private void GetSortedThreats(IMyEntity shooter, ICollection<MyTuple<IMyEntity, float>> collection)
        {
            var grid = shooter.GetTopMostParent() as MyCubeGrid;
            GridAi gridAi;
            if (grid != null && _session.GridTargetingAIs.TryGetValue(grid, out gridAi))
            {
                for (int i = 0; i < gridAi.SortedTargets.Count; i++)
                {
                    var targetInfo = gridAi.SortedTargets[i];
                    collection.Add(new MyTuple<IMyEntity, float>(targetInfo.Target, targetInfo.OffenseRating));
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
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
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
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
                GridAi.AcquireTarget(comp.Platform.Weapons[weaponId], false, (MyEntity)target);
        }

        private static void FireWeaponOnce(IMyTerminalBlock weaponBlock, bool allWeapons = true, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
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

        private static void ToggleWeaponFire(IMyTerminalBlock weaponBlock, bool on, bool allWeapons = true, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
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

        private static bool IsWeaponReadyToFire(IMyTerminalBlock weaponBlock, int weaponId = 0, bool anyWeaponReady = true, bool shotReady = false)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId && comp.State.Value.Online && comp.Set.Value.Overrides.Activate)
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

        private static float GetMaxWeaponRange(IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
                return (float)comp.Platform.Weapons[weaponId].MaxTargetDistance;

            return 0f;
        }

        private static bool GetTurretTargetTypes(IMyTerminalBlock weaponBlock, ICollection<string> collection, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                var threats = weapon.System.Values.Targeting.Threats;
                for (int i = 0; i < threats.Length; i++) collection.Add(threats[i].ToString());
                return true;
            }
            return false;
        }

        private static void SetTurretTargetTypes(IMyTerminalBlock weaponBlock, ICollection<string> collection, int weaponId = 0)
        {

        }

        private static void SetBlockTrackingRange(IMyTerminalBlock weaponBlock, float range)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                double maxTargetDistance = 0;
                foreach (var w in comp.Platform.Weapons)
                    if (w.MaxTargetDistance > maxTargetDistance) 
                        maxTargetDistance = w.MaxTargetDistance;
                
                comp.Set.Value.Range = (float) (range > maxTargetDistance ? maxTargetDistance : range);
            }
        }

        private static bool IsTargetAligned(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
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
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];
                var topMost = targetEnt.GetTopMostParent();
                var targetVel = topMost.Physics?.LinearVelocity ?? Vector3.Zero;
                var targetAccel = topMost.Physics?.AngularAcceleration ?? Vector3.Zero;
                Vector3D predictedPos;
                return Weapon.CanShootTargetObb(w, (MyEntity)targetEnt, targetVel, targetAccel, out predictedPos);
            }
            return false;
        }

        private static Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
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
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.MaxHeat > 0)
            {
                return comp.State.Value.Heat / comp.MaxHeat;
            }
            return 0f;
        }

        private static float GetCurrentPower(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                return comp.SinkPower;

            return 0f;
        }

        private float GetMaxPower(MyDefinitionId weaponDef)
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

        private float GetOptimalDps(IMyEntity entity)
        {
            var terminalBlock = entity as IMyTerminalBlock;
            if (terminalBlock != null)
            {
                WeaponComponent comp;
                if (terminalBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                    return comp.PeakDps;
            }
            else
            {
                var grid = entity.GetTopMostParent() as MyCubeGrid;
                GridAi gridAi;
                if (grid != null && _session.GridTargetingAIs.TryGetValue(grid, out gridAi))
                    return gridAi.OptimalDps;
            }
            return 0f;
        }

        private static string GetActiveAmmo(IMyTerminalBlock weaponBlock, int weaponId)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
                return comp.Platform.Weapons[weaponId].ActiveAmmoDef.AmmoDef.AmmoRound;

            return null;
        }

        private static void SetActiveAmmo(IMyTerminalBlock weaponBlock, int weaponId, string ammoTypeStr)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];
                for (int i = 0; i < w.System.WeaponAmmoTypes.Length; i++)
                {
                    var ammoType = w.System.WeaponAmmoTypes[i];
                    if (ammoType.AmmoName == ammoTypeStr && ammoType.AmmoDef.Const.IsTurretSelectable)
                    {
                        w.Set.AmmoTypeId = i;
                        if (comp.Session.MpActive)
                            comp.Session.SendCycleAmmoNetworkUpdate(w, i);

                        break;
                    }
                }
            }
        }

        private void RegisterProjectileAddedCallback(Action<Vector3, float> callback)
        {
            _session.ProjectileAddedCallback += callback;
        }

        private void UnRegisterProjectileAddedCallback(Action<Vector3, float> callback)
        {
            try
            {
                _session.ProjectileAddedCallback += callback;
            }
            catch (Exception e)
            {
                Log.Line($"Cannot remove Action, Action is not registered: {e}");
            }
        }

        private float GetConstructEffectiveDps(IMyEntity entity)
        {
            var grid = entity.GetTopMostParent() as MyCubeGrid;
            GridAi gridAi;
            if (grid != null && _session.GridTargetingAIs.TryGetValue(grid, out gridAi))
                return gridAi.EffectiveDps;

            return 0;
        }
    }
}
