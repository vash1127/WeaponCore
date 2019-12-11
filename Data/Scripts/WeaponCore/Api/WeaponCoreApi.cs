using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace WeaponCore.Support
{
    internal class WeaponCoreApi
    {
        private bool _apiInit;
        private Func<List<MyDefinitionId>> _getAllCoreStaticLaunchers;
        private Func<List<MyDefinitionId>> _getAllCoreTurrets;
        private Action<VRage.Game.ModAPI.Ingame.IMyEntity, VRage.Game.ModAPI.Ingame.IMyEntity> _setTargetEntity;
        private Action<IMyTerminalBlock> _fireWeaponOnce;
        private Action<IMyTerminalBlock, bool> _toggleWeaponFire;
        private Func<IMyTerminalBlock, bool> _isWeaponReadyToFire;
        private Func<IMyTerminalBlock, float> _getMaxWeaponRange;
        private Func<IMyTerminalBlock, List<List<Threat>>> _getTurretTargetTypes;
        private Action<IMyTerminalBlock, List<List<Threat>>> _setTurretTargetTypes;
        private Action<IMyTerminalBlock, float> _setTurretTargetingRange;
        private Func<IMyTerminalBlock, VRage.Game.ModAPI.Ingame.IMyEntity> _getTargetedEntity;
        private Func<IMyTerminalBlock, Vector3D> _getTargetLeadPredicitionCoords;
        private Func<IMyTerminalBlock, float> _getHeatLevel;
        private Func<IMyTerminalBlock, float> _currentPowerConsumption;
        private Func<MyDefinitionId, float> _maxPowerConsumption;
        private Action<IMyTerminalBlock> _disablePowerRequirements;

        private const long Channel = 67549756549;

        public enum Threat
        {
            Projectiles,
            Characters,
            Grids,
            Neutrals,
            Meteors,
            Other
        }

        public bool IsReady { get; private set; }

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

        public void ApiLoad(IReadOnlyDictionary<string, Delegate> delegates)
        {
            _apiInit = true;
            _getAllCoreStaticLaunchers = (Func<List<MyDefinitionId>>)delegates["GetCoreStaticLaunchers"];
            _getAllCoreTurrets = (Func<List<MyDefinitionId>>)delegates["GetCoreTurrets"];
            _setTargetEntity = (Action<VRage.Game.ModAPI.Ingame.IMyEntity, VRage.Game.ModAPI.Ingame.IMyEntity>)delegates["SetTargetEntity"];
            _fireWeaponOnce = (Action<IMyTerminalBlock>)delegates["FireOnce"];
            _toggleWeaponFire = (Action<IMyTerminalBlock, bool>)delegates["ToggleFire"];
            _isWeaponReadyToFire = (Func<IMyTerminalBlock, bool>)delegates["WeaponReady"];
            _getMaxWeaponRange = (Func<IMyTerminalBlock, float>)delegates["GetMaxRange"];
            _getTurretTargetTypes = (Func<IMyTerminalBlock, List<List<Threat>>>)delegates["GetTurretTargetTypes"];
            _setTurretTargetingRange = (Action <IMyTerminalBlock, float>)delegates["SetTurretRange"];
            _setTurretTargetTypes = (Action<IMyTerminalBlock, List<List<Threat>>>)delegates["SetTurretTargetTypes"];
            _getTargetedEntity = (Func<IMyTerminalBlock, VRage.Game.ModAPI.Ingame.IMyEntity>)delegates["GetTargetedEntity"];
            _getTargetLeadPredicitionCoords = (Func<IMyTerminalBlock, Vector3D>)delegates["TargetPredictedPosition"];
            _getHeatLevel = (Func<IMyTerminalBlock, float>)delegates["GetHeatLevel"];
            _currentPowerConsumption = (Func<IMyTerminalBlock, float>)delegates["CurrentPower"];
            _maxPowerConsumption = (Func<MyDefinitionId, float>)delegates["MaxPower"];
            _disablePowerRequirements = (Action<IMyTerminalBlock>)delegates["DisableRequiredPower"];
        }

        public List<MyDefinitionId> GetAllCoreStaticLaunchers() => _getAllCoreStaticLaunchers?.Invoke();
        public List<MyDefinitionId> GetAllCoreTurrets() => _getAllCoreTurrets?.Invoke();
        public List<List<Threat>> GetTurretTargetTypes(IMyTerminalBlock weapon) => _getTurretTargetTypes?.Invoke(weapon);
        public VRage.Game.ModAPI.Ingame.IMyEntity GetTargetedEntity(IMyTerminalBlock weapon) => _getTargetedEntity?.Invoke(weapon);
        public bool? IsWeaponReadyToFire(IMyTerminalBlock weapon) => _isWeaponReadyToFire?.Invoke(weapon);
        public float? GetMaxWeaponRange(IMyTerminalBlock weapon) => _getMaxWeaponRange?.Invoke(weapon);
        public float? GetHeatLevel(IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon);
        public float? CurrentPowerConsumption(IMyTerminalBlock weapon) => _currentPowerConsumption?.Invoke(weapon);
        public float? MaxPowerConsumption(MyDefinitionId weaponDef) => _maxPowerConsumption?.Invoke(weaponDef);
        public Vector3D? GetTargetLeadPredicitionCoords(IMyTerminalBlock weapon) => _getTargetLeadPredicitionCoords?.Invoke(weapon);

        public void DisablePowerRequirements(IMyTerminalBlock weapon) => _disablePowerRequirements?.Invoke(weapon);
        public void SetTurretTargetingRange(IMyTerminalBlock weapon, float range) => _setTurretTargetingRange?.Invoke(weapon, range);
        public void SetTargetEntity(VRage.Game.ModAPI.Ingame.IMyEntity shooter, VRage.Game.ModAPI.Ingame.IMyEntity target) => _setTargetEntity?.Invoke(shooter, target);
        public void FireWeaponOnce(IMyTerminalBlock weapon) => _fireWeaponOnce?.Invoke(weapon);
        public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on) => _toggleWeaponFire?.Invoke(weapon, on);
        public void SetTurretTargetTypes(IMyTerminalBlock weapon, List<List<Threat>> threats) => _setTurretTargetTypes?.Invoke(weapon, threats);
    }
}
