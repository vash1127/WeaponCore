using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        public struct CompReAdd
        {
            public WeaponComponent Comp;
            public GridAi Ai;
            public int AiVersion;
            public uint AddTick;
        }

        private bool CompRestricted(WeaponComponent comp)
        {
            var grid = comp.MyCube?.CubeGrid;

            GridAi ai;
            if (grid == null || !GridTargetingAIs.TryGetValue(grid, out ai))
                return false;

            MyOrientedBoundingBoxD b;
            BoundingSphereD s;
            MyOrientedBoundingBoxD blockBox;
            SUtils.GetBlockOrientedBoundingBox(comp.MyCube, out blockBox);

            if (IsWeaponAreaRestricted(comp.MyCube.BlockDefinition.Id.SubtypeId, blockBox, grid, comp.MyCube.EntityId, ai, out b, out s)) {

                if (!DedicatedServer) {

                    if (comp.MyCube.OwnerId == PlayerId)
                        MyAPIGateway.Utilities.ShowNotification($"Block {comp.MyCube.DisplayNameText} was placed too close to another gun", 10000);
                }

                if (IsServer)
                    comp.MyCube.CubeGrid.RemoveBlock(comp.MyCube.SlimBlock);
                return true;
            }

            return false;
        }

        private void StartComps()
        {
            for (int i = 0; i < CompsToStart.Count; i++) {

                var weaponComp = CompsToStart[i];
                if (weaponComp.MyCube.CubeGrid.IsPreview || CompRestricted(weaponComp)) {

                    PlatFormPool.Return(weaponComp.Platform);
                    weaponComp.Platform = null;
                    CompsToStart.Remove(weaponComp);
                    continue;
                }

                if (weaponComp.MyCube.CubeGrid.Physics == null && !weaponComp.MyCube.CubeGrid.MarkedForClose && weaponComp.MyCube.BlockDefinition.HasPhysics)
                    continue;

                QuickDisableGunsCheck = true;
                if (weaponComp.Platform.State == MyWeaponPlatform.PlatformState.Fresh) {

                    if (weaponComp.MyCube.MarkedForClose) {
                        CompsToStart.Remove(weaponComp);
                        continue;
                    }

                    if (!GridToInfoMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                        continue;

                    IdToCompMap[weaponComp.MyCube.EntityId] = weaponComp;
                    weaponComp.MyCube.Components.Add(weaponComp);
                    CompsToStart.Remove(weaponComp);
                }
                else {
                    Log.Line($"comp didn't match CompsToStart condition, removing");
                    CompsToStart.Remove(weaponComp);
                }
            }
            CompsToStart.ApplyRemovals();
        }

        private void InitComp(MyCubeBlock cube, bool thread = true)
        {
            using (cube.Pin())
            {
                if (cube.MarkedForClose)
                    return;

                var blockDef = ReplaceVanilla && VanillaIds.ContainsKey(cube.BlockDefinition.Id) ? VanillaIds[cube.BlockDefinition.Id] : cube.BlockDefinition.Id.SubtypeId;
                
                var weaponComp = new WeaponComponent(this, cube, blockDef);

                CompsToStart.Add(weaponComp);
                if (thread) CompsToStart.ApplyAdditions();
            }
        }

        private void ChangeReAdds()
        {
            for (int i = CompReAdds.Count - 1; i >= 0; i--)
            {
                var reAdd = CompReAdds[i];
                if (reAdd.Ai.Version != reAdd.AiVersion || Tick - reAdd.AddTick > 1200)
                {
                    CompReAdds.RemoveAtFast(i);
                    Log.Line($"ChangeReAdds reject: Age:{Tick - reAdd.AddTick} - Version:{reAdd.Ai.Version}({reAdd.AiVersion}) - Marked/Closed:{reAdd.Ai.MarkedForClose}({reAdd.Ai.Closed})");
                    continue;
                }

                if (!GridToInfoMap.ContainsKey(reAdd.Comp.MyCube.CubeGrid))
                    continue;

                if (reAdd.Comp.Ai != null && reAdd.Comp.Entity != null) 
                    reAdd.Comp.OnAddedToSceneTasks();
                //else Log.Line($"ChangeReAdds nullSkip: Version:{reAdd.Ai.Version}({reAdd.AiVersion}) - Marked/Closed:{reAdd.Ai.MarkedForClose}({reAdd.Ai.Closed})");
                CompReAdds.RemoveAtFast(i);
            }
        }

        private void DelayedComps(bool forceRemove = false)
        {
            for (int i = CompsDelayed.Count - 1; i >= 0; i--)
            {
                var delayed = CompsDelayed[i];
                if (forceRemove || delayed.Entity == null || delayed.Platform == null || delayed.MyCube.MarkedForClose || delayed.Platform.State != MyWeaponPlatform.PlatformState.Delay)
                {
                    if (delayed.Platform != null && delayed.Platform.State != MyWeaponPlatform.PlatformState.Delay)
                        Log.Line($"[DelayedComps skip due to platform != Delay] marked:{delayed.MyCube.MarkedForClose} - entityNull:{delayed.Entity == null} - force:{forceRemove}");

                    CompsDelayed.RemoveAtFast(i);
                }
                else if (delayed.MyCube.IsFunctional)
                {
                    delayed.PlatformInit();
                    CompsDelayed.RemoveAtFast(i);
                }
            }
        }

        private void DelayedAiCleanup()
        {
            for (int i = 0; i < DelayedAiClean.Count; i++)
            {
                var ai = DelayedAiClean[i];
                ai.AiDelayedClose();
                if (ai.Closed)
                    DelayedAiClean.Remove(ai);
            }
            DelayedAiClean.ApplyRemovals();
        }

        internal void CloseComps(MyEntity ent)
        {
            try
            {
                var cube = (MyCubeBlock)ent;
                cube.OnClose -= CloseComps;
                if (cube.CubeGrid.IsPreview)
                    return;

                WeaponComponent comp;
                if (!cube.Components.TryGet(out comp)) return;

                for (int i = 0; i < comp.Monitors.Length; i++) {
                    comp.Monitors[i].Clear();
                    comp.Monitors[i] = null;
                }

                //IdToCompMap.Remove(comp.MyCube.EntityId);

                if (comp.Platform.State == MyWeaponPlatform.PlatformState.Ready)
                {
                    comp.GeneralWeaponCleanUp();
                    comp.StopAllSounds();
                    comp.CleanCompParticles();
                    comp.CleanCompSounds();
                    comp.Platform.RemoveParts(comp);
                }

                if (comp.Ai != null)
                {
                    Log.Line("Comp still had AI on close");
                    comp.Ai = null;
                }
                
                if (comp.Registered)
                {
                    Log.Line($"comp still registered");
                    comp.RegisterEvents(false);
                }

                PlatFormPool.Return(comp.Platform);
                comp.Platform = null;
                var sinkInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = comp.GId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = null,
                };

                comp.MyCube.ResourceSink.Init(MyStringHash.GetOrCompute("Charging"), sinkInfo);
            }
            catch (Exception ex) { Log.Line($"Exception in DelayedCompClose: {ex}"); }
        }
    }
}
