using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRageMath;

namespace WeaponCore.Api
{
    /// <summary>
    /// https://github.com/sstixrud/WeaponCore/blob/master/Data/Scripts/WeaponCore/Api/WeaponCorePbApi.cs
    /// </summary>
    public class WcPbApi
    {
        private Action<ICollection<MyDefinitionId>> _getCoreWeapons;
        private Action<ICollection<MyDefinitionId>> _getCoreStaticLaunchers;
        private Action<ICollection<MyDefinitionId>> _getCoreTurrets;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, IDictionary<string, int>, bool> _getBlockWeaponMap;
        private Func<VRage.Game.ModAPI.Ingame.IMyEntity, MyTuple<bool, int, int>> _getProjectilesLockedOn;
        private Action<VRage.Game.ModAPI.Ingame.IMyEntity, ICollection<MyTuple<VRage.Game.ModAPI.Ingame.IMyEntity, float>>> _getSortedThreats;
        private Func<VRage.Game.ModAPI.Ingame.IMyEntity, int, VRage.Game.ModAPI.Ingame.IMyEntity> _getAiFocus;
        private Func<VRage.Game.ModAPI.Ingame.IMyEntity, VRage.Game.ModAPI.Ingame.IMyEntity, int, bool> _setAiFocus;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, MyTuple<bool, bool, bool, VRage.Game.ModAPI.Ingame.IMyEntity>> _getWeaponTarget;
        private Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, VRage.Game.ModAPI.Ingame.IMyEntity, int> _setWeaponTarget;
        private Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool, int> _fireWeaponOnce;
        private Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool, bool, int> _toggleWeaponFire;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, float> _getMaxWeaponRange;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, ICollection<string>, int, bool> _getTurretTargetTypes;
        private Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, ICollection<string>, int> _setTurretTargetTypes;
        private Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float> _setBlockTrackingRange;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, VRage.Game.ModAPI.Ingame.IMyEntity, int, bool> _isTargetAligned;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, VRage.Game.ModAPI.Ingame.IMyEntity, int, bool> _canShootTarget;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, VRage.Game.ModAPI.Ingame.IMyEntity, int, Vector3D?> _getPredictedTargetPos;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float> _getHeatLevel;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float> _currentPowerConsumption;
        private Func<MyDefinitionId, float> _getMaxPower;
        private Func<VRage.Game.ModAPI.Ingame.IMyEntity, bool> _hasGridAi;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool> _hasCoreWeapon;
        private Func<VRage.Game.ModAPI.Ingame.IMyEntity, float> _getOptimalDps;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, string> _getActiveAmmo;
        private Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, string> _setActiveAmmo;
        private Action<Action<Vector3, float>> _registerProjectileAdded;
        private Action<Action<Vector3, float>> _unRegisterProjectileAdded;
        private Func<VRage.Game.ModAPI.Ingame.IMyEntity, float> _getConstructEffectiveDps;

        private bool Activate(Sandbox.ModAPI.Ingame.IMyTerminalBlock pbBlock)
        {
            var dict = pbBlock.GetProperty("WcPbAPI")?.As<Dictionary<string, Delegate>>().GetValue(pbBlock);
            if (dict == null) throw new Exception($"WcPbAPI failed to activate");
            return ApiAssign(dict);
        }

        public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            if (delegates == null)
                return false;

            AssignMethod(delegates, "GetCoreWeapons", ref _getCoreWeapons);
            AssignMethod(delegates, "GetCoreStaticLaunchers", ref _getCoreStaticLaunchers);
            AssignMethod(delegates, "GetCoreTurrets", ref _getCoreTurrets);
            AssignMethod(delegates, "GetBlockWeaponMap", ref _getBlockWeaponMap);
            AssignMethod(delegates, "GetProjectilesLockedOn", ref _getProjectilesLockedOn);
            AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
            AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
            AssignMethod(delegates, "SetAiFocus", ref _setAiFocus);
            AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
            AssignMethod(delegates, "SetWeaponTarget", ref _setWeaponTarget);
            AssignMethod(delegates, "FireWeaponOnce", ref _fireWeaponOnce);
            AssignMethod(delegates, "ToggleWeaponFire", ref _toggleWeaponFire);
            AssignMethod(delegates, "IsWeaponReadyToFire", ref _isWeaponReadyToFire);
            AssignMethod(delegates, "GetMaxWeaponRange", ref _getMaxWeaponRange);
            AssignMethod(delegates, "GetTurretTargetTypes", ref _getTurretTargetTypes);
            AssignMethod(delegates, "SetTurretTargetTypes", ref _setTurretTargetTypes);
            AssignMethod(delegates, "SetBlockTrackingRange", ref _setBlockTrackingRange);
            AssignMethod(delegates, "IsTargetAligned", ref _isTargetAligned);
            AssignMethod(delegates, "CanShootTarget", ref _canShootTarget);
            AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);
            AssignMethod(delegates, "GetHeatLevel", ref _getHeatLevel);
            AssignMethod(delegates, "GetCurrentPower", ref _currentPowerConsumption);
            AssignMethod(delegates, "GetMaxPower", ref _getMaxPower);
            AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
            AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
            AssignMethod(delegates, "GetOptimalDps", ref _getOptimalDps);
            AssignMethod(delegates, "GetActiveAmmo", ref _getActiveAmmo);
            AssignMethod(delegates, "SetActiveAmmo", ref _setActiveAmmo);
            AssignMethod(delegates, "RegisterProjectileAdded", ref _registerProjectileAdded);
            AssignMethod(delegates, "UnRegisterProjectileAdded", ref _unRegisterProjectileAdded);
            AssignMethod(delegates, "GetConstructEffectiveDps", ref _getConstructEffectiveDps);
            
