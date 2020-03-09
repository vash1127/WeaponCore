using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;

namespace WeaponCore.Support
{
    internal class WeaponCoreApi
    {
        private bool _apiInit;

        private Action<IList<byte[]>> _getAllWeaponDefinitions;
        private Action<IList<MyDefinitionId>> _getCoreWeapons;
        private Action<IList<MyDefinitionId>> _getCoreStaticLaunchers;
        private Action<IList<MyDefinitionId>> _getCoreTurrets;
        private Func<IMyTerminalBlock, IDictionary<string, int>, bool> _getBlockWeaponMap;
        private Func<IMyEntity, MyTuple<bool, int, int>> _getProjectilesLockedOn;
        private Action<IMyEntity, IDictionary<IMyEntity, float>> _getSortedThreats;
        private Func<IMyEntity, int, IMyEntity> _getAiFocus;
        private Func<IMyEntity, IMyEntity, int, bool> _setAiFocus;
        private Func<IMyTerminalBlock, int, MyTuple<bool, bool, bool, IMyEntity>> _getWeaponTarget;
        private Action<IMyTerminalBlock, IMyEntity, int> _setWeaponTarget;
        private Action<IMyTerminalBlock, bool, int> _fireWeaponOnce;
        private Action<IMyTerminalBlock, bool, bool, int> _toggleWeaponFire;
        private Func<IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
        private Func<IMyTerminalBlock, int, float> _getMaxWeaponRange;
        private Func<IMyTerminalBlock, IList<string>, int, bool> _getTurretTargetTypes;
        private Action<IMyTerminalBlock, IList<string>, int> _setTurretTargetTypes;
        private Action<IMyTerminalBlock, float> _setBlockTrackingRange;
        private Func<IMyTerminalBlock, IMyEntity, int, bool> _isTargetAligned;
        private Func<IMyTerminalBlock, IMyEntity, int, bool> _canShootTarget;
        private Func<IMyTerminalBlock, IMyEntity, int, Vector3D?> _getPredictedTargetPos;
        private Func<IMyTerminalBlock, float> _getHeatLevel;
        private Func<IMyTerminalBlock, float> _currentPowerConsumption;
        private Func<MyDefinitionId, float> _getMaxPower;
        private Action<IMyTerminalBlock> _disableRequiredPower;
        private Func<IMyEntity, bool> _hasGridAi;
        private Func<IMyTerminalBlock, bool> _hasCoreWeapon;

        private const long Channel = 67549756549;
        private readonly List<byte[]> _byteArrays = new List<byte[]>();

        public bool IsReady { get; private set; }
        public readonly List<WeaponDefinition> WeaponDefinitions = new List<WeaponDefinition>();

        private void HandleMessage(object o)
        {
            if (_apiInit) return;
            var dict = o as IReadOnlyDictionary<string, Delegate>;
            if (dict == null)
                return;
            ApiLoad(dict);
            IsReady = true;
        }

        private bool _isRegistered;

        public bool Load()
        {
            if (!_isRegistered)
            {
                _isRegistered = true;
                MyAPIGateway.Utilities.RegisterMessageHandler(Channel, HandleMessage);
            }
            if (!IsReady)
                MyAPIGateway.Utilities.SendModMessage(Channel, "ApiEndpointRequest");
            return IsReady;
        }

        public void Unload()
        {
            if (_isRegistered)
            {
                _isRegistered = false;
                MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, HandleMessage);
            }
            IsReady = false;
        }

