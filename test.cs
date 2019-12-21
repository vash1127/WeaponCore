using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace WeaponCore
{
    class test
    {

        #region This goes in the programmable block
        const string VERSION = "116.2.0"; const string DATE = "11/29/2019";

        #region Variables that you should not touch
        readonly TurretVariables defaultTurretVariables = new TurretVariables() { ToleranceAngle = 5, ConvergenceRange = 800, EquilibriumRotationSpeed = 10, ProportionalGain = 75, IntegralGain = 0, IntegralDecayRatio = 0.25, DerivativeGain = 0, GameMaxSpeed = 100, TargetRefreshInterval = 2, RotorTurretGroupNameTag = "Turret Group", RotorGimbalGroupNameTag = "Gimbal Group", AiTurretGroupNameTag = "Slaved Group", ElevationRotorNameTag = "Elevation", AzimuthRotorNameTag = "Azimuth", DesignatorNameTag = "Designator", OnlyShootWhenDesignatorShoots = false, }; class TurretVariables { public double ToleranceAngle; public double ConvergenceRange; public double EquilibriumRotationSpeed; public double ProportionalGain; public double IntegralGain; public double DerivativeGain; public double IntegralDecayRatio; public double GameMaxSpeed; public double TargetRefreshInterval; public string RotorTurretGroupNameTag; public string RotorGimbalGroupNameTag; public string AiTurretGroupNameTag; public string ElevationRotorNameTag; public string AzimuthRotorNameTag; public string DesignatorNameTag; public bool OnlyShootWhenDesignatorShoots; }
        const string IgcTag = "IGC_IFF_MSG"; const string IniGeneralSection = "Turret Slaver - General Parameters"; const string IniMuzzleVelocity = "- Muzzle velocity (m/s)"; const string IniIsRocket = "- Is rocket"; const string IniRocketAccel = "- Rocket acceleration (m/s^2)"; const string IniRocketInitVel = "- Rocket initial velocity (m/s)"; const string IniAvoidFriendly = "- Avoid friendly fire"; const string IniTolerance = "- Fire tolerance angle (deg)"; const string IniConvergence = "- Manual convergence range (m)"; const string IniKp = "- Proportional constant"; const string IniKi = "- Integral constant"; const string IniKd = "- Derivative constant"; const string IniIntegralDecayRatio = "- Integral decay ratio"; const string IniRestRpm = "- Return to rest position speed (rpm)"; const string IniGameMaxSpeed = "- Game max speed (m/s)"; const string IniRotorTurretName = "- Rotor turret group name tag"; const string IniAiTurretName = "- Slaved AI turret group name tag"; const string IniAzimuthName = "- Azimuth rotor name tag (optional)"; const string IniElevationName = "- Elevation rotor name tag (optional)"; const string IniDesignatorName = "- Designator name tag"; const string IniEngagementRange = "- Autofire range (m)"; const string IniRestTime = "- Return to rest delay (s)"; const string IniShootWhenDesignatorDoes = "- Only shoot when designator shoots"; const string IniTimerRetriggerInterval = "- Timer trigger interval (s)"; const string IniGravityMult = "- Gravity multiplier (for mods)"; const string _iniMigrationKey = "[General Parameters]"; Dictionary<string, string> _iniMigrationDictionary = new Dictionary<string, string>() { { "General Parameters", IniGeneralSection }, { "muzzle_velocity", IniMuzzleVelocity }, { "is_rocket", IniIsRocket }, { "rocket_acceleration", IniRocketAccel }, { "rocket_initial_velocity", IniRocketInitVel }, { "avoid_friendly_fire", IniAvoidFriendly }, { "fire_tolerance_deg", IniTolerance }, { "manual_convergence_range", IniConvergence }, { "proportional_gain", IniKp }, { "integral_gain", IniKi }, { "derivative_gain", IniKd }, { "integral_decay_ratio", IniIntegralDecayRatio }, { "return_to_rest_rpm", IniRestRpm }, { "max_game_speed", IniGameMaxSpeed }, { "rotor_turret_group_tag", IniRotorTurretName }, { "ai_turret_group_tag", IniAiTurretName }, { "azimuth_rotor_name_tag", IniAzimuthName }, { "elevation_rotor_name_tag", IniElevationName }, { "designator_name_tag", IniDesignatorName }, { "auto_fire_range", IniEngagementRange }, { "return_to_rest_delay", IniRestTime }, { "only_shoot_when_designator_shoots", IniShootWhenDesignatorDoes }, { "gravity_multiplier", IniGravityMult } }; const double UpdatesPerSecond = 10; const double MainUpdateInterval = 1.0 / UpdatesPerSecond; const double Tick = 1.0 / 60.0; const int MaxBlocksToCheckForFF = 50; Dictionary<long, FriendlyData> _friendlyData = new Dictionary<long, FriendlyData>(); Dictionary<long, FriendlyData> _friendlyDataBuffer = new Dictionary<long, FriendlyData>(); List<IMyLargeTurretBase> _allDesignators = new List<IMyLargeTurretBase>(); List<IMyShipController> _shipControllers = new List<IMyShipController>(); List<IMyTextPanel> _debugPanels = new List<IMyTextPanel>(); List<IMyBlockGroup> _allCurrentGroups = new List<IMyBlockGroup>(); List<IMyBlockGroup> _currentRotorTurretGroups = new List<IMyBlockGroup>(); List<IMyBlockGroup> _currentRotorGimbalGroups = new List<IMyBlockGroup>(); List<IMyBlockGroup> _currentAITurretGroups = new List<IMyBlockGroup>(); List<TurretGroup> _turretList = new List<TurretGroup>(); MyIni _generalIni = new MyIni(); Scheduler _scheduler; ScheduledAction _scheduledMainSetup; StringBuilder _iniOutput = new StringBuilder(); StringBuilder _echoOutput = new StringBuilder(); StringBuilder _turretEchoBuilder = new StringBuilder(); StringBuilder _turretWarningBuilder = new StringBuilder(); StringBuilder _turretErrorBuilder = new StringBuilder(); StringBuilder _turretEchoOutput = new StringBuilder(); StringBuilder _turretWarningOutput = new StringBuilder(); StringBuilder _turretErrorOutput = new StringBuilder(); RuntimeTracker _runtimeTracker; CircularBuffer<Action> _turretBuffer; IMyBroadcastListener _broadcastListener; IMyShipController _reference = null; Vector3D _lastGridPosition = Vector3D.Zero, _gridVelocity = Vector3D.Zero, _gravity = Vector3D.Zero; double _turretRefreshTime; bool _useVelocityEstimation = true, _debugMode = false, _writtenTurretEcho = false; int _rotorTurretCount = 0, _aiTurretCount = 0, _rotorGimbalCount = 0;
        #endregion

        #region Main Routine Methods
        Program()
        {
            MigrateConfig(); Runtime.UpdateFrequency = UpdateFrequency.Update1; _runtimeTracker = new RuntimeTracker(this, 5 * 60); double step = UpdatesPerSecond / 60.0; _turretBuffer = new CircularBuffer<Action>(6); _turretBuffer.Add(() => UpdateTurrets(0 * step, 1 * step)); _turretBuffer.Add(() => UpdateTurrets(1 * step, 2 * step)); _turretBuffer.Add(() => UpdateTurrets(2 * step, 3 * step)); _turretBuffer.Add(() => UpdateTurrets(3 * step, 4 * step)); _turretBuffer.Add(() => UpdateTurrets(4 * step, 5 * step)); _turretBuffer.Add(() => UpdateTurrets(5 * step, 6 * step)); _scheduler = new Scheduler(this); _scheduledMainSetup = new ScheduledAction(MainSetup, 0.1); _scheduler.AddScheduledAction(_scheduledMainSetup); _scheduler.AddScheduledAction(CalculateShooterVelocity, UpdatesPerSecond); _scheduler.AddScheduledAction(PrintDetailedInfo, 1); _scheduler.AddScheduledAction(NetworkTargets, 6);
            //scheduler.AddScheduledAction(RefreshDesignatorTargeting, 1, timeOffset: 0.66);
            _scheduler.AddScheduledAction(MoveNextTurrets, 60); PrintRunning("Whip's Turret Slaver", VERSION, DATE, 1f);
            // IGC Register
            _broadcastListener = IGC.RegisterBroadcastListener(IgcTag); _broadcastListener.SetMessageCallback(IgcTag); MainSetup(); base.Echo("Initializing...");
        }
        void Main(string arg, UpdateType updateType) { _runtimeTracker.AddRuntime(); if ((updateType & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) != 0) ArgumentHandling(arg); if (arg.Equals(IgcTag)) ProcessNetworkMessage(); _scheduler.Update(); _runtimeTracker.AddInstructions(); }
        /*
        * Hiding default echo implementation so that we can display precisely when we want.
        */
        new void Echo(string text) { _echoOutput.AppendLine(text); }
        void PrintEcho() { base.Echo(_echoOutput.ToString()); }
        void PrintDetailedInfo() { Echo($"WMI Turret Control Systems\n(Version {VERSION} - {DATE})"); Echo("\nYou can customize turrets \n    individually in the Custom Data\n    of this block."); Echo($"\nNext block refresh in {Math.Max(0, _scheduledMainSetup.RunInterval - _scheduledMainSetup.TimeSinceLastRun):N0} second(s).\n"); Echo($"Turret Summary:\n> {_rotorTurretCount} rotor turret group(s) found"); Echo($"> {_rotorGimbalCount} rotor gimbal group(s) found"); Echo($"> {_aiTurretCount} slaved AI turret group(s) found\n"); Echo($"Debug mode is: {(_debugMode ? "ON" : "OFF")}\n> Toggle debug output with the\nargument: \"debug_toggle\"."); if (_debugMode) Echo($"> {_debugPanels.Count} debug panel(s) found\n> Name a text panel \"DEBUG\" to \nsee debug text."); _echoOutput.Append(_turretErrorOutput); _echoOutput.Append(_turretWarningOutput); Echo(_runtimeTracker.Write()); if (_debugMode) { string finalOutput = _echoOutput.ToString() + _turretEchoOutput.ToString(); foreach (var block in _debugPanels) { block.WriteText(finalOutput); block.ContentType = ContentType.TEXT_AND_IMAGE; } } PrintEcho(); _echoOutput.Clear(); }
        void MoveNextTurrets() { try { _turretBuffer.MoveNext().Invoke(); } catch (Exception e) { PrintBsod(Me.GetSurface(0), "Whip's Turret Slaver", VERSION, 0.5f, e); throw e; } }
        void UpdateTurrets(double startProportion, double endProportion)
        {
            int startInt = (int)Math.Round(startProportion * _turretList.Count); int endInt = (int)Math.Round(endProportion * _turretList.Count); for (int i = startInt; i < endInt; ++i) { var turretToUpdate = _turretList[i]; 
                turretToUpdate.DoWork(_gridVelocity, _gravity, _allShipGrids, _friendlyData); _turretErrorBuilder.Append(turretToUpdate.ErrorOutput); _turretWarningBuilder.Append(turretToUpdate.WarningOutput); if (_debugMode) _turretEchoBuilder.Append(turretToUpdate.EchoOutput); }
            // End of cycle
            if (endInt == _turretList.Count && !_writtenTurretEcho) { _writtenTurretEcho = true; if (_debugMode) { _turretEchoOutput.Clear(); _turretEchoOutput.Append(_turretEchoBuilder); _turretEchoBuilder.Clear(); } _turretErrorOutput.Clear(); _turretErrorOutput.Append(_turretErrorBuilder); _turretWarningOutput.Clear(); _turretWarningOutput.Append(_turretWarningBuilder); _turretErrorBuilder.Clear(); _turretWarningBuilder.Clear(); } else { _writtenTurretEcho = false; }
        }
        void MainSetup() { ParseGeneralIni(); GetAllGrids(); GetBlockGroups(); GetVelocityReference(); BuildIniOutput(); }
        void CalculateShooterVelocity()
        {
            _gravity = Vector3D.Zero; if (_useVelocityEstimation)
            {
                var currentGridPosition = Me.CubeGrid.WorldAABB.Center; //get grid's bounding box center, decent approximation for CoM
                _gridVelocity = (currentGridPosition - _lastGridPosition) * UpdatesPerSecond; _lastGridPosition = currentGridPosition;
            }
            else { if (DoesBlockExist(_reference)) { _gridVelocity = _reference.GetShipVelocities().LinearVelocity; _gravity = _reference.GetNaturalGravity(); } else { GetVelocityReference(); } }
        }
        void WriteGeneralIni() { _generalIni.Clear(); _generalIni.TryParse(Me.CustomData, IniGeneralSection); _generalIni.Set(IniGeneralSection, IniGameMaxSpeed, defaultTurretVariables.GameMaxSpeed); _generalIni.Set(IniGeneralSection, IniRotorTurretName, defaultTurretVariables.RotorTurretGroupNameTag); _generalIni.Set(IniGeneralSection, IniAiTurretName, defaultTurretVariables.AiTurretGroupNameTag); _generalIni.Set(IniGeneralSection, IniAzimuthName, defaultTurretVariables.AzimuthRotorNameTag); _generalIni.Set(IniGeneralSection, IniElevationName, defaultTurretVariables.ElevationRotorNameTag); _generalIni.Set(IniGeneralSection, IniDesignatorName, defaultTurretVariables.DesignatorNameTag); }
        void ParseGeneralIni() { _generalIni.Clear(); bool parsed = _generalIni.TryParse(Me.CustomData, IniGeneralSection); if (!parsed) return; defaultTurretVariables.GameMaxSpeed = _generalIni.Get(IniGeneralSection, IniGameMaxSpeed).ToDouble(defaultTurretVariables.GameMaxSpeed); defaultTurretVariables.RotorTurretGroupNameTag = _generalIni.Get(IniGeneralSection, IniRotorTurretName).ToString(defaultTurretVariables.RotorTurretGroupNameTag); defaultTurretVariables.AiTurretGroupNameTag = _generalIni.Get(IniGeneralSection, IniAiTurretName).ToString(defaultTurretVariables.AiTurretGroupNameTag); defaultTurretVariables.DesignatorNameTag = _generalIni.Get(IniGeneralSection, IniDesignatorName).ToString(defaultTurretVariables.DesignatorNameTag); defaultTurretVariables.AzimuthRotorNameTag = _generalIni.Get(IniGeneralSection, IniAzimuthName).ToString(defaultTurretVariables.AzimuthRotorNameTag); defaultTurretVariables.ElevationRotorNameTag = _generalIni.Get(IniGeneralSection, IniElevationName).ToString(defaultTurretVariables.ElevationRotorNameTag); }
        void BuildIniOutput() { _iniOutput.Clear(); WriteGeneralIni(); _iniOutput.AppendLine(_generalIni.ToString()); foreach (TurretGroup turret in _turretList) { _iniOutput.Append(turret.IniOutput).Append(Environment.NewLine); } Me.CustomData = _iniOutput.ToString(); }
        bool CollectDesignatorsDebugAndMech(IMyTerminalBlock b) { if (!b.IsSameConstructAs(Me)) return false; double rad = b.CubeGrid.WorldAABB.HalfExtents.LengthSquared(); if (rad > _biggestGridRadius) { _biggestGridRadius = rad; _biggestGrid = b.CubeGrid; } var turret = b as IMyLargeTurretBase; if (turret != null && StringExtensions.Contains(b.CustomName, defaultTurretVariables.DesignatorNameTag)) { _allDesignators.Add(turret); return false; } var sc = b as IMyShipController; if (sc != null) { _shipControllers.Add(sc); return false; } var mech = b as IMyMechanicalConnectionBlock; if (mech != null) { allMechanical.Add(mech); return false; } var text = b as IMyTextPanel; if (_debugMode && b != null && b.CustomName.Contains("DEBUG")) { _debugPanels.Add(text); return false; } return false; }
        bool CollectTurretGroups(IMyBlockGroup g) { if (StringExtensions.Contains(g.Name, defaultTurretVariables.AiTurretGroupNameTag)) { _currentAITurretGroups.Add(g); _allCurrentGroups.Add(g); return false; } else if (StringExtensions.Contains(g.Name, defaultTurretVariables.RotorTurretGroupNameTag)) { _currentRotorTurretGroups.Add(g); _allCurrentGroups.Add(g); return false; } else if (StringExtensions.Contains(g.Name, defaultTurretVariables.RotorGimbalGroupNameTag)) { _currentRotorGimbalGroups.Add(g); _allCurrentGroups.Add(g); } return false; }
        void GetBlockGroups()
        {
            _biggestGridRadius = 0; _biggestGrid = null; _shipControllers.Clear(); _allDesignators.Clear(); allMechanical.Clear(); _debugPanels.Clear(); _currentAITurretGroups.Clear(); _currentRotorTurretGroups.Clear(); _currentRotorGimbalGroups.Clear(); _allCurrentGroups.Clear(); GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectDesignatorsDebugAndMech); GridTerminalSystem.GetBlockGroups(null, CollectTurretGroups); _turretRefreshTime = defaultTurretVariables.TargetRefreshInterval / _allDesignators.Count(); _turretList.RemoveAll(x => !_allCurrentGroups.Contains(x.ThisGroup)); _rotorTurretCount = _currentRotorTurretGroups.Count; _aiTurretCount = _currentAITurretGroups.Count; _rotorGimbalCount = _currentRotorGimbalGroups.Count;
            //Update existing turrets
            foreach (var turret in _turretList)
            {
                turret.GetTurretGroupBlocks(defaultTurretVariables: defaultTurretVariables);
                switch (turret.Type) //Remove existing turrets from list
                { case TurretGroup.TurretType.RotorTurret: _currentRotorTurretGroups.Remove(turret.ThisGroup); break; case TurretGroup.TurretType.RotorGimbal: _currentRotorGimbalGroups.Remove(turret.ThisGroup); break; case TurretGroup.TurretType.SlavedAI: _currentAITurretGroups.Remove(turret.ThisGroup); break; }
            }
            //Add new turret groups to the master list
            foreach (var g in _currentAITurretGroups) { var turret = new TurretGroup(g, defaultTurretVariables, this, TurretGroup.TurretType.SlavedAI, _allShipGrids, _friendlyData); _turretList.Add(turret); }
            foreach (var g in _currentRotorGimbalGroups) { var turret = new TurretGroup(g, defaultTurretVariables, this, TurretGroup.TurretType.RotorGimbal, _allShipGrids, _friendlyData); _turretList.Add(turret); }
            foreach (var g in _currentRotorTurretGroups) { var turret = new TurretGroup(g, defaultTurretVariables, this, TurretGroup.TurretType.RotorTurret, _allShipGrids, _friendlyData); _turretList.Add(turret); }
        }
        void GetVelocityReference() { _reference = _shipControllers.Count > 0 ? _shipControllers[0] : null; _useVelocityEstimation = _reference == null; }
        void RefreshDesignatorTargeting() { foreach (var turret in _allDesignators) { float range = TerminalPropertiesHelper.GetValue<float>(turret, "Range"); TerminalPropertiesHelper.SetValue(turret, "Range", range - 1); TerminalPropertiesHelper.SetValue(turret, "Range", range); } }
        void ArgumentHandling(string arg) { switch (arg.ToLower()) { case "reset_targeting": ResetAllDesignatorTargeting(); break; case "debug_toggle": _debugMode = !_debugMode; if (_debugMode) { GetBlockGroups(); } break; default: break; } }
        void ResetAllDesignatorTargeting()
        {
            foreach (var thisTurret in _allDesignators)
            {
                thisTurret.ResetTargetingToDefault();
                TerminalPropertiesHelper.SetValue(thisTurret, "Range", float.MaxValue); //still no damn setter for this
            }
        }
        #endregion

        #region Inter-Grid Comms
        struct FriendlyData { public Vector3D Position; public double Radius; public FriendlyData(Vector3D pos, double rad) { Position = pos; Radius = rad; } }
        void ProcessNetworkMessage()
        {
            byte relationship = 0; long entityId = 0; Vector3D position = default(Vector3D); double targetRadius = 0; while (_broadcastListener.HasPendingMessage)
            {
                object messageData = _broadcastListener.AcceptMessage().Data;
                if (messageData is MyTuple<byte, long, Vector3D, double>) // Item4 is ignored on ingest
                {
                    if (_biggestGrid == null) continue; var myTuple = (MyTuple<byte, long, Vector3D, double>)messageData; relationship = myTuple.Item1; if (relationship != 2)
                        continue; /* Ignore IFF message if not friendly */
                    entityId = myTuple.Item2; if (entityId == _biggestGrid.EntityId)
                        continue; /* Ignore if source ship is the same */
                    position = myTuple.Item3; targetRadius = myTuple.Item4; if (Vector3D.DistanceSquared(position, _biggestGrid.GetPosition()) < targetRadius)
                        continue; /* Ignore if we are within the bounding sphere */
                    _friendlyDataBuffer[entityId] = new FriendlyData(position, targetRadius);
                }
            }
        }
        IMyCubeGrid _biggestGrid = null; double _biggestGridRadius = 0; void NetworkTargets()
        {
            if (true)//broadcastIFF)
            { var myTuple = new MyTuple<byte, long, Vector3D, double>((byte)2, _biggestGrid.EntityId, _biggestGrid.WorldAABB.Center, _biggestGridRadius); IGC.SendBroadcastMessage(IgcTag, myTuple); }
            _friendlyData.Clear(); foreach (var kvp in _friendlyDataBuffer) { _friendlyData[kvp.Key] = kvp.Value; }
            _friendlyDataBuffer.Clear();
        }
        #endregion

        #region General Utilities
        bool DoesBlockExist(IMyTerminalBlock block) { return !Vector3D.IsZero(block.WorldMatrix.Translation); }
        void MigrateConfig()
        {
            if (!Me.CustomData.Contains(_iniMigrationKey)) return;
            // Hijack our INI builder for a bit...
            _iniOutput.Clear(); _iniOutput.Append(Me.CustomData); foreach (var keyValue in _iniMigrationDictionary) { _iniOutput.Replace(keyValue.Key, keyValue.Value); }
            Me.CustomData = _iniOutput.ToString(); _iniOutput.Clear(); Echo("Config Migrated!\n");
        }
        const int MAX_BSOD_WIDTH = 40; const string BSOD_TEMPLATE = "\n" + "{0} - v{1}\n" + "A fatal exception has occured at\n" + "{2}. The current\n" + "program will be terminated.\n" + "\n" + "EXCEPTION:\n" + "{3}\n" + "\n" + "* Please REPORT this crash message to\n" + "the Bug Reports discussion of this script\n" + "\n" + "* Press RECOMPILE to restart the program"; StringBuilder bsodBuilder = new StringBuilder(); void PrintBsod(IMyTextSurface surface, string scriptName, string version, float fontSize, Exception e) { surface.ContentType = ContentType.TEXT_AND_IMAGE; surface.Alignment = TextAlignment.CENTER; surface.FontSize = fontSize; surface.FontColor = Color.White; surface.BackgroundColor = Color.Blue; string exceptionStr = e.ToString(); string[] exceptionLines = exceptionStr.Split('\n'); bsodBuilder.Clear(); Echo(exceptionLines.Length.ToString()); foreach (string line in exceptionLines) { Echo("line: " + line + " | length: " + line.Length.ToString()); if (line.Length <= MAX_BSOD_WIDTH) { bsodBuilder.Append(line).Append("\n"); Echo("Fine"); } else { Echo("Breaking"); string[] words = line.Split(' '); int lineLength = 0; foreach (string word in words) { lineLength += word.Length; if (lineLength >= MAX_BSOD_WIDTH) { lineLength = 0; bsodBuilder.Append("\n"); } bsodBuilder.Append(word).Append(" "); } bsodBuilder.Append("\n"); } } surface.WriteText(string.Format(BSOD_TEMPLATE, scriptName.ToUpperInvariant(), version, DateTime.Now, bsodBuilder)); }
        void PrintRunning(string scriptName, string version, string date, float fontSize) { IMyTextSurface surface = Me.GetSurface(0); surface.FontColor = Color.White; surface.BackgroundColor = Color.Black; surface.ContentType = ContentType.TEXT_AND_IMAGE; surface.Alignment = TextAlignment.CENTER; surface.FontSize = fontSize; surface.WriteText($"\n\n\n{scriptName}\n(Version {version} - {date})\n\nRunning..."); }
        #endregion

        #region Turret Group Class
        class TurretGroup
        {
            #region Member Fields
            public StringBuilder EchoOutput = new StringBuilder(); public StringBuilder ErrorOutput = new StringBuilder(); public StringBuilder WarningOutput = new StringBuilder(); public StringBuilder IniOutput = new StringBuilder(); public MyIni Ini = new MyIni(); public bool IsRotorTurret { get; }
            public IMyBlockGroup ThisGroup { get; private set; }
            public enum TurretType { RotorTurret = 1, RotorGimbal = 2, SlavedAI = 4 }; public TurretType Type { get; private set; }
            Program _program; Dictionary<long, float> _rotorRestAngles = new Dictionary<long, float>(); Dictionary<Vector3D, bool> _scannedBlocks = new Dictionary<Vector3D, bool>(); Dictionary<long, FriendlyData> _friendlyData = new Dictionary<long, FriendlyData>(); List<IMyMotorStator> _secondaryElevationRotors = new List<IMyMotorStator>(); List<IMyCameraBlock> _cameras = new List<IMyCameraBlock>(); List<IMyShipToolBase> _tools = new List<IMyShipToolBase>(); List<IMyLightingBlock> _lights = new List<IMyLightingBlock>(); List<IMyUserControllableGun> _guns = new List<IMyUserControllableGun>(); List<IMyTimerBlock> _timers = new List<IMyTimerBlock>(); List<IMyTerminalBlock> _groupBlocks = new List<IMyTerminalBlock>(); List<IMyTerminalBlock> _vitalBlocks = new List<IMyTerminalBlock>(); List<IMyTerminalBlock> _slavedTurrets = new List<IMyTerminalBlock>(); List<IMyLargeTurretBase> _turretDesignators = new List<IMyLargeTurretBase>(); HashSet<IMyCubeGrid> _shipGrids = new HashSet<IMyCubeGrid>(); HashSet<IMyCubeGrid> _thisTurretGrids = new HashSet<IMyCubeGrid>(); Dictionary<IMyCubeGrid, IMyTerminalBlock> _gunGridDict = new Dictionary<IMyCubeGrid, IMyTerminalBlock>(); Dictionary<IMyCubeGrid, IMyTerminalBlock> _toolGridDict = new Dictionary<IMyCubeGrid, IMyTerminalBlock>(); Dictionary<IMyCubeGrid, IMyTerminalBlock> _lightGridDict = new Dictionary<IMyCubeGrid, IMyTerminalBlock>(); HashSet<IMyCubeGrid> _weaponGrids = new HashSet<IMyCubeGrid>(); HashSet<IMyCubeGrid> _elevationRotorGrids = new HashSet<IMyCubeGrid>(); List<IMyMotorStator> _unsortedRotors = new List<IMyMotorStator>(); DecayingIntegralPID _elevationPID, _azimuthPID; IMyTerminalBlock _rotorTurretReference; IMyMotorStator _mainElevationRotor; IMyMotorStator _azimuthRotor; IMyLargeTurretBase _designator; Vector3D _gridVelocity, _targetVec, _averageWeaponPos, _gravity, _lastTargetVelocity = Vector3D.Zero; MatrixD _lastAzimuthMatrix, _lastElevationMatrix; double _muzzleVelocity, _toleranceDotProduct, _proportionalGain, _integralGain, _derivativeGain, _toleranceAngle, _equilibriumRotationSpeed, _convergenceRange, _gameMaxSpeed, _integralDecayRatio, _autoEngagementRange = 800, _gravityMultiplier, _rocketInitVelocity, _rocketAcceleration, _timerTriggerInterval = 0.1, _timerElapsed = 141; bool _isSetup = false, _firstRun = true, _isRocket, _intersection = false, _avoidFriendlyFire = true, _isShooting = true, _toolsOn = true, _onlyShootWhenDesignatorShoots = false, _init = false; long _lastTargetEntityId = 0; int _framesSinceLastLock = 141, _returnToRestDelay = 20, _errorCount = 0, _warningCount = 0; string _designatorName, _elevationRotorName, _azimuthRotorName; double _horizontalSpread = 1.0; double _spreadTime = 1.0; double _currentHorizontalSpread = 0.0; int _horizontalSpreadSign = 1; const double RotorStopThresholdRad = 1.0 / 180.0 * Math.PI; enum DominantWeaponType { None = 0, Projectile = 1, Rocket = 2 };
            #endregion

            #region Constructor
            public TurretGroup(IMyBlockGroup group, TurretVariables defaultTurretVariables, Program program, TurretType turretType, HashSet<IMyCubeGrid> shipGrids, Dictionary<long, FriendlyData> friendlyData) { ThisGroup = group; Type = turretType; IsRotorTurret = ((TurretType.RotorTurret | TurretType.RotorGimbal) & Type) != 0; _program = program; _shipGrids = shipGrids; _elevationRotorName = defaultTurretVariables.ElevationRotorNameTag; _azimuthRotorName = defaultTurretVariables.AzimuthRotorNameTag; _designatorName = defaultTurretVariables.DesignatorNameTag; _gameMaxSpeed = defaultTurretVariables.GameMaxSpeed; _toleranceAngle = defaultTurretVariables.ToleranceAngle; _convergenceRange = defaultTurretVariables.ConvergenceRange; _toleranceDotProduct = Math.Cos(_toleranceAngle * Math.PI / 180); _onlyShootWhenDesignatorShoots = defaultTurretVariables.OnlyShootWhenDesignatorShoots; _friendlyData = friendlyData; if (IsRotorTurret) { _proportionalGain = defaultTurretVariables.ProportionalGain; _integralGain = defaultTurretVariables.IntegralGain; _derivativeGain = defaultTurretVariables.DerivativeGain; _equilibriumRotationSpeed = defaultTurretVariables.EquilibriumRotationSpeed; _integralDecayRatio = defaultTurretVariables.IntegralDecayRatio; SetPidValues(); } GetTurretGroupBlocks(); }
            #endregion

            #region Ini Config
            void WriteIni() { Ini.Clear(); Ini.TryParse(_program.Me.CustomData, ThisGroup.Name); Ini.Set(ThisGroup.Name, IniMuzzleVelocity, _muzzleVelocity); Ini.Set(ThisGroup.Name, IniIsRocket, _isRocket); Ini.Set(ThisGroup.Name, IniRocketInitVel, _rocketInitVelocity); Ini.Set(ThisGroup.Name, IniRocketAccel, _rocketAcceleration); Ini.Set(ThisGroup.Name, IniAvoidFriendly, _avoidFriendlyFire); Ini.Set(ThisGroup.Name, IniTolerance, _toleranceAngle); Ini.Set(ThisGroup.Name, IniConvergence, _convergenceRange); Ini.Set(ThisGroup.Name, IniEngagementRange, _autoEngagementRange); if (IsRotorTurret) { Ini.Set(ThisGroup.Name, IniKp, _proportionalGain); Ini.Set(ThisGroup.Name, IniKi, _integralGain); Ini.Set(ThisGroup.Name, IniIntegralDecayRatio, _integralDecayRatio); Ini.Set(ThisGroup.Name, IniKd, _derivativeGain); Ini.Set(ThisGroup.Name, IniRestRpm, _equilibriumRotationSpeed); Ini.Set(ThisGroup.Name, IniRestTime, _returnToRestDelay / UpdatesPerSecond); } Ini.Set(ThisGroup.Name, IniShootWhenDesignatorDoes, _onlyShootWhenDesignatorShoots); Ini.Set(ThisGroup.Name, IniTimerRetriggerInterval, _timerTriggerInterval); Ini.Set(ThisGroup.Name, IniGravityMult, _gravityMultiplier); IniOutput.Clear(); IniOutput.Append(Ini.ToString()); }
            void ParseIni() { Ini.Clear(); Ini.TryParse(_program.Me.CustomData, ThisGroup.Name); _muzzleVelocity = Ini.Get(ThisGroup.Name, IniMuzzleVelocity).ToDouble(_muzzleVelocity); _isRocket = Ini.Get(ThisGroup.Name, IniIsRocket).ToBoolean(_isRocket); _rocketInitVelocity = Ini.Get(ThisGroup.Name, IniRocketInitVel).ToDouble(_rocketInitVelocity); _rocketAcceleration = Ini.Get(ThisGroup.Name, IniRocketAccel).ToDouble(_rocketAcceleration); _avoidFriendlyFire = Ini.Get(ThisGroup.Name, IniAvoidFriendly).ToBoolean(_avoidFriendlyFire); _convergenceRange = Ini.Get(ThisGroup.Name, IniConvergence).ToDouble(_convergenceRange); _autoEngagementRange = Ini.Get(ThisGroup.Name, IniEngagementRange).ToDouble(_autoEngagementRange); _onlyShootWhenDesignatorShoots = Ini.Get(ThisGroup.Name, IniShootWhenDesignatorDoes).ToBoolean(_onlyShootWhenDesignatorShoots); _gravityMultiplier = Ini.Get(ThisGroup.Name, IniGravityMult).ToDouble(_gravityMultiplier); _timerTriggerInterval = Ini.Get(ThisGroup.Name, IniTimerRetriggerInterval).ToDouble(_timerTriggerInterval); double t = _toleranceAngle; _toleranceAngle = Ini.Get(ThisGroup.Name, IniTolerance).ToDouble(_toleranceAngle); if (t != _toleranceAngle) _toleranceDotProduct = Math.Cos(_toleranceAngle * Math.PI / 180); if (IsRotorTurret) { double kp = _proportionalGain, ki = _integralGain, kd = _derivativeGain, decay = _integralDecayRatio; _proportionalGain = Ini.Get(ThisGroup.Name, IniKp).ToDouble(_proportionalGain); _integralGain = Ini.Get(ThisGroup.Name, IniKi).ToDouble(_integralGain); _derivativeGain = Ini.Get(ThisGroup.Name, IniKd).ToDouble(_derivativeGain); _integralDecayRatio = Ini.Get(ThisGroup.Name, IniIntegralDecayRatio).ToDouble(_integralDecayRatio); _equilibriumRotationSpeed = Ini.Get(ThisGroup.Name, IniRestRpm).ToDouble(_equilibriumRotationSpeed); if (kp != _proportionalGain || ki != _integralGain || kd != _derivativeGain || decay != _integralDecayRatio) { SetPidValues(); } _returnToRestDelay = (int)(Ini.Get(ThisGroup.Name, IniRestTime).ToDouble(_returnToRestDelay / UpdatesPerSecond) * UpdatesPerSecond); } WriteIni(); }
            #endregion

            #region Grabbing Blocks
            public void UpdateGeneralSettings(TurretVariables defaultTurretVariables) { if (defaultTurretVariables == null) return; _elevationRotorName = defaultTurretVariables.ElevationRotorNameTag; _azimuthRotorName = defaultTurretVariables.AzimuthRotorNameTag; _designatorName = defaultTurretVariables.DesignatorNameTag; _gameMaxSpeed = defaultTurretVariables.GameMaxSpeed; }
            public void GetTurretGroupBlocks(bool verbose = false, TurretVariables defaultTurretVariables = null)
            {
                UpdateGeneralSettings(defaultTurretVariables);
                ThisGroup.GetBlocks(_groupBlocks); //TODO optimize this away
                switch (this.Type) { case TurretType.RotorTurret: _isSetup = GrabBlocks(_groupBlocks, verbose); break; case TurretType.RotorGimbal: _isSetup = GrabBlocksGimbal(_groupBlocks, verbose); break; case TurretType.SlavedAI: _isSetup = GrabBlocksAI(_groupBlocks, verbose); break; }
                if (!_isSetup) return; if (!_init) SetInitialWeaponParameters(); ParseIni();
            }
            void SetInitialWeaponParameters()
            {
                DominantWeaponType weaponType = GetDominantWeaponType(_guns); switch (weaponType)
                {
                    case DominantWeaponType.None: _muzzleVelocity = 3e8; _isRocket = false; break;
                    case DominantWeaponType.Projectile: _muzzleVelocity = 400; _isRocket = false; break;
                    case DominantWeaponType.Rocket:
                        _muzzleVelocity = 200; //212.8125;
                        _isRocket = true; _rocketAcceleration = 600; _rocketInitVelocity = 100; break;
                }
            }
            void AddRotorGridsToHash(IMyMotorStator rotor, bool addBase = true) { if (addBase) _thisTurretGrids.Add(rotor.CubeGrid); if (rotor.IsAttached) _thisTurretGrids.Add(rotor.TopGrid); }
            /*
            * I have these collection functions
            * (1) because they are cleaner and
            * (2) they save characters as they are tabulated less.
            */
            bool CollectRotorTurretBlocks(IMyTerminalBlock block)
            {
                if (!block.IsSameConstructAs(_program.Me)) return false; var turret = block as IMyLargeTurretBase; if (turret != null && StringExtensions.Contains(block.CustomName, _designatorName)) { if (!turret.IsFunctional) return false; _turretDesignators.Add(turret); EnableTurretAI(turret); return false; }
                var weapon = block as IMyUserControllableGun; if (weapon != null) { if (weapon is IMyLargeTurretBase) return false; _guns.Add(weapon); _gunGridDict[block.CubeGrid] = block; _weaponGrids.Add(block.CubeGrid); return false; }
                var cam = block as IMyCameraBlock; if (cam != null) { _cameras.Add(cam); _toolGridDict[block.CubeGrid] = block; _weaponGrids.Add(block.CubeGrid); return false; }
                var tool = block as IMyShipToolBase; if (tool != null) { _tools.Add(tool); _toolGridDict[block.CubeGrid] = block; _weaponGrids.Add(block.CubeGrid); return false; }
                var light = block as IMyLightingBlock; if (light != null) { _lights.Add(light); _lightGridDict[block.CubeGrid] = block; _weaponGrids.Add(block.CubeGrid); return false; }
                var timer = block as IMyTimerBlock; if (timer != null) { _timers.Add(timer); return false; }
                var rotor = block as IMyMotorStator; if (rotor != null && rotor.IsFunctional)
                {
                    if (this.Type == TurretType.RotorTurret && StringExtensions.Contains(block.CustomName, _elevationRotorName)) // Named elevation
                    { if (!rotor.IsAttached) { EchoWarning($"No rotor head for elevation\nrotor named '{rotor.CustomName}'\nSkipping this rotor..."); return false; } _secondaryElevationRotors.Add(rotor); AddRotorGridsToHash(block as IMyMotorStator); GetRotorRestAngle(rotor); }
                    else if (this.Type == TurretType.RotorTurret && StringExtensions.Contains(block.CustomName, _azimuthRotorName)) // Named azimuth
                    { if (_azimuthRotor != null) { EchoWarning("Only one azimuth rotor is\n allowed per turret. Additional ones\n will be ignored."); return false; } _azimuthRotor = block as IMyMotorStator; AddRotorGridsToHash(block as IMyMotorStator, false); GetRotorRestAngle(rotor); }
                    else // Unnamed: These will be sorted automatically
                    { _unsortedRotors.Add(rotor); }
                    return false;
                }
                return false;
            }
            void GetRotorRestAngle(IMyMotorStator rotor) { float restAngle = 0; if (float.TryParse(rotor.CustomData, out restAngle)) { _rotorRestAngles[rotor.EntityId] = MathHelper.ToRadians(restAngle) % MathHelper.TwoPi; } }
            IMyTerminalBlock GetTurretReferenceOnRotorHead(IMyMotorStator rotor) { IMyTerminalBlock block = null; IMyCubeGrid rotorHeadGrid = rotor.TopGrid; if (rotorHeadGrid == null) return null; if (_gunGridDict.TryGetValue(rotorHeadGrid, out block)) return block; if (_toolGridDict.TryGetValue(rotorHeadGrid, out block)) return block; if (_lightGridDict.TryGetValue(rotorHeadGrid, out block)) return block; return null; }
            void ClearBlocks() { _mainElevationRotor = null; _azimuthRotor = null; _designator = null; _rotorTurretReference = null; _guns.Clear(); _tools.Clear(); _lights.Clear(); _cameras.Clear(); _timers.Clear(); _thisTurretGrids.Clear(); _turretDesignators.Clear(); _secondaryElevationRotors.Clear(); _rotorRestAngles.Clear(); _gunGridDict.Clear(); _toolGridDict.Clear(); _lightGridDict.Clear(); _weaponGrids.Clear(); _unsortedRotors.Clear(); _elevationRotorGrids.Clear(); _vitalBlocks.Clear(); }
            bool GrabBlocks(List<IMyTerminalBlock> _groupBlocks, bool verbose)
            {
                ClearBlocks(); ClearErrorOutput(); ClearWarningOutput(); foreach (var block in _groupBlocks) { CollectRotorTurretBlocks(block); }
                /*
                * Get our elevation rotors by determining which rotors have weapons on their
                * rotor head grids.
                */
                for (int i = _unsortedRotors.Count - 1; i >= 0; --i) { var rotor = _unsortedRotors[i]; if (!rotor.IsAttached) { EchoWarning($"No rotor head for elevation\nrotor named '{rotor.CustomName}'\nSkipping this rotor..."); continue; } if (_weaponGrids.Contains(rotor.TopGrid)) { _secondaryElevationRotors.Add(rotor); _elevationRotorGrids.Add(rotor.CubeGrid); AddRotorGridsToHash(rotor); GetRotorRestAngle(rotor); _unsortedRotors.RemoveAt(i); } }
                /*
                * Grab the first elevation rotor that we have
                */
                foreach (var rotor in _secondaryElevationRotors) { _rotorTurretReference = GetTurretReferenceOnRotorHead(rotor); if (_rotorTurretReference != null) { _mainElevationRotor = rotor; break; } }
                /*
                * Now we fetch the azimuth rotor by determining which has an elevation rotor on
                * its rotor head grid.
                *
                * Note: I could break once we find one since only one azimuth is supported, but
                * this would make troubleshooting harder.
                */
                bool printMultipleAzimuthWarning = false; foreach (var rotor in _unsortedRotors) { if (rotor.IsAttached && _elevationRotorGrids.Contains(rotor.TopGrid)) { if (_azimuthRotor != null) { printMultipleAzimuthWarning = true; continue; } GetRotorRestAngle(rotor); AddRotorGridsToHash(rotor, false); _azimuthRotor = rotor; } }
                if (printMultipleAzimuthWarning) { EchoWarning("Only one azimuth rotor is\n allowed per turret. Additional ones\n will be ignored."); }
                bool noErrors = true; if (_guns.Count == 0 && _tools.Count == 0 && _cameras.Count == 0 && _lights.Count == 0) { if (verbose) EchoError("No weapons, tools, lights or\ncameras found"); noErrors = false; }
                if (_turretDesignators.Count == 0) { if (verbose) EchoError("No designators found"); noErrors = false; }
                if (_azimuthRotor == null) { if (verbose) EchoError("No azimuth rotor found"); noErrors = false; }
                if (_mainElevationRotor == null) { if (_secondaryElevationRotors.Count == 0 && verbose) EchoError("No elevation rotor(s) found"); else if (verbose) EchoError($"None of the {_secondaryElevationRotors.Count} elevation\nrotor(s) has weapons/tools attached to them"); noErrors = false; }
                else
                {
                    _secondaryElevationRotors.Remove(_mainElevationRotor); /* Remove main elevation rotor from the list so it isnt double counted. */
                }
                _vitalBlocks.Add(_mainElevationRotor); _vitalBlocks.Add(_azimuthRotor); _vitalBlocks.Add(_rotorTurretReference); _vitalBlocks.AddRange(_guns); _vitalBlocks.AddRange(_lights); _vitalBlocks.AddRange(_tools); _vitalBlocks.AddRange(_cameras); return noErrors;
            }
            bool GrabBlocksGimbal(List<IMyTerminalBlock> _groupBlocks, bool verbose) { ClearBlocks(); ClearErrorOutput(); ClearWarningOutput(); foreach (var block in _groupBlocks) { CollectRotorTurretBlocks(block); } bool printMultipleAzimuthWarning = false; foreach (var rotor in _unsortedRotors) { if (rotor.IsAttached) { if (_azimuthRotor != null) { printMultipleAzimuthWarning = true; continue; } GetRotorRestAngle(rotor); AddRotorGridsToHash(rotor, false); _rotorTurretReference = GetTurretReferenceOnRotorHead(rotor); if (_rotorTurretReference != null) { _azimuthRotor = rotor; break; } } } if (printMultipleAzimuthWarning) { EchoWarning("Only one azimuth rotor is\n allowed per turret. Additional ones\n will be ignored."); } bool noErrors = true; if (_guns.Count == 0 && _tools.Count == 0 && _cameras.Count == 0 && _lights.Count == 0) { if (verbose) EchoError("No weapons, tools, lights or\ncameras found"); noErrors = false; } if (_turretDesignators.Count == 0) { if (verbose) EchoError("No designators found"); noErrors = false; } if (_azimuthRotor == null) { if (verbose) EchoError("No azimuth rotor found"); noErrors = false; } _vitalBlocks.Add(_azimuthRotor); _vitalBlocks.Add(_rotorTurretReference); _vitalBlocks.AddRange(_guns); _vitalBlocks.AddRange(_lights); _vitalBlocks.AddRange(_tools); _vitalBlocks.AddRange(_cameras); return noErrors; }
            bool GrabBlocksAI(List<IMyTerminalBlock> groupBlocks, bool verbose)
            {
                _designator = null; _vitalBlocks.Clear(); _slavedTurrets.Clear(); _turretDesignators.Clear(); ClearErrorOutput(); ClearWarningOutput(); foreach (IMyTerminalBlock thisBlock in groupBlocks) { if (!thisBlock.IsSameConstructAs(_program.Me)) continue; var turret = thisBlock as IMyLargeTurretBase; if (turret == null) continue; if (StringExtensions.Contains(turret.CustomName, _designatorName) && turret.IsFunctional) { _turretDesignators.Add(turret); EnableTurretAI(turret); } else { TerminalPropertiesHelper.SetValue(turret, "Range", 1f); if (turret.EnableIdleRotation) turret.EnableIdleRotation = false; _slavedTurrets.Add(turret); } }
                bool setupError = false; if (_slavedTurrets.Count == 0) { if (verbose) EchoError($"No slaved AI turrets found"); setupError = true; }
                if (_turretDesignators.Count == 0) /* second null check (If STILL null) */
                { if (verbose) EchoError($"No designators found"); setupError = true; }
                _vitalBlocks.AddRange(_slavedTurrets); return !setupError;
            }
            // TODO: Add this to the block fetch func?
            DominantWeaponType GetDominantWeaponType<T>(List<T> weaponsAndTools) where T : class, IMyTerminalBlock { int projectileCount = 0; int rocketCount = 0; if (IsRotorTurret) { foreach (var block in weaponsAndTools) { if (block is IMySmallGatlingGun) projectileCount++; else if (block is IMySmallMissileLauncher) rocketCount++; } } else { foreach (var block in _slavedTurrets) { if (block is IMyLargeGatlingTurret || block is IMyLargeInteriorTurret) projectileCount++; else if (block is IMyLargeMissileTurret) rocketCount++; } } if (projectileCount == 0 && rocketCount == 0) return DominantWeaponType.None; else if (rocketCount > projectileCount) return DominantWeaponType.Rocket; else return DominantWeaponType.Projectile; }
            #endregion

            #region Main Entrypoint
            public void DoWork(Vector3D gridVelocity, Vector3D gravity, HashSet<IMyCubeGrid> allShipGrids, Dictionary<long, FriendlyData> friendlyData)
            {
                EchoOutput.Clear(); Echo($"_____________________________\n\n'{ThisGroup.Name}'\n");
                if (_isSetup) // Verify that all vital blocks are working
                    _isSetup = VerifyBlocks(_vitalBlocks);
                // If the turret group is not functional, grab blocks and return
                if (!_isSetup) { GetTurretGroupBlocks(true); if (IsRotorTurret) StopRotorMovement(); else ResetTurretTargeting(_slavedTurrets); return; }
                this._gridVelocity = gridVelocity; this._gravity = gravity; this._shipGrids = allShipGrids; this._friendlyData = friendlyData; if (_timerElapsed < _timerTriggerInterval) _timerElapsed += MainUpdateInterval; _averageWeaponPos = GetAverageWeaponPosition(); _designator = GetDesignatorTurret(_turretDesignators, _averageWeaponPos); switch (this.Type) { case TurretType.RotorTurret: case TurretType.RotorGimbal: HandleRotorAndGimbalTurrets(); break; case TurretType.SlavedAI: HandleSlavedAiTurret(); break; }
                Echo($"Instruction sum: {_program.Runtime.CurrentInstructionCount}");
            }
            void HandleRotorAndGimbalTurrets()
            {
                if (_designator == null) { ToggleWeaponsAndTools(false, false); return; }
                if ((_designator.IsUnderControl || _designator.HasTarget) && _designator.IsWorking)
                {
                    if (this.Type == TurretType.RotorTurret) RotorTurretTargeting();
                    else // rotor gimbal
                        RotorGimbalTargeting(); Echo($"Rotor {(Type == TurretType.RotorTurret ? "turret" : "gimbal")} is targeting"); _framesSinceLastLock = 0;
                }
                else { ToggleWeaponsAndTools(false, false); if (_framesSinceLastLock < _returnToRestDelay) { _framesSinceLastLock++; StopRotorMovement(); } else ReturnToEquilibrium(); Echo($"Rotor {(Type == TurretType.RotorTurret ? "turret" : "gimbal")} is idle"); }
                var num = _mainElevationRotor == null ? 0 : 1; Echo($"Targeting: {_designator.HasTarget || _designator.IsUnderControl}"); Echo($"Grid intersection: {_intersection}"); if (this.Type == TurretType.RotorTurret) { Echo($"Main elevation rotor: {(_mainElevationRotor == null ? "null" : _mainElevationRotor.CustomName)}"); Echo($"Elevation rotors: {_secondaryElevationRotors.Count + num}"); }
                Echo($"Weapons: {_guns.Count}"); Echo($"Tools: {_tools.Count}"); Echo($"Lights: {_lights.Count}"); Echo($"Cameras: {_cameras.Count}"); Echo($"Timers: {_timers.Count}"); Echo($"Designators: {_turretDesignators.Count}"); Echo($"Muzzle velocity: {_muzzleVelocity} m/s"); Echo($"Is Firing: {_isShooting}");
            }
            void HandleSlavedAiTurret() { if (_designator == null) { ToggleWeaponsAndTools(false, false); return; } if (_designator.EnableIdleRotation) _designator.EnableIdleRotation = false; if ((_designator.IsUnderControl || _designator.HasTarget) && _designator.IsWorking) { SlavedTurretTargeting(); Echo($"Slaved turret(s) targeting"); } else { if (_isShooting != false) { foreach (IMyLargeTurretBase thisTurret in _slavedTurrets) { TerminalPropertiesHelper.SetValue(thisTurret, "Shoot", false); if (thisTurret.EnableIdleRotation) thisTurret.EnableIdleRotation = false; } _isShooting = false; } Echo($"Slaved turret(s) idle"); } Echo($"Targeting: {_designator.HasTarget || _designator.IsUnderControl}"); Echo($"Grid intersection: {_intersection}"); Echo($"Slaved turrets: {_slavedTurrets.Count}"); Echo($"Designators: {_turretDesignators.Count}"); }
            #endregion

            #region Check Friendly Data
            bool IsOccludedByFriendlyShip(Vector3D position, Vector3D direction) { foreach (var kvp in _friendlyData) { var friendly = kvp.Value; if (Vector3D.DistanceSquared(friendly.Position, position) > _autoEngagementRange * _autoEngagementRange) continue; var perpDist = VectorMath.Rejection(friendly.Position - position, direction).LengthSquared(); if (perpDist < friendly.Radius) return true; } return false; }
            #endregion

            #region Helper Functions
            void SetPidValues() { _elevationPID = new DecayingIntegralPID(_proportionalGain, _integralGain, _derivativeGain, MainUpdateInterval, _integralDecayRatio); _azimuthPID = new DecayingIntegralPID(_proportionalGain, _integralGain, _derivativeGain, MainUpdateInterval, _integralDecayRatio); }
            private bool VerifyBlocks(List<IMyTerminalBlock> blocks) { foreach (var x in blocks) { if (IsClosed(x)) return false; } return true; }
            public static bool IsClosed(IMyTerminalBlock block) { if (block == null) return true; return Vector3D.IsZero(block.WorldMatrix.Translation); }
            void Echo(string data) { EchoOutput.AppendLine(data); }
            void EchoError(string data) { if (_errorCount == 0) ErrorOutput.Append("_____________________________\nErrors for '").Append(ThisGroup.Name).AppendLine("'"); ErrorOutput.Append($"> Error {++_errorCount}: ").AppendLine(data); }
            void EchoWarning(string data) { if (_warningCount == 0) WarningOutput.Append("_____________________________\nWarnings for '").Append(ThisGroup.Name).AppendLine("'"); WarningOutput.Append($"> Warning {++_warningCount}: ").AppendLine(data); }
            void ClearErrorOutput() { ErrorOutput.Clear(); _errorCount = 0; }
            void ClearWarningOutput() { WarningOutput.Clear(); _warningCount = 0; }
            #endregion

            #region Targeting Functions

            void TargetSpread(ref Vector3D targetDirection, ref Vector3D horizontalVector)
            {
                double _horizontalSpreadPerUpdate = 4.0 * _horizontalSpread / _spreadTime * MainUpdateInterval; 
                _currentHorizontalSpread += _horizontalSpreadPerUpdate * _horizontalSpreadSign; targetDirection += horizontalVector * _currentHorizontalSpread;
                
                if (Math.Abs(_currentHorizontalSpread) >= _horizontalSpread)
                {
                    _horizontalSpreadSign *= -1;
                }
            }
            Vector3D GetTargetPoint(Vector3D shooterPosition, IMyLargeTurretBase designator)
            {
                if (designator.IsUnderControl)
                {
                    _targetVec = designator.GetPosition() + VectorAzimuthElevation(designator) * _convergenceRange; _lastTargetEntityId = 0;
                }
                else if (designator.HasTarget)
                {
                    var targetInfo = designator.GetTargetedEntity();
                    /*
                    * We reset our PID controllers and make acceleration compute to zero to handle switching off targets.
                    */
                    if (targetInfo.EntityId != _lastTargetEntityId)
                    {
                        _lastTargetVelocity = targetInfo.Velocity;
                        if (IsRotorTurret)
                        {
                            _azimuthPID.Reset(); 
                            _elevationPID.Reset();
                        }
                    }
                    _lastTargetEntityId = targetInfo.EntityId; 
                    double timeToIntercept = 0; 
                    double projectileInitSpeed = 0;
                    double projectileAcceleration = 0; 
                    Vector3D gridVelocity;
                    /*
                    ** Predict a cycle ahead to overlead a slight bit. We want to overlead rather
                    ** than under lead because the aim point is computed at the beginning of a 0.1 s
                    ** time tick. This instead aims near the middle of the current time tick and the
                    ** next predicted time tick.
                    */
                    Vector3D targetPosition = MainUpdateInterval * ((Vector3D)targetInfo.Velocity - _gridVelocity) + targetInfo.Position;
                    
                    if (_isRocket)
                    {
                        projectileInitSpeed = _rocketInitVelocity; 
                        projectileAcceleration = _rocketAcceleration; 
                        gridVelocity = Vector3D.Zero;
                    }
                    else
                    {
                        gridVelocity = _gridVelocity;
                    }

                    timeToIntercept = CalculateTimeToIntercept(_muzzleVelocity, gridVelocity, shooterPosition, targetInfo.Velocity, targetPosition); 
                    Vector3D targetAcceleration = UpdatesPerSecond * (targetInfo.Velocity - _lastTargetVelocity); 
                    _targetVec = TrajectoryEstimation(timeToIntercept, targetPosition, targetInfo.Velocity, targetAcceleration, _gameMaxSpeed, shooterPosition, _gridVelocity, _muzzleVelocity, projectileInitSpeed, projectileAcceleration, _gravityMultiplier); _lastTargetVelocity = targetInfo.Velocity;
                }
                else { _lastTargetEntityId = 0; }
                return _targetVec;
            }

            /*
            ** Whip's Projectile Time To Intercept - Modified 07/21/2019
            */
            double CalculateTimeToIntercept(double projectileSpeed, Vector3D shooterVelocity, Vector3D shooterPosition, Vector3D targetVelocity, Vector3D targetPosition)
            {
                double timeToIntercept = -1; 
                Vector3D deltaPos = targetPosition - shooterPosition; 
                Vector3D deltaVel = targetVelocity - shooterVelocity; 
                Vector3D deltaPosNorm = VectorMath.SafeNormalize(deltaPos); 
                double closingSpeed = Vector3D.Dot(deltaVel, deltaPosNorm); 
                Vector3D closingVel = closingSpeed * deltaPosNorm; Vector3D lateralVel = deltaVel - closingVel; 
                double diff = projectileSpeed * projectileSpeed - lateralVel.LengthSquared();
               
                if (diff < 0)
                {
                    return 0;
                } 

                double projectileClosingSpeed = Math.Sqrt(diff) - closingSpeed; 
                double closingDistance = Vector3D.Dot(deltaPos, deltaPosNorm); 
                timeToIntercept = closingDistance / projectileClosingSpeed; 
                return timeToIntercept;
            }

            Vector3D TrajectoryEstimation(double timeToIntercept, Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, double targetMaxSpeed, Vector3D shooterPos, Vector3D shooterVel, double projectileMaxSpeed, double projectileInitSpeed = 0, double projectileAccMag = 0, double gravityMultiplier = 0)
            {
                bool projectileAccelerates = projectileAccMag > 1e-6; 
                bool hasGravity = gravityMultiplier > 1e-6; 
                double shooterVelScaleFactor = 1; 
                
                if (projectileAccelerates)
                {
                    /*
                    This is a rough estimate to smooth out our initial guess based upon the missile parameters.
                    The reasoning is that the longer it takes to reach max velocity, the more the initial velocity
                    has an overall impact on the estimated impact point.
                    */
                    shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);
                }
                /*
                Estimate our predicted impact point and aim direction
                */
                Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor); 
                Vector3D aimDirection = estimatedImpactPoint - shooterPos; Vector3D aimDirectionNorm = VectorMath.SafeNormalize(aimDirection); 
                Vector3D projectileVel = shooterVel; Vector3D projectilePos = shooterPos;
                
                if (projectileAccelerates)
                {
                    projectileVel += aimDirectionNorm * projectileInitSpeed;
                }
                else
                {
                    projectileVel += aimDirectionNorm * projectileMaxSpeed;
                }

                /*
                Target trajectory estimation. We do only 10 steps since PBs are instruction limited.
                */

                double dt = timeToIntercept * 0.1; // This can be a const somewhere
                double maxSpeedSq = targetMaxSpeed * targetMaxSpeed; 
                double projectileMaxSpeedSq = projectileMaxSpeed * projectileMaxSpeed; 
                Vector3D targetAccStep = targetAcc * dt; 
                Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt; 
                Vector3D gravityStep = _gravity * gravityMultiplier * dt; 
                Vector3D aimOffset = Vector3D.Zero; double minDiff = double.MaxValue; 
                for (int i = 0; i < 10; ++i) 
                { 
                    targetVel += targetAccStep; 
                    
                    if (targetVel.LengthSquared() > maxSpeedSq) 
                        targetVel = Vector3D.Normalize(targetVel) * targetMaxSpeed; 
                    
                    targetPos += targetVel * dt; 
                    if (projectileAccelerates) 
                    { 
                        projectileVel += projectileAccStep;
                        if (projectileVel.LengthSquared() > projectileMaxSpeedSq)
                        {
                            projectileVel = Vector3D.Normalize(projectileVel) * projectileMaxSpeed;
                        }
                    }

                    if (hasGravity)
                    {
                        projectileVel += gravityStep;
                    } 
                    
                    projectilePos += projectileVel * dt; 
                    Vector3D diff = (targetPos - projectilePos); 
                    double diffLenSq = diff.LengthSquared();
                    if (diffLenSq < minDiff)
                    {
                        minDiff = diffLenSq; aimOffset = diff;
                    }
                }
                return estimatedImpactPoint + aimOffset; //(targetPos - projectilePos);
            }

            Vector3D GetAverageTurretPosition()
            {
                Vector3D positionSum = Vector3D.Zero; if (_slavedTurrets.Count == 0) return positionSum;
                foreach (var block in _slavedTurrets)
                {
                    positionSum += block.GetPosition();
                } 
                return positionSum / _slavedTurrets.Count;
            }
            Vector3D GetAverageWeaponPosition()
            {
                Vector3D positionSum = Vector3D.Zero; if (_guns.Count != 0) { foreach (var block in _guns) { positionSum += block.GetPosition(); } return positionSum / _guns.Count; }
                /*
                * This is a fall-through in case the user has no guns. The code will use the
                * tools for alignment instead.
                */
                int toolCount = _lights.Count + _cameras.Count + _tools.Count; if (toolCount == 0) return positionSum; foreach (var block in _lights) { positionSum += block.GetPosition(); }
                foreach (var block in _cameras) { positionSum += block.GetPosition(); }
                foreach (var block in _tools) { positionSum += block.GetPosition(); }
                return positionSum / toolCount;
            }
            static void EnableTurretAI(IMyLargeTurretBase turret) { if (!turret.AIEnabled) turret.ResetTargetingToDefault(); turret.EnableIdleRotation = false; }
            #endregion

            #region Weapon Control
            void ToggleWeaponsAndTools(bool toggleWeapons, bool toggleLightsAndTools)
            {
                /*
                * This attempts to avoid spamming terminal actions if we have already set the shoot state.
                */
                if (_isShooting != toggleWeapons) { foreach (var weapon in _guns) TerminalPropertiesHelper.SetValue(weapon, "Shoot", toggleWeapons); _isShooting = toggleWeapons; }
                if (_toolsOn != toggleLightsAndTools) { ChangePowerState(_tools, toggleLightsAndTools); ChangePowerState(_lights, toggleLightsAndTools); _toolsOn = toggleLightsAndTools; }
                if (toggleWeapons && _timerElapsed >= _timerTriggerInterval) { foreach (var timer in _timers) { timer.Trigger(); } _timerElapsed = 0; }
            }
            static void ChangePowerState<T>(List<T> list, bool stateToSet) where T : class, IMyFunctionalBlock { foreach (IMyFunctionalBlock block in list) { if (block.Enabled != stateToSet) block.Enabled = stateToSet; } }
            #endregion

            #region Designator Selection
            IMyLargeTurretBase GetDesignatorTurret(List<IMyLargeTurretBase> turretDesignators, Vector3D referencePos) { IMyLargeTurretBase closestTurret = null; double closestDistanceSq = double.MaxValue; foreach (var block in turretDesignators) { if (IsClosed(block)) continue; if (block.IsUnderControl) return block; if (block.HasTarget) { var distanceSq = Vector3D.DistanceSquared(block.GetPosition(), referencePos); if (distanceSq + 1e-3 < closestDistanceSq) { closestDistanceSq = distanceSq; closestTurret = block; } } } if (closestTurret == null) { closestTurret = turretDesignators.Count == 0 ? null : turretDesignators[0]; } return closestTurret; }
            #endregion

            #region Rotor Gimbal Control
            void RotorGimbalTargeting()
            {
                Vector3D aimPosition = GetTargetPoint(_averageWeaponPos, _designator); 
                Vector3D targetDirection = aimPosition - _averageWeaponPos; 
                Vector3D turretFrontVec = _rotorTurretReference.WorldMatrix.Forward; 
                Vector3D baseUp = _azimuthRotor.WorldMatrix.Up; Vector3D baseLeft = baseUp.Cross(turretFrontVec); 
                
                if (!Vector3D.IsUnit(ref baseLeft)) 
                    baseLeft = Vector3D.Normalize(baseLeft); 

                Vector3D baseForward = baseLeft.Cross(baseUp); 
                MatrixD turretBaseMatrix = new MatrixD { Forward = baseForward, Left = baseLeft, Up = baseUp }; 
                TargetSpread(ref targetDirection, ref baseLeft);
                
                if (_avoidFriendlyFire)
                {
                    _intersection = CheckForFF(_averageWeaponPos, turretFrontVec, _azimuthRotor); 
                    if (!_intersection) 
                        _intersection = IsOccludedByFriendlyShip(_averageWeaponPos, turretFrontVec);
                }
                else
                    _intersection = IsOccludedByFriendlyShip(_averageWeaponPos, turretFrontVec); //false;

                double azimuthAngle; GetAzimuthAngle(ref targetDirection, ref turretBaseMatrix, out azimuthAngle); 
                azimuthAngle = GetAllowedRotationAngle(azimuthAngle, _azimuthRotor); 
                double azimuthError, tmp; ComputeShipHeadingError(out azimuthError, out tmp); 
                double azimuthSpeed = _azimuthPID.Control(azimuthAngle - azimuthError);
                /*
                * Negative because we want to cancel the positive angle via our movements.
                */
                _azimuthRotor.TargetVelocityRPM = -(float)azimuthSpeed; 
                if (!_azimuthRotor.Enabled) 
                    _azimuthRotor.Enabled = true; 
                bool inRange = _autoEngagementRange * _autoEngagementRange >= targetDirection.LengthSquared(); 
                bool angleWithinTolerance = VectorMath.IsDotProductWithinTolerance(turretFrontVec, targetDirection, _toleranceDotProduct); 
                bool toggleLights = _designator.IsUnderControl || _designator.HasTarget; 
                bool shootWeapons = false; 

                if (!_intersection && angleWithinTolerance)
                {
                    if (_designator.IsUnderControl && _designator.IsShooting) // If manually controlled
                    {
                        shootWeapons = true; 

                    }
                    else if (_designator.HasTarget && inRange) // If AI controlled
                    { 
                        if (!_onlyShootWhenDesignatorShoots) 
                            shootWeapons = true; 
                        else if (_onlyShootWhenDesignatorShoots && _designator.IsShooting) 
                            shootWeapons = true;
                    }
                }
                ToggleWeaponsAndTools(shootWeapons, toggleLights);
            }
            #endregion

            #region Rotor Turret Control
            void RotorTurretTargeting()
            {
                Vector3D aimPosition = GetTargetPoint(_averageWeaponPos, _designator); Vector3D targetDirection = aimPosition - _averageWeaponPos; Vector3D turretFrontVec = _rotorTurretReference.WorldMatrix.Forward; Vector3D absUpVec = _azimuthRotor.WorldMatrix.Up; Vector3D turretSideVec = _mainElevationRotor.WorldMatrix.Up; Vector3D turretFrontCrossSide = turretFrontVec.Cross(turretSideVec); Vector3D baseUp = absUpVec; Vector3D baseLeft = baseUp.Cross(turretFrontVec); if (!Vector3D.IsUnit(ref baseLeft)) baseLeft = Vector3D.Normalize(baseLeft); Vector3D baseForward = baseLeft.Cross(baseUp); MatrixD turretBaseMatrix = new MatrixD { Forward = baseForward, Left = baseLeft, Up = baseUp }; TargetSpread(ref targetDirection, ref baseLeft); if (_avoidFriendlyFire) { _intersection = CheckForFF(_averageWeaponPos, turretFrontVec, _azimuthRotor); if (!_intersection) _intersection = IsOccludedByFriendlyShip(_averageWeaponPos, turretFrontVec); }
                else
                    _intersection = IsOccludedByFriendlyShip(_averageWeaponPos, turretFrontVec); //false;

                /*
                * We need 2 sets of angles to be able to prevent the turret from trying to rotate over 90 deg
                * vertical to get to a target behind it. This ensures that the elevation angle is always
                * lies in the domain: -90 deg <= elevation <= 90 deg.
                */
                double desiredAzimuthAngle, desiredElevationAngle, currentElevationAngle, azimuthAngle, elevationAngle; 
                GetRotationAngles(ref targetDirection, ref turretBaseMatrix, out desiredAzimuthAngle, out desiredElevationAngle); 
                GetElevationAngle(ref turretFrontVec, ref turretBaseMatrix, out currentElevationAngle); 
                elevationAngle = (desiredElevationAngle - currentElevationAngle) * -Math.Sign(absUpVec.Dot(turretFrontCrossSide));
                azimuthAngle = desiredAzimuthAngle; // Current azimuth is always zero with this matrix definition.
                azimuthAngle = GetAllowedRotationAngle(azimuthAngle, _azimuthRotor); 
                double azimuthError, elevationError; ComputeShipHeadingError(out azimuthError, out elevationError); 
                double azimuthSpeed = _azimuthPID.Control(azimuthAngle - azimuthError); 
                double elevationSpeed = _elevationPID.Control(elevationAngle - elevationError);
                /*
                * Negative because we want to cancel the positive angle via our movements.
                */
                _azimuthRotor.TargetVelocityRPM = -(float)azimuthSpeed; _mainElevationRotor.TargetVelocityRPM = -(float)elevationSpeed; 
                if (!_azimuthRotor.Enabled) 
                    _azimuthRotor.Enabled = true; 

                if (!_mainElevationRotor.Enabled) 
                    _mainElevationRotor.Enabled = true; 

                bool inRange = _autoEngagementRange * _autoEngagementRange >= targetDirection.LengthSquared();
                bool angleWithinTolerance = VectorMath.IsDotProductWithinTolerance(turretFrontVec, targetDirection, _toleranceDotProduct); 
                bool toggleLights = _designator.IsUnderControl || _designator.HasTarget; 
                bool shootWeapons = false; 

                if (!_intersection && angleWithinTolerance)
                {
                    if (_designator.IsUnderControl && _designator.IsShooting) // If manually controlled
                    { shootWeapons = true; }
                    else if (_designator.HasTarget && inRange) // If AI controlled
                    { if (!_onlyShootWhenDesignatorShoots) shootWeapons = true; else if (_onlyShootWhenDesignatorShoots && _designator.IsShooting) shootWeapons = true; }
                }
                ToggleWeaponsAndTools(shootWeapons, toggleLights); foreach (var rotor in _secondaryElevationRotors) { HandleSecondaryElevationRotors(rotor, elevationSpeed, turretFrontVec, turretFrontCrossSide); }
            }
            void HandleSecondaryElevationRotors(IMyMotorStator rotor, double elevationSpeed, Vector3D turretFrontVec, Vector3D turretFrontCrossSide) { if (IsClosed(rotor)) return; IMyTerminalBlock reference = GetTurretReferenceOnRotorHead(rotor); if (reference == null) { EchoWarning($"No weapons, tools, cameras, or lights\non elevation rotor named\n'{rotor.CustomName}'\nSkipping this rotor..."); return; } if (!rotor.Enabled) rotor.Enabled = true; var desiredFrontVec = reference.WorldMatrix.Forward; float multiplier = Math.Sign(rotor.WorldMatrix.Up.Dot(_mainElevationRotor.WorldMatrix.Up)); var diff = (float)VectorMath.AngleBetween(desiredFrontVec, turretFrontVec) * Math.Sign(desiredFrontVec.Dot(turretFrontCrossSide)) * 100; rotor.TargetVelocityRPM = multiplier * (float)(-elevationSpeed - diff); if (!rotor.Enabled) rotor.Enabled = true; }
            void ComputeShipHeadingError(out double azimuthError, out double elevationError) { if (_firstRun) { _firstRun = false; if (this.Type == TurretType.RotorTurret) _lastElevationMatrix = _mainElevationRotor.WorldMatrix; _lastAzimuthMatrix = _azimuthRotor.WorldMatrix; azimuthError = 0; elevationError = 0; return; } azimuthError = CalculateRotorDeviationAngle(_azimuthRotor.WorldMatrix.Forward, _lastAzimuthMatrix); if (this.Type == TurretType.RotorTurret) elevationError = CalculateRotorDeviationAngle(_mainElevationRotor.WorldMatrix.Forward, _lastElevationMatrix); else elevationError = 0; if (this.Type == TurretType.RotorTurret) _lastElevationMatrix = _mainElevationRotor.WorldMatrix; _lastAzimuthMatrix = _azimuthRotor.WorldMatrix; }
            static double GetAllowedRotationAngle(double initialAngle, IMyMotorStator rotor)
            {
                if (rotor.LowerLimitRad >= -MathHelper.TwoPi && rotor.UpperLimitRad <= MathHelper.TwoPi && rotor.UpperLimitRad - rotor.LowerLimitRad > Math.PI)
                {
                    var currentAngleVector = rotor.Top.WorldMatrix.Backward; //GetVectorFromRotorAngle(rotor.Angle, rotor);
                    var lowerLimitVector = GetVectorFromRotorAngle(rotor.LowerLimitRad, rotor); var upperLimitVector = GetVectorFromRotorAngle(rotor.UpperLimitRad, rotor); var upAxis = Vector3D.Cross(upperLimitVector, lowerLimitVector); var currentCrossLower = Vector3D.Cross(currentAngleVector, lowerLimitVector); var currentCrossUpper = Vector3D.Cross(currentAngleVector, lowerLimitVector); var angleToLowerLimit = Math.Acos(Vector3D.Dot(lowerLimitVector, currentAngleVector)); if (Vector3D.Dot(upAxis, currentCrossLower) > 0) angleToLowerLimit = MathHelper.TwoPi - angleToLowerLimit; var angleToUpperLimit = Math.Acos(Vector3D.Dot(upperLimitVector, currentAngleVector)); if (Vector3D.Dot(upAxis, currentCrossUpper) < 0) angleToUpperLimit = MathHelper.TwoPi - angleToUpperLimit;
                    if (initialAngle > 0) //rotating towards lower bound
                    {
                        if (angleToLowerLimit < Math.Abs(initialAngle))
                        {
                            var newAngle = -MathHelper.TwoPi + initialAngle; if (angleToUpperLimit < Math.Abs(newAngle)) return 0;
                            return newAngle; //rotate opposite direction
                        }
                    }
                    else
                    {
                        if (angleToUpperLimit < Math.Abs(initialAngle))
                        {
                            var newAngle = MathHelper.TwoPi + initialAngle; if (angleToLowerLimit < Math.Abs(newAngle)) return 0;
                            return newAngle;//rotate opposite direction
                        }
                    }
                    return initialAngle; //conditional fall-through
                }
                else return initialAngle;
            }
            void ReturnToEquilibrium() { MoveRotorToEquilibrium(_azimuthRotor); MoveRotorToEquilibrium(_mainElevationRotor); foreach (var block in _secondaryElevationRotors) { if (IsClosed(block)) continue; MoveRotorToEquilibrium(block); } }
            void MoveRotorToEquilibrium(IMyMotorStator rotor) { if (IsClosed(rotor)) return; if (rotor == null) return; if (!rotor.Enabled) rotor.Enabled = true; float restAngle = 0; float currentAngle = rotor.Angle; float lowerLimitRad = rotor.LowerLimitRad; float upperLimitRad = rotor.UpperLimitRad; if (_rotorRestAngles.TryGetValue(rotor.EntityId, out restAngle)) { if (lowerLimitRad >= -MathHelper.TwoPi && upperLimitRad <= MathHelper.TwoPi) { if (restAngle > upperLimitRad) restAngle -= MathHelper.TwoPi; else if (restAngle < lowerLimitRad) restAngle += MathHelper.TwoPi; } else { if (restAngle > currentAngle + MathHelper.Pi) restAngle -= MathHelper.TwoPi; else if (restAngle < currentAngle - MathHelper.Pi) restAngle += MathHelper.TwoPi; } } else { if (lowerLimitRad >= -MathHelper.TwoPi && upperLimitRad <= MathHelper.TwoPi) restAngle = (lowerLimitRad + upperLimitRad) * 0.5f; else restAngle = currentAngle; } float angularDeviation = (restAngle - currentAngle); float targetVelocity = (float)Math.Round(angularDeviation * _equilibriumRotationSpeed, 2); if (Math.Abs(angularDeviation) < RotorStopThresholdRad) { rotor.TargetVelocityRPM = 0; } else { rotor.TargetVelocityRPM = targetVelocity; } }
            void StopRotorMovement() { if (_azimuthRotor != null && !IsClosed(_azimuthRotor)) _azimuthRotor.TargetVelocityRPM = 0; if (_mainElevationRotor != null && !IsClosed(_mainElevationRotor)) _mainElevationRotor.TargetVelocityRPM = 0; foreach (var rotor in _secondaryElevationRotors) { if (IsClosed(rotor)) continue; rotor.TargetVelocityRPM = 0f; } }
            #endregion

            #region Slaved Turret Control
            void SlavedTurretTargeting() { _isShooting = false; foreach (IMyLargeTurretBase turret in _slavedTurrets) { SlaveAITurret(turret); } }
            void SlaveAITurret(IMyLargeTurretBase turret)
            {
                Vector3D aimPosition = GetTargetPoint(turret.GetPosition(), _designator); MatrixD turretMatrix = turret.WorldMatrix; Vector3D turretDirection = VectorAzimuthElevation(turret); Vector3D targetDirectionNorm = Vector3D.Normalize(aimPosition - turretMatrix.Translation); if (_avoidFriendlyFire) { _intersection = CheckForFF(turret.GetPosition(), targetDirectionNorm, turret); if (!_intersection) _intersection = IsOccludedByFriendlyShip(turret.GetPosition(), targetDirectionNorm); }
                else
                    _intersection = IsOccludedByFriendlyShip(turret.GetPosition(), targetDirectionNorm);//false;
                                                                                                        //This shit is broke yo
                                                                                                        //thisTurret.SetTarget(aimPosition);
                double azimuth = 0; double elevation = 0; GetRotationAngles(ref targetDirectionNorm, ref turretMatrix, out azimuth, out elevation); turret.Azimuth = (float)azimuth; turret.Elevation = (float)elevation;
                turret.SyncAzimuth(); //this syncs both angles
                bool inRange = _autoEngagementRange * _autoEngagementRange > Vector3D.DistanceSquared(aimPosition, turretMatrix.Translation); bool withinAngleTolerance = VectorMath.IsDotProductWithinTolerance(turretDirection, turretDirection, _toleranceDotProduct); bool shouldShoot = false; if (withinAngleTolerance && !_intersection) { if (_designator.IsUnderControl && _designator.IsShooting) shouldShoot = true; else if (_designator.HasTarget) { if (inRange) { if (!_onlyShootWhenDesignatorShoots) shouldShoot = true; else if (_designator.IsShooting) shouldShoot = true; } } }
                if (TerminalPropertiesHelper.GetValue<bool>(turret, "Shoot") != shouldShoot) //TODO: Check if .IsShooting works
                    TerminalPropertiesHelper.SetValue(turret, "Shoot", shouldShoot); _isShooting |= shouldShoot; if (turret.EnableIdleRotation) turret.EnableIdleRotation = false;
            }
            static void ResetTurretTargeting(List<IMyTerminalBlock> turrets)
            {
                foreach (var block in turrets)
                {
                    var thisTurret = block as IMyLargeTurretBase; if (thisTurret == null) continue; if (!thisTurret.AIEnabled)
                    {
                        thisTurret.ResetTargetingToDefault(); thisTurret.EnableIdleRotation = false;
                        TerminalPropertiesHelper.SetValue(thisTurret, "Shoot", false); //still no damn setter for this
                        TerminalPropertiesHelper.SetValue(thisTurret, "Range", float.MaxValue); //still no damn setter for this
                    }
                }
            }
            #endregion

            #region Vector Math Functions

            static void WrapAngleAroundPI(ref float angle)
            {
                angle %= MathHelper.TwoPi; 
                if (angle > Math.PI) 
                    angle = -MathHelper.TwoPi + angle; 
                else if (angle < -Math.PI) 
                    angle = MathHelper.TwoPi + angle;
            }

            static Vector3D GetVectorFromRotorAngle(float angle, IMyMotorStator rotor)
            {
                double x = MyMath.FastSin(angle); 
                double y = MyMath.FastCos(angle); 
                var rotorMatrix = rotor.WorldMatrix; 
                return rotorMatrix.Backward * y + rotor.WorldMatrix.Left * x;
            }

            static double CalculateRotorDeviationAngle(Vector3D forwardVector, MatrixD lastOrientation)
            {
                var flattenedForwardVector = VectorMath.Rejection(forwardVector, lastOrientation.Up); 
                return VectorMath.AngleBetween(flattenedForwardVector, lastOrientation.Forward) * Math.Sign(flattenedForwardVector.Dot(lastOrientation.Left));
            }

            static Vector3D VectorAzimuthElevation(IMyLargeTurretBase turret)
            {
                double el = turret.Elevation; double az = turret.Azimuth; 
                Vector3D targetDirection; Vector3D.CreateFromAzimuthAndElevation(az, el, out targetDirection); 
                return Vector3D.TransformNormal(targetDirection, turret.WorldMatrix);
            }
            static void GetRotationAngles(ref Vector3D targetVector, ref MatrixD matrix, out double yaw, out double pitch)
            {
                var localTargetVector = Vector3D.TransformNormal(targetVector, MatrixD.Transpose(matrix)); 
                var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
                yaw = VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is positive
                if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
                    yaw = Math.PI;
                if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                    pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
                else
                    pitch = VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
            }
            static void GetAzimuthAngle(ref Vector3D targetVector, ref MatrixD matrix, out double azimuth)
            {
                var localTargetVector = Vector3D.TransformNormal(targetVector, MatrixD.Transpose(matrix)); 
                var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
                azimuth = VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is positive
                if (Math.Abs(azimuth) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
                    azimuth = Math.PI;
            }
            static void GetElevationAngle(ref Vector3D targetVector, ref MatrixD matrix, out double pitch)
            {
                var localTargetVector = Vector3D.TransformNormal(targetVector, MatrixD.Transpose(matrix)); 
                var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
                if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                    pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
                else
                    pitch = VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
            }
            #endregion

            #region Intersection Checks

            //List<Vector3I> scannedPoints = new List<Vector3I>();
            bool CheckForFF(Vector3D start, Vector3D dirnNorm, IMyTerminalBlock ignoredBlock)
            {
                bool intersection = false;
                Vector3D end = start; //This may not be precise if target is off axis by a bunch
                if (_isRocket) end += (dirnNorm * (_muzzleVelocity - _gridVelocity.Dot(dirnNorm)) - _gridVelocity) * 5; else end += dirnNorm * 1000; foreach (var grid in _shipGrids) { if (_thisTurretGrids.Contains(grid)) continue; intersection = CheckGridIntersection(start, end, grid, ignoredBlock, true, MaxBlocksToCheckForFF); if (intersection) break; }
                return intersection;
            }
            /*
            * Checks for intersection of a line through a grid's blocks. Returns true if there is an intersection.
            */
            bool CheckGridIntersection(Vector3D startPosWorld, Vector3D endPosWorld, IMyCubeGrid cubeGrid, IMyTerminalBlock originBlock, bool checkFast = false, int maxIterations = 50)
            {
                Vector3D startPosGrid = WorldToGridVec(ref startPosWorld, cubeGrid); Vector3D endPosGrid = WorldToGridVec(ref endPosWorld, cubeGrid); var line = new LineD(startPosGrid, endPosGrid);
                double padding = cubeGrid.GridSizeEnum == MyCubeSize.Large ? 1.0 : 5.0; // Approx 2.5 meters of padding
                var box = new BoundingBoxD((Vector3D)cubeGrid.Min - 0.5 - padding, (Vector3D)cubeGrid.Max + 0.5 + padding); Vector3D boxMin = box.Min; Vector3D boxMax = box.Max; Echo($"Checking grid: {cubeGrid.CustomName}");
                // Check fast if possible to save on runtime
                if (checkFast && !PointInBox(ref startPosGrid, ref boxMin, ref boxMax)) { Echo("> Checked fast"); return CheckGridIntersectionFast(ref startPosGrid, ref endPosGrid, ref box); }
                Echo("> Checked slow"); var intersectedLine = new LineD(); if (!box.Intersect(ref line, out intersectedLine)) { Echo($"No intersection"); return false; }
                Vector3I startInt = Vector3I.Round(intersectedLine.From); Vector3I endInt = Vector3I.Round(intersectedLine.To); IMySlimBlock slim = originBlock.CubeGrid.GetCubeBlock(originBlock.Position); if (BlockExistsAtPoint(cubeGrid, ref startInt, slim)) return true; Vector3D diff = endInt - startInt; if (Vector3D.IsZero(diff)) return false; Vector3I sign = Vector3I.Sign(diff); Vector3D dirn = VectorMath.SafeNormalize(diff); Vector3D dirnAbs = dirn * (Vector3D)sign; Vector3D tMaxVec = 0.5 / dirnAbs; Vector3D tDelta = 2.0 * tMaxVec;
                //scannedPoints.Clear();
                Vector3I scanPos = startInt; for (int i = 0; i < maxIterations; ++i)
                {
                    //scannedPoints.Add(scanPos);
                    if (BlockExistsAtPoint(cubeGrid, ref scanPos, slim)) return true; if (!PointInBox(ref scanPos, ref boxMin, ref boxMax)) return false; int idx = GetMinIndex(ref tMaxVec); switch (idx) { case 0: scanPos.X += sign.X; tMaxVec.X += tDelta.X; break; case 1: scanPos.Y += sign.Y; tMaxVec.Y += tDelta.Y; break; case 2: scanPos.Z += sign.Z; tMaxVec.Z += tDelta.Z; break; }
                }
                return false;
            }
            int GetMinIndex(ref Vector3D vec) { var min = vec.AbsMin(); if (min == vec.X) return 0; if (min == vec.Y) return 1; return 2; }
            bool CheckGridIntersectionFast(ref Vector3D startPosGrid, ref Vector3D endPosGrid, ref BoundingBoxD box) { var line = new LineD(startPosGrid, endPosGrid); return box.Intersects(ref line); }
            static Vector3D WorldToGridVec(ref Vector3D position, IMyCubeGrid cubeGrid) { var direction = position - cubeGrid.GetPosition(); return Vector3D.TransformNormal(direction, MatrixD.Transpose(cubeGrid.WorldMatrix)) / cubeGrid.GridSize; }
            static bool BlockExistsAtPoint(IMyCubeGrid cubeGrid, ref Vector3I point, IMySlimBlock blockToIgnore = null) { if (!cubeGrid.CubeExists(point)) return false; var slim = cubeGrid.GetCubeBlock(point); return slim != blockToIgnore; }
            static bool PointInBox(ref Vector3I point, ref Vector3D boxMin, ref Vector3D boxMax) { var temp = (Vector3D)point; return PointInBox(ref temp, ref boxMin, ref boxMax); }
            static bool PointInBox(ref Vector3D point, ref Vector3D boxMin, ref Vector3D boxMax) { if (boxMin.X <= point.X && point.X <= boxMax.X && boxMin.Y <= point.Y && point.Y <= boxMax.Y && boxMin.Z <= point.Z && point.Z <= boxMax.Z) { return true; } return false; }
            #endregion
        }
        #endregion

        #region Getting All Grids
        readonly List<IMyMechanicalConnectionBlock> allMechanical = new List<IMyMechanicalConnectionBlock>(); readonly HashSet<IMyCubeGrid> _allShipGrids = new HashSet<IMyCubeGrid>(); void GetAllGrids() { _allShipGrids.Clear(); _allShipGrids.Add(Me.CubeGrid); GridTerminalSystem.GetBlocksOfType(allMechanical); foreach (var block in allMechanical) { _allShipGrids.Add(block.CubeGrid); if (block.IsAttached && block.TopGrid != null) _allShipGrids.Add(block.TopGrid); } }
        #endregion

        #region Helper Classes/Functions

        #region Circular Buffer
        public class CircularBuffer<T> { public readonly int Capacity; readonly T[] _array = null; int _setIndex = 0; int _getIndex = 0; public CircularBuffer(int capacity) { if (capacity < 1) throw new Exception($"Capacity of CircularBuffer ({capacity}) can not be less than 1"); Capacity = capacity; _array = new T[Capacity]; } public void Add(T item) { _array[_setIndex] = item; _setIndex = ++_setIndex % Capacity; } public T MoveNext() { T val = _array[_getIndex]; _getIndex = ++_getIndex % Capacity; return val; } public T Peek() { return _array[_getIndex]; } }
        #endregion

        #region Scheduler
        public class Scheduler
        {
            ScheduledAction _currentlyQueuedAction = null; bool _firstRun = true; bool _ignoreFirstRun; List<ScheduledAction> _scheduledActions = new List<ScheduledAction>(); List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>(); Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>(); Program _program; const double RUNTIME_TO_REALTIME = (1.0 / 60.0) / 0.0166666; public Scheduler(Program program, bool ignoreFirstRun = false) { _program = program; _ignoreFirstRun = ignoreFirstRun; }
            public void Update()
            {
                double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * RUNTIME_TO_REALTIME); if (_ignoreFirstRun && _firstRun) deltaTime = 0; _firstRun = false; _actionsToDispose.Clear(); foreach (ScheduledAction action in _scheduledActions) { action.Update(deltaTime); if (action.JustRan && action.DisposeAfterRun) { _actionsToDispose.Add(action); } }
                // Remove all actions that we should dispose
                _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x)); if (_currentlyQueuedAction == null)
                {
                    // If queue is not empty, populate current queued action
                    if (_queuedActions.Count != 0) _currentlyQueuedAction = _queuedActions.Dequeue();
                }
                // If queued action is populated
                if (_currentlyQueuedAction != null)
                {
                    _currentlyQueuedAction.Update(deltaTime); if (_currentlyQueuedAction.JustRan)
                    {
                        // Set the queued action to null for the next cycle
                        _currentlyQueuedAction = null;
                    }
                }
            }
            public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false, double timeOffset = 0) { ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun, timeOffset); _scheduledActions.Add(scheduledAction); }
            public void AddScheduledAction(ScheduledAction scheduledAction) { _scheduledActions.Add(scheduledAction); }
            public void AddQueuedAction(Action action, double updateInterval)
            {
                if (updateInterval <= 0)
                {
                    updateInterval = 0.001; // avoids divide by zero
                }
                ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, true); _queuedActions.Enqueue(scheduledAction);
            }
            public void AddQueuedAction(ScheduledAction scheduledAction) { _queuedActions.Enqueue(scheduledAction); }
        }
        public class ScheduledAction { public bool JustRan { get; private set; } = false; public bool DisposeAfterRun { get; private set; } = false; public double TimeSinceLastRun { get; private set; } = 0; public double RunInterval; double _runFrequency; Action _action; public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false, double timeOffset = 0) { _action = action; _runFrequency = runFrequency; RunInterval = 1.0 / _runFrequency; DisposeAfterRun = removeAfterRun; TimeSinceLastRun = timeOffset; } public void Update(double deltaTime) { TimeSinceLastRun += deltaTime; if (TimeSinceLastRun >= RunInterval) { _action.Invoke(); TimeSinceLastRun = 0; JustRan = true; } else { JustRan = false; } } }
        #endregion

        #region PID Class

        /// <summary>
        /// Discrete time PID controller class.
        /// </summary>
        public class PID { readonly double _kP = 0; readonly double _kI = 0; readonly double _kD = 0; double _timeStep = 0; double _inverseTimeStep = 0; double _errorSum = 0; double _lastError = 0; bool _firstRun = true; public double Value { get; private set; } public PID(double kP, double kI, double kD, double timeStep) { _kP = kP; _kI = kI; _kD = kD; _timeStep = timeStep; _inverseTimeStep = 1 / _timeStep; } protected virtual double GetIntegral(double currentError, double errorSum, double timeStep) { return errorSum + currentError * timeStep; } public double Control(double error) { var errorDerivative = (error - _lastError) * _inverseTimeStep; if (_firstRun) { errorDerivative = 0; _firstRun = false; } _errorSum = GetIntegral(error, _errorSum, _timeStep); _lastError = error; this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative; return this.Value; } public double Control(double error, double timeStep) { if (timeStep != _timeStep) { _timeStep = timeStep; _inverseTimeStep = 1 / _timeStep; } return Control(error); } public void Reset() { _errorSum = 0; _lastError = 0; _firstRun = true; } }
        public class DecayingIntegralPID : PID { readonly double _decayRatio; public DecayingIntegralPID(double kP, double kI, double kD, double timeStep, double decayRatio) : base(kP, kI, kD, timeStep) { _decayRatio = decayRatio; } protected override double GetIntegral(double currentError, double errorSum, double timeStep) { return errorSum = errorSum * (1.0 - _decayRatio) + currentError * timeStep; } }
        #endregion
        public static class StringExtensions { public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase) { return source?.IndexOf(toCheck, comp) >= 0; } }
        public static class VectorMath
        {
            public static Vector3D SafeNormalize(Vector3D a) { if (Vector3D.IsZero(a)) return Vector3D.Zero; if (Vector3D.IsUnit(ref a)) return a; return Vector3D.Normalize(a); }
            public static Vector3D Reflection(Vector3D a, Vector3D b, double rejectionFactor = 1) //reflect a over b
            { Vector3D project_a = Projection(a, b); Vector3D reject_a = a - project_a; return project_a - reject_a * rejectionFactor; }
            public static Vector3D Rejection(Vector3D a, Vector3D b) //reject a on b
            { if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) return Vector3D.Zero; return a - a.Dot(b) / b.LengthSquared() * b; }
            public static Vector3D Projection(Vector3D a, Vector3D b) { if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) return Vector3D.Zero; if (Vector3D.IsUnit(ref b)) return a.Dot(b) * b; return a.Dot(b) / b.LengthSquared() * b; }
            public static double ScalarProjection(Vector3D a, Vector3D b) { if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) return 0; if (Vector3D.IsUnit(ref b)) return a.Dot(b); return a.Dot(b) / b.Length(); }
            public static double AngleBetween(Vector3D a, Vector3D b) { if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) return 0; else return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1)); }
            public static double CosBetween(Vector3D a, Vector3D b) { if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) return 0; else return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1); }
            public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b, double tolerance) { double dot = Vector3D.Dot(a, b); double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance); return Math.Abs(dot) * dot > num; }
        }
        public class RuntimeTracker { public int Capacity { get; set; } public double Sensitivity { get; set; } public double MaxRuntime { get; private set; } public double MaxInstructions { get; private set; } public double AverageRuntime { get; private set; } public double AverageInstructions { get; private set; } private readonly Queue<double> _runtimes = new Queue<double>(); private readonly Queue<double> _instructions = new Queue<double>(); private readonly StringBuilder _sb = new StringBuilder(); private readonly int _instructionLimit; private readonly Program _program; public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.01) { _program = program; Capacity = capacity; Sensitivity = sensitivity; _instructionLimit = _program.Runtime.MaxInstructionCount; } public void AddRuntime() { double runtime = _program.Runtime.LastRunTimeMs; AverageRuntime = Sensitivity * (runtime - AverageRuntime) + AverageRuntime; _runtimes.Enqueue(runtime); if (_runtimes.Count == Capacity) { _runtimes.Dequeue(); } MaxRuntime = _runtimes.Max(); } public void AddInstructions() { double instructions = _program.Runtime.CurrentInstructionCount; AverageInstructions = Sensitivity * (instructions - AverageInstructions) + AverageInstructions; _instructions.Enqueue(instructions); if (_instructions.Count == Capacity) { _instructions.Dequeue(); } MaxInstructions = _instructions.Max(); } public string Write() { _sb.Clear(); _sb.AppendLine("\n_____________________________\nGeneral Runtime Info\n"); _sb.AppendLine($"Avg instructions: {AverageInstructions:n2}"); _sb.AppendLine($"Max instructions: {MaxInstructions:n0}"); _sb.AppendLine($"Avg complexity: {MaxInstructions / _instructionLimit:0.000}%"); _sb.AppendLine($"Avg runtime: {AverageRuntime:n4} ms"); _sb.AppendLine($"Max runtime: {MaxRuntime:n4} ms"); return _sb.ToString(); } }
        static class TerminalPropertiesHelper { static Dictionary<string, ITerminalAction> _terminalActionDict = new Dictionary<string, ITerminalAction>(); static Dictionary<string, ITerminalProperty> _terminalPropertyDict = new Dictionary<string, ITerminalProperty>(); public static void ApplyAction(IMyTerminalBlock block, string actionName) { ITerminalAction act; if (_terminalActionDict.TryGetValue(actionName, out act)) { act.Apply(block); return; } act = block.GetActionWithName(actionName); _terminalActionDict[actionName] = act; act.Apply(block); } public static void SetValue<T>(IMyTerminalBlock block, string propertyName, T value) { ITerminalProperty prop; if (_terminalPropertyDict.TryGetValue(propertyName, out prop)) { prop.Cast<T>().SetValue(block, value); return; } prop = block.GetProperty(propertyName); _terminalPropertyDict[propertyName] = prop; prop.Cast<T>().SetValue(block, value); } public static T GetValue<T>(IMyTerminalBlock block, string propertyName) { ITerminalProperty prop; if (_terminalPropertyDict.TryGetValue(propertyName, out prop)) { return prop.Cast<T>().GetValue(block); } prop = block.GetProperty(propertyName); _terminalPropertyDict[propertyName] = prop; return prop.Cast<T>().GetValue(block); } }
        #endregion

        #endregion

    }
}
