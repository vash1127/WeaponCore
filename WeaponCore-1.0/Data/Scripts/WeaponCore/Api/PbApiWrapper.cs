using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.ModAPI;

namespace WeaponCore.Support
{
    internal class WeaponCorePbApi
    {
        private IMyTerminalBlock _block;

        private readonly Func<IMyEntity, IMyTerminalBlock> _getCoreBlock;
        private readonly Func<IMyTerminalBlock, bool> _isCoreBlock;

        public void SetActiveShield(IMyTerminalBlock block) => _block = block; // AutoSet to TapiFrontend(block) if shield exists on grid.

        public WeaponCorePbApi(IMyTerminalBlock block)
        {
            _block = block;
            var delegates = _block.GetProperty("WeaponCorePbAPI")?.As<Dictionary<string, Delegate>>().GetValue(_block);
            if (delegates == null) return;

            _getCoreBlock = (Func<IMyEntity, IMyTerminalBlock>)delegates["GetCoreBlock"];

            if (!IsCoreBlock()) _block = GetCoreBlock(_block.CubeGrid) ?? _block;
        }
        public IMyTerminalBlock GetCoreBlock(IMyEntity entity) => _getCoreBlock?.Invoke(entity) ?? null;
        public bool IsCoreBlock() => _isCoreBlock?.Invoke(_block) ?? false;
    }
}
