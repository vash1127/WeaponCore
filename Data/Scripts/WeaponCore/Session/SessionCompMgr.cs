using Sandbox.Game.Entities;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        public struct CompChange
        {
            public WeaponComponent Comp;
            public GridAi Ai;
            public ChangeType Change;

            public enum ChangeType
            {
                Reinit,
                Init,
                PlatformInit,
                OnRemovedFromSceneQueue,
            }
        }

        private void StartComps()
        {
            var reassign = false;
            for (int i = 0; i < CompsToStart.Count; i++)
            {
                var weaponComp = CompsToStart[i];
                if (weaponComp.MyCube.CubeGrid.IsPreview)
                {
                    Log.Line($"[IsPreview] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.Ai.DelayedGridCleanUp(null);
                    weaponComp.RemoveCompList();
                    CompsToStart.Remove(weaponComp);
                    continue;
                }
                if (weaponComp.MyCube.CubeGrid.Physics == null && !weaponComp.MyCube.CubeGrid.MarkedForClose && weaponComp.MyCube.BlockDefinition.HasPhysics)
                    continue;
                if (weaponComp.Ai.MyGrid != weaponComp.MyCube.CubeGrid)
                {
                    if (!GridToFatMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                    {
                        Log.Line($"grid not yet in map1");
                        continue;
                    }

                    Log.Line($"[StartComps - gridMisMatch] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - WeaponName:{weaponComp.MyCube.BlockDefinition.Id.SubtypeId.String} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid} - {weaponComp.Ai.MyGrid.MarkedForClose}");
                    weaponComp.RemoveCompList();
                    InitComp(weaponComp.MyCube, false);
                    reassign = true;
                    CompsToStart.Remove(weaponComp);
                }
                else if (weaponComp.Platform.State == MyWeaponPlatform.PlatformState.Fresh)
                {
                    if (weaponComp.MyCube.MarkedForClose)
                    {
                        weaponComp.Ai.DelayedGridCleanUp(null);
                        CompsToStart.Remove(weaponComp);
                        continue;
                    }
                    if (!GridToFatMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                    {
                        //Log.Line($"grid not in map and Platform state is fresh: marked:{weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - working:{weaponComp.MyCube.IsWorking} - functional:{weaponComp.MyCube.IsFunctional} - {weaponComp.MyCube.CubeGrid.DebugName} - gridMisMatch:{weaponComp.Ai.MyGrid != weaponComp.MyCube.CubeGrid}");
                        continue;
                    }
                    //Log.Line($"[Init] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.MyCube.Components.Add(weaponComp);
                    CompsToStart.Remove(weaponComp);
                }
                else
                {
                    Log.Line($"comp didn't match CompsToStart condition, removing");
                    //Log.Line($"[Other] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - WeaponName:{weaponComp.Ob.SubtypeId.String} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.Ai.DelayedGridCleanUp(null);
                    CompsToStart.Remove(weaponComp);
                }
            }
            CompsToStart.ApplyRemovals();
            if (reassign)
            {
                CompsToStart.ApplyAdditions();
                StartComps();
            }
        }

        private void InitComp(MyCubeBlock cube, bool thread = true)
        {
            if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

            using (cube.Pin())
            {
                if (cube.MarkedForClose)
                    return;

                GridAi gridAi;
                if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                {
                    gridAi = GridAiPool.Get();
                    gridAi.Init(cube.CubeGrid, this);
                    GridTargetingAIs.TryAdd(cube.CubeGrid, gridAi);
                }

                var weaponComp = new WeaponComponent(gridAi, cube);
                if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp))
                {
                    weaponComp.AddCompList(); // thread safe because on first add nothing can access list from main thread?
                    var blockDef = cube.BlockDefinition.Id.SubtypeId;
                    if (!gridAi.WeaponCounter.ContainsKey(blockDef))
                        gridAi.WeaponCounter.TryAdd(blockDef, WeaponCountPool.Get());

                    CompsToStart.Add(weaponComp);
                    if (thread) CompsToStart.ApplyAdditions();
                }
            }
        }

        private void ChangeComps()
        {
            foreach (var change in CompChanges)
            {
                if (change.Change != CompChange.ChangeType.OnRemovedFromSceneQueue && !GridToFatMap.ContainsKey(change.Comp.MyCube.CubeGrid))
                    continue;

                CompChange removed;
                switch (change.Change)
                {
                    case CompChange.ChangeType.PlatformInit:
                        CompChanges.TryDequeue(out removed);
                        change.Comp.PlatformInit();
                        break;
                    case CompChange.ChangeType.Init:
                        CompChanges.TryDequeue(out removed);
                        change.Comp.Init();
                        break;
                    case CompChange.ChangeType.Reinit:
                        CompChanges.TryDequeue(out removed);
                        change.Comp.ReInit();
                        break;
                    case CompChange.ChangeType.OnRemovedFromSceneQueue:
                        CompChanges.TryDequeue(out removed);
                        change.Comp.OnRemovedFromSceneQueue();
                        break;
                }
            }
        }

        private void DelayedComps()
        {
            foreach (var delayed in CompsDelayed)
            {
                WeaponComponent remove;
                if (delayed.MyCube.MarkedForClose)
                    CompsDelayed.TryDequeue(out remove);
                else if (delayed.MyCube.IsFunctional)
                {
                    Log.Line($"delayed released");
                    CompsDelayed.TryDequeue(out remove);
                    CompChanges.Enqueue(new CompChange { Ai = delayed.Ai, Comp = delayed, Change = CompChange.ChangeType.PlatformInit });
                }
            }
        }

    }
}