        public void ApiLoad(IReadOnlyDictionary<string, Delegate> delegates, bool getWeaponDefinitions = false)
        {
            _apiInit = true;
            _getCoreWeapons = (Action<IList<MyDefinitionId>>)delegates["GetCoreWeapons"];
            _getCoreStaticLaunchers = (Action<IList<MyDefinitionId>>)delegates["GetCoreStaticLaunchers"];
            _getCoreTurrets = (Action<IList<MyDefinitionId>>)delegates["GetCoreTurrets"];
            _getBlockWeaponMap = (Func<IMyTerminalBlock, IDictionary<string, int>, bool>)delegates["GetBlockWeaponMap"];
            _getProjectilesLockedOn = (Func<IMyEntity, MyTuple<bool, int, int>>)delegates["GetProjectilesLockedOn"];
            _getSortedThreats = (Action< IMyEntity, IDictionary<IMyEntity, float>>)delegates["GetSortedThreats"];
            _getAiFocus = (Func<IMyEntity, int, IMyEntity>)delegates["GetAiFocus"];
            _setAiFocus = (Func<IMyEntity, IMyEntity, int, bool>)delegates["SetAiFocus"];
            _getWeaponTarget = (Func <IMyTerminalBlock, int, MyTuple<bool, bool, bool, IMyEntity>>)delegates["GetWeaponTarget"];
            _setWeaponTarget = (Action<IMyTerminalBlock, IMyEntity, int>)delegates["SetWeaponTarget"];
            _fireWeaponOnce = (Action<IMyTerminalBlock, bool, int>)delegates["FireWeaponOnce"];
            _toggleWeaponFire = (Action<IMyTerminalBlock, bool, bool, int>)delegates["ToggleWeaponFire"];
            _isWeaponReadyToFire = (Func<IMyTerminalBlock, int, bool, bool, bool>)delegates["IsWeaponReadyToFire"];
            _getMaxWeaponRange = (Func<IMyTerminalBlock, int, float>)delegates["GetMaxWeaponRange"];
            _getTurretTargetTypes = (Func<IMyTerminalBlock, IList<string>, int, bool>)delegates["GetTurretTargetTypes"];
            _setTurretTargetTypes = (Action<IMyTerminalBlock, IList<string>, int>)delegates["SetTurretTargetTypes"];
            _setBlockTrackingRange = (Action <IMyTerminalBlock, float>)delegates["SetBlockTrackingRange"];
            _isTargetAligned = (Func<IMyTerminalBlock, IMyEntity, int, bool>)delegates["IsTargetAligned"];
            _canShootTarget = (Func<IMyTerminalBlock, IMyEntity, int, bool>)delegates["CanShootTarget"];
            _getPredictedTargetPos = (Func<IMyTerminalBlock, IMyEntity, int, Vector3D?>)delegates["GetPredictedTargetPosition"];
            _getHeatLevel = (Func<IMyTerminalBlock, float>)delegates["GetHeatLevel"];
            _currentPowerConsumption = (Func<IMyTerminalBlock, float>)delegates["GetCurrentPower"];
            _getMaxPower = (Func<MyDefinitionId, float>)delegates["GetMaxPower"];
            _disableRequiredPower = (Action<IMyTerminalBlock>)delegates["DisableRequiredPower"];
            _hasGridAi = (Func<IMyEntity, bool>)delegates["HasGridAi"];
            _hasCoreWeapon = (Func<IMyEntity, bool>)delegates["HasCoreWeapon"];
            _getAllWeaponDefinitions = (Action<IList<byte[]>>)delegates["GetAllWeaponDefinitions"];
            if (getWeaponDefinitions)
            {
                GetAllWeaponDefinitions(_byteArrays);
                foreach (var byteArray in _byteArrays)
                {
                    WeaponDefinitions.Add(MyAPIGateway.Utilities.SerializeFromBinary<WeaponDefinition>(byteArray));
                }
            }
        }
        public void GetAllWeaponDefinitions(IList<byte[]> collection) => _getAllWeaponDefinitions?.Invoke(collection);
        public void GetAllCoreWeapons(IList<MyDefinitionId> collection) => _getCoreWeapons?.Invoke(collection);
        public void GetAllCoreStaticLaunchers(IList<MyDefinitionId> collection) => _getCoreStaticLaunchers?.Invoke(collection);
        public void GetAllCoreTurrets(IList<MyDefinitionId> collection) => _getCoreTurrets?.Invoke(collection);
        public bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) => _getBlockWeaponMap?.Invoke(weaponBlock, collection) ?? false;
        public MyTuple<bool, int, int> GetProjectilesLockedOn(IMyEntity victim) => _getProjectilesLockedOn?.Invoke(victim) ?? new MyTuple<bool, int, int>();
        public void GetSortedThreats(IMyEntity shooter, IDictionary<IMyEntity, float> collection) => _getSortedThreats?.Invoke(shooter, collection);
        public IMyEntity GetAiFocus(IMyEntity shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);
        public bool SetAiFocus(IMyEntity shooter, IMyEntity target, int priority = 0) => _setAiFocus?.Invoke(shooter, target, priority) ?? false;
        public MyTuple<bool, bool, bool, IMyEntity> GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) => _getWeaponTarget?.Invoke(weapon, weaponId) ?? new MyTuple<bool, bool, bool, IMyEntity>();
        public void SetWeaponTarget(IMyTerminalBlock weapon, IMyEntity target, int weaponId = 0) => _setWeaponTarget?.Invoke(weapon, target, weaponId);
        public void FireWeaponOnce(IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) => _fireWeaponOnce?.Invoke(weapon, allWeapons, weaponId);
        public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on, bool allWeapons, int weaponId = 0) => _toggleWeaponFire?.Invoke(weapon, on, allWeapons, weaponId);
        public bool IsWeaponReadyToFire(IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true, bool shootReady = false) => _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;
        public float GetMaxWeaponRange(IMyTerminalBlock weapon, int weaponId) => _getMaxWeaponRange?.Invoke(weapon, weaponId) ?? 0f;
        public bool GetTurretTargetTypes(IMyTerminalBlock weapon, IList<string> collection, int weaponId = 0) => _getTurretTargetTypes?.Invoke(weapon, collection, weaponId) ?? false;
        public void SetTurretTargetTypes(IMyTerminalBlock weapon, IList<string> collection, int weaponId = 0) => _setTurretTargetTypes?.Invoke(weapon, collection, weaponId);
        public void SetBlockTrackingRange(IMyTerminalBlock weapon, float range) => _setBlockTrackingRange?.Invoke(weapon, range);
        public bool IsTargetAligned(IMyTerminalBlock weapon, IMyEntity targetEnt, int weaponId) => _isTargetAligned?.Invoke(weapon, targetEnt, weaponId) ?? false;
        public bool CanShootTarget(IMyTerminalBlock weapon, IMyEntity targetEnt, int weaponId) => _canShootTarget?.Invoke(weapon, targetEnt, weaponId) ?? false;
        public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, IMyEntity targetEnt, int weaponId) => _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;
        public float GetHeatLevel(IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;
        public float GetCurrentPower(IMyTerminalBlock weapon) => _currentPowerConsumption?.Invoke(weapon) ?? 0f;
        public float GetMaxPower(MyDefinitionId weaponDef) => _getMaxPower?.Invoke(weaponDef) ?? 0f;
        public void DisableRequiredPower(IMyTerminalBlock weapon) => _disableRequiredPower?.Invoke(weapon);
        public bool HasGridAi(IMyTerminalBlock weapon) => _hasGridAi?.Invoke(weapon) ?? false;
        public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;

    }
}