            return true;
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if (delegates == null) {
                field = null;
                return;
            }

            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");

            field = del as T;
            if (field == null)
                throw new Exception(
                    $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }

        public void GetAllCoreWeapons(ICollection<MyDefinitionId> collection) => _getCoreWeapons?.Invoke(collection);

        public void GetAllCoreStaticLaunchers(ICollection<MyDefinitionId> collection) =>
            _getCoreStaticLaunchers?.Invoke(collection);

        public void GetAllCoreTurrets(ICollection<MyDefinitionId> collection) => _getCoreTurrets?.Invoke(collection);

        public bool GetBlockWeaponMap(Sandbox.ModAPI.Ingame.IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) =>
            _getBlockWeaponMap?.Invoke(weaponBlock, collection) ?? false;

        public MyTuple<bool, int, int> GetProjectilesLockedOn(VRage.Game.ModAPI.Ingame.IMyEntity victim) =>
            _getProjectilesLockedOn?.Invoke(victim) ?? new MyTuple<bool, int, int>();

        public void GetSortedThreats(VRage.Game.ModAPI.Ingame.IMyEntity shooter, ICollection<MyTuple<VRage.Game.ModAPI.Ingame.IMyEntity, float>> collection) =>
            _getSortedThreats?.Invoke(shooter, collection);

        public VRage.Game.ModAPI.Ingame.IMyEntity GetAiFocus(VRage.Game.ModAPI.Ingame.IMyEntity shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);

        public bool SetAiFocus(VRage.Game.ModAPI.Ingame.IMyEntity shooter, VRage.Game.ModAPI.Ingame.IMyEntity target, int priority = 0) =>
            _setAiFocus?.Invoke(shooter, target, priority) ?? false;

        public MyTuple<bool, bool, bool, VRage.Game.ModAPI.Ingame.IMyEntity> GetWeaponTarget(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId = 0) =>
            _getWeaponTarget?.Invoke(weapon, weaponId) ?? new MyTuple<bool, bool, bool, VRage.Game.ModAPI.Ingame.IMyEntity>();

        public void SetWeaponTarget(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, VRage.Game.ModAPI.Ingame.IMyEntity target, int weaponId = 0) =>
            _setWeaponTarget?.Invoke(weapon, target, weaponId);

        public void FireWeaponOnce(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) =>
            _fireWeaponOnce?.Invoke(weapon, allWeapons, weaponId);

        public void ToggleWeaponFire(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, bool on, bool allWeapons, int weaponId = 0) =>
            _toggleWeaponFire?.Invoke(weapon, on, allWeapons, weaponId);

        public bool IsWeaponReadyToFire(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true,
            bool shootReady = false) =>
            _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;

        public float GetMaxWeaponRange(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId) =>
            _getMaxWeaponRange?.Invoke(weapon, weaponId) ?? 0f;

        public bool GetTurretTargetTypes(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, IList<string> collection, int weaponId = 0) =>
            _getTurretTargetTypes?.Invoke(weapon, collection, weaponId) ?? false;

        public void SetTurretTargetTypes(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, IList<string> collection, int weaponId = 0) =>
            _setTurretTargetTypes?.Invoke(weapon, collection, weaponId);

        public void SetBlockTrackingRange(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, float range) =>
            _setBlockTrackingRange?.Invoke(weapon, range);

        public bool IsTargetAligned(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, VRage.Game.ModAPI.Ingame.IMyEntity targetEnt, int weaponId) =>
            _isTargetAligned?.Invoke(weapon, targetEnt, weaponId) ?? false;

        public bool CanShootTarget(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, VRage.Game.ModAPI.Ingame.IMyEntity targetEnt, int weaponId) =>
            _canShootTarget?.Invoke(weapon, targetEnt, weaponId) ?? false;

        public Vector3D? GetPredictedTargetPosition(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, VRage.Game.ModAPI.Ingame.IMyEntity targetEnt, int weaponId) =>
            _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;

        public float GetHeatLevel(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;
        public float GetCurrentPower(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon) => _currentPowerConsumption?.Invoke(weapon) ?? 0f;
        public float GetMaxPower(MyDefinitionId weaponDef) => _getMaxPower?.Invoke(weaponDef) ?? 0f;
        public bool HasGridAi(VRage.Game.ModAPI.Ingame.IMyEntity entity) => _hasGridAi?.Invoke(entity) ?? false;
        public bool HasCoreWeapon(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
        public float GetOptimalDps(VRage.Game.ModAPI.Ingame.IMyEntity entity) => _getOptimalDps?.Invoke(entity) ?? 0f;

        public string GetActiveAmmo(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId) =>
            _getActiveAmmo?.Invoke(weapon, weaponId) ?? null;

        public void SetActiveAmmo(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId, string ammoType) =>
            _setActiveAmmo?.Invoke(weapon, weaponId, ammoType);

        public void RegisterProjectileAddedCallback(Action<Vector3, float> action) =>
            _registerProjectileAdded?.Invoke(action);

        public void UnRegisterProjectileAddedCallback(Action<Vector3, float> action) =>
            _unRegisterProjectileAdded?.Invoke(action);

        public float GetConstructEffectiveDps(VRage.Game.ModAPI.Ingame.IMyEntity entity) => _getConstructEffectiveDps?.Invoke(entity) ?? 0f;

    }
}