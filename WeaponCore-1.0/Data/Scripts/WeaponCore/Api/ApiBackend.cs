using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support
{
    internal class ApiBackend
    {
        internal readonly Dictionary<string, Delegate> ModApiMethods = new Dictionary<string, Delegate>()
        {
            ["MatchEntToShieldFastExt"] =
                new Func<MyEntity, bool, MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>>?>(TAPI_MatchEntToShieldFastExt),
        };

        private readonly Dictionary<string, Delegate> _terminalPbApiMethods = new Dictionary<string, Delegate>()
        {
            ["MatchEntToShieldFastExt"] =
                new Func<MyEntity, bool, MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>>?>(TAPI_MatchEntToShieldFastExt),
        };

        internal void Init()
        {
            var mod = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("WeaponCoreAPI");
            mod.Getter = (b) => ModApiMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(mod);

            var pb = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("WeaponCorePbAPI");
            pb.Getter = (b) => _terminalPbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyProgrammableBlock>(pb);
        }

        private static MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>>? TAPI_MatchEntToShieldFastExt(MyEntity entity, bool onlyIfOnline)
        {
            if (entity == null) return null;
            return null;
        }
    }
}
