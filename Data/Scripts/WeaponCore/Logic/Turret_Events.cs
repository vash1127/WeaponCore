using System;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Logic
    {
        internal void RegisterEvents(bool register = true)
        {
            if (register)
            {
                Turret.AppendingCustomInfo += AppendingCustomInfo;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);

            }
            else
            {
                Turret.AppendingCustomInfo -= AppendingCustomInfo;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsWorking = myCubeBlock.IsWorking;
            IsFunctional = myCubeBlock.IsFunctional;
        }

        internal string GetShieldStatus()
        {
            if (!State.Value.Online && !MyCube.IsFunctional) return "[Controller Faulty]";
            if (!State.Value.Online && !MyCube.IsWorking) return "[Controller Offline]";
            return "[Shield Up]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetShieldStatus();
                if (status == "[Shield Up]" || status == "[Shield Down]" || status == "[Shield Offline]" || status == "[Insufficient Power]")
                {
                    stringBuilder.Append(status +
                                         "\n" +
                                         "\n[Shield Power]: " + SinkCurrentPower.ToString("0.0") + " Mw");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller AppendingCustomInfo: {ex}"); }
        }
    }
}