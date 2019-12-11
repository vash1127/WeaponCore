using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore.Support
{
    internal class ApiBackend
    {
        internal readonly Dictionary<string, Delegate> ModApiMethods = new Dictionary<string, Delegate>()
        {
            ["GetCoreStaticLaunchers"]= new Func<List<MyDefinitionId>>(GetCoreStaticLaunchers),
            ["GetCoreTurrets"] = new Func<List<MyDefinitionId>>(GetCoreTurrets),
            ["SetTargetEntity"] = new Action<IMyEntity, IMyEntity>(SetTargetEntity),
            ["FireOnce"] = new Action<IMyTerminalBlock>(FireOnce),
            ["ToggleFire"] = new Action<IMyTerminalBlock, bool>(ToggleFire),
            ["WeaponReady"] = new Func<IMyTerminalBlock, bool?>(WeaponReady),
            ["GetMaxRange"] = new Func<IMyTerminalBlock, float?>(GetMaxRange),
            ["GetTurretTargetTypes"] = new Func<IMyTerminalBlock, List<List<Threat>>>(GetTurretTargetTypes),
            ["SetTurretTargetTypes"] = new Action<IMyTerminalBlock, List<List<Threat>>>(SetTurretTargetTypes),
            ["SetTurretRange"] = new Action<IMyTerminalBlock, float>(SetTurretRange),            
            ["GetTargetedEntity"] = new Func<IMyTerminalBlock, IMyEntity>(GetTargetedEntity),
            ["TargetPredictedPosition"] = new Func<IMyTerminalBlock, Vector3D?>(TargetPredictedPosition),
            ["GetHeatLevel"] = new Func<IMyTerminalBlock, float?>(GetHeatLevel),
            ["CurrentPower"] = new Func<IMyTerminalBlock, float?>(CurrentPower),
            ["MaxPower"] = new Func<MyDefinitionId, float?>(MaxPower),
            ["DisableRequiredPower"] = new Action<IMyTerminalBlock>(DisableRequiredPower)
        };

        private readonly Dictionary<string, Delegate> _terminalPbApiMethods = new Dictionary<string, Delegate>()
        {
            ["SetTargetEntity"] = new Action<IMyEntity, IMyEntity>(SetTargetEntity),
            ["FireOnce"] = new Action<IMyTerminalBlock>(FireOnce),
            ["ToggleFire"] = new Action<IMyTerminalBlock, bool>(ToggleFire),
            ["WeaponReady"] = new Func<IMyTerminalBlock, bool?>(WeaponReady),
            ["GetMaxRange"] = new Func<IMyTerminalBlock, float?>(GetMaxRange),
            ["GetTurretTargetTypes"] = new Func<IMyTerminalBlock, List<List<Threat>>>(GetTurretTargetTypes),
            ["SetTurretTargetTypes"] = new Action<IMyTerminalBlock, List<List<Threat>>>(SetTurretTargetTypes),
            ["SetTurretRange"] = new Action<IMyTerminalBlock, float>(SetTurretRange),
            ["GetTargetedEntity"] = new Func<IMyTerminalBlock, IMyEntity>(GetTargetedEntity),
            ["TargetPredictedPosition"] = new Func<IMyTerminalBlock, Vector3D?>(TargetPredictedPosition),
            ["GetHeatLevel"] = new Func<IMyTerminalBlock, float?>(GetHeatLevel),
            ["CurrentPower"] = new Func<IMyTerminalBlock, float?>(CurrentPower),
            ["MaxPower"] = new Func<MyDefinitionId, float?>(MaxPower)
        };

        internal void Init()
        {
            var pb = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("WeaponCorePbAPI");
            pb.Getter = (b) => _terminalPbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyProgrammableBlock>(pb);
        }

        private static List<MyDefinitionId> GetCoreStaticLaunchers()
        {
            return new List<MyDefinitionId>();
        }

        private static List<MyDefinitionId> GetCoreTurrets()
        {
            return new List<MyDefinitionId>();
        }

        private static void SetTargetEntity(IMyEntity shooter, IMyEntity target)
        {

        }

        private static void FireOnce(IMyTerminalBlock weaponBlock)
        {

        }

        private static void ToggleFire(IMyTerminalBlock weaponBlock, bool on)
        {

        }

        private static bool? WeaponReady(IMyTerminalBlock weaponBlock)
        {
            return false;
        }

        private static float? GetMaxRange(IMyTerminalBlock weaponBlock)
        {
            return 0f;
        }

        private static List<List<Threat>> GetTurretTargetTypes(IMyTerminalBlock weaponBlock)
        {
            return new List<List<Threat>>();
        }

        private static void SetTurretRange(IMyTerminalBlock weaponBlock, float range)
        {

        }

        private static void SetTurretTargetTypes(IMyTerminalBlock weaponBlock, List<List<Threat>> threats)
        {

        }

        private static IMyEntity GetTargetedEntity(IMyTerminalBlock weaponBlock)
        {
            return null;
        }

        private static Vector3D? TargetPredictedPosition(IMyTerminalBlock weaponBlock)
        {
            return Vector3D.Zero;
        }

        private static float? GetHeatLevel(IMyTerminalBlock weaponBlock)
        {
            return 0f;
        }

        private static float? CurrentPower(IMyTerminalBlock weaponBlock)
        {
            return 0f;
        }

        private static float? MaxPower(MyDefinitionId weaponBlock)
        {
            return 0f;
        }

        private static void DisableRequiredPower(IMyTerminalBlock weaponBlock)
        {

        }
    }
}
