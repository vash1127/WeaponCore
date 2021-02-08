using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal class BlockSupports
    {
        internal readonly ConcurrentDictionary<MyStringHash, SupportSys.SupportComponent> ActiveSupports = new ConcurrentDictionary<MyStringHash, SupportSys.SupportComponent>(MyStringHash.Comparer);
        internal Ai Ai;
        internal IMySlimBlock Block;

        internal bool AddSupport(SupportSys.SupportComponent support, MyCube myCube = null, Ai ai = null)
        {
            if (myCube != null && Block != null)
                Log.Line($"AddSupport already had block");

            var validInit = true;
            if (myCube != null && ai != null) {
                Ai = ai;
                Block = myCube.CubeBlock;
                validInit = ai.Session.ActiveSupports.TryAdd(Block, support);
            }

            return validInit && ActiveSupports.TryAdd(support.SubTypeId, support);
        }

        internal bool RemoveSupport(SupportSys.SupportComponent support)
        {
            SupportSys.SupportComponent oldSupport;
            var removed = ActiveSupports.TryRemove(support.SubTypeId, out oldSupport);
            if (removed && ActiveSupports.IsEmpty)
                Clean(true);

            return removed;
        }

        internal bool Clean(bool skipRemove = false)
        {
            Ai.Session.BlockSupportsPool.Return(this);
            ActiveSupports.Clear();
            SupportSys.SupportComponent oldSupport;
            var removed = skipRemove || Ai.Session.ActiveSupports.TryRemove(Block, out oldSupport);
            Ai = null;
            Log.Line($"cleaning up BlockSupport");
            return removed;
        }
    }
}
