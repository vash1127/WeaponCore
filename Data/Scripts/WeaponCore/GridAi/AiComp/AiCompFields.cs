using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class AiComponent
    {
        internal readonly uint[] MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];
        internal readonly Session Session;
        internal readonly MyCubeGrid MyGrid;
        internal GridAi Ai;
        public AiComponent(Session session, MyCubeGrid grid)
        {
            Session = session;
            MyGrid = grid;
        }        
    }
}
