using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;

namespace WeaponCore
{
    public partial class Session
    {

        internal class TerminalMonitor
        {
            internal Session Session;
            internal WeaponComponent Comp;
            internal int OriginalAiVersion;
            internal bool Active;

            internal TerminalMonitor(Session session)
            {
                Session = session;
            }

            internal void Update(WeaponComponent comp, bool isCaller = false)
            {
                Comp = comp;
                Active = true;
                OriginalAiVersion = comp.Ai.Version;
                comp.Ai.Construct.RootAi.ActiveWeaponTerminal = comp.MyCube;

                if (comp.IsAsleep)
                    comp.WakeupComp();

                if (Session.IsClient && isCaller)
                {
                    //SyncGoesHere
                }
            }

            internal void Clean(bool isCaller = false)
            {
                if (Comp != null && Comp.Ai.Version == OriginalAiVersion)
                {
                    Comp.Ai.Construct.RootAi.ActiveWeaponTerminal = null;

                    if (Session.IsClient && isCaller)
                    {
                        //SyncGoesHere
                    }

                }

                Comp = null;
                OriginalAiVersion = -1;
                Active = false;
            }

            internal void Monitor()
            {
                if (IsActive())
                {

                    if (Session.Tick20)
                        Comp.TerminalRefresh();
                }
                else if (Active)
                    Clean();
            }

            internal bool IsActive()
            {
                if (Comp?.Ai == null) return false;

                var sameVersion = Comp.Ai.Version == OriginalAiVersion;
                var nothingMarked = !Comp.MyCube.MarkedForClose && !Comp.Ai.MyGrid.MarkedForClose && !Comp.Ai.MyGrid.MarkedForClose;
                var sameGrid = Comp.MyCube.CubeGrid == Comp.Ai.MyGrid;
                var inTerminalWindow = Session.InMenu && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
                var compReady = Comp.Platform.State == MyWeaponPlatform.PlatformState.Ready;
                var sameTerminalBlock = Session.LastTerminal == Comp.Ai.Construct.RootAi?.ActiveWeaponTerminal;

                return (sameVersion && nothingMarked && sameGrid && compReady && inTerminalWindow && sameTerminalBlock);
            }


            internal void Purge()
            {
                Clean();
                Session = null;
            }
        }

        internal class AcquireManager
        {
            internal Session Session;
            internal readonly HashSet<WeaponAcquire> Awake = new HashSet<WeaponAcquire>();
            internal readonly HashSet<WeaponAcquire> Asleep = new HashSet<WeaponAcquire>();

            internal readonly List<WeaponAcquire> Collector = new List<WeaponAcquire>();
            internal readonly List<WeaponAcquire> Removal = new List<WeaponAcquire>();

            internal int LastSleepSlot = -1;
            internal int LastAwakeSlot = -1;
            internal int WasAwake;
            internal int WasAsleep;

            internal AcquireManager(Session session)
            {
                Session = session;
            }

            internal void Awaken(WeaponAcquire wa)
            {
                var notValid = !wa.Weapon.Set.Enable || !wa.Weapon.Comp.State.Value.Online || !wa.Weapon.Comp.Set.Value.Overrides.Activate || !wa.Weapon.TrackTarget || Session.IsClient;
                if (notValid)
                {
                    if (!Session.IsClient) Log.Line($"cannot awaken: wEnable:{wa.Weapon.Set.Enable} - cOnline:{wa.Weapon.Comp.State.Value.Online} - cOverride:{wa.Weapon.Comp.Set.Value.Overrides.Activate} - tracking:{wa.Weapon.TrackTarget}");
                    return;
                }

                wa.CreatedTick = Session.Tick;

                if (!wa.Asleep)
                    return;

                Asleep.Remove(wa);

                AddAwake(wa);
            }

            internal void AddAwake(WeaponAcquire wa)
            {
                var notValid = !wa.Weapon.Set.Enable || !wa.Weapon.Comp.State.Value.Online || !wa.Weapon.Comp.Set.Value.Overrides.Activate || !wa.Weapon.TrackTarget || Session.IsClient;
                if (notValid)
                {
                    if (!Session.IsClient) Log.Line($"cannot add: wEnable:{wa.Weapon.Set.Enable} - cOnline:{wa.Weapon.Comp.State.Value.Online} - cOverride:{wa.Weapon.Comp.Set.Value.Overrides.Activate} - tracking:{wa.Weapon.TrackTarget}");
                    return;
                }

                wa.Enabled = true;
                wa.Asleep = false;
                wa.CreatedTick = Session.Tick;

                if (LastAwakeSlot < AwakeBuckets - 1)
                {

                    wa.SlotId = ++LastAwakeSlot;

                    Awake.Add(wa);
                }
                else
                {

                    wa.SlotId = LastAwakeSlot = 0;

                    Awake.Add(wa);
                }
            }

            internal void Remove(WeaponAcquire wa)
            {
                wa.Enabled = false;

                if (wa.Asleep)
                {

                    wa.Asleep = false;
                    Asleep.Remove(wa);
                }
                else
                {
                    Awake.Remove(wa);
                }
            }


            internal void UpdateAsleep()
            {
                WasAwake = 0;
                WasAwake += Awake.Count;

                foreach (var wa in Awake)
                {

                    if (wa.Weapon.Target.HasTarget)
                    {
                        Removal.Add(wa);
                        continue;
                    }

                    if (Session.Tick - wa.CreatedTick > 599)
                    {

                        if (LastSleepSlot < AsleepBuckets - 1)
                        {

                            wa.SlotId = ++LastSleepSlot;
                            wa.Asleep = true;

                            Asleep.Add(wa);
                            Removal.Add(wa);
                        }
                        else
                        {

                            wa.SlotId = LastSleepSlot = 0;
                            wa.Asleep = true;

                            Asleep.Add(wa);
                            Removal.Add(wa);
                        }
                    }
                }

                for (int i = 0; i < Removal.Count; i++)
                    Awake.Remove(Removal[i]);

                Removal.Clear();
            }


            internal void ReorderSleep()
            {
                foreach (var wa in Asleep)
                {

                    var remove = wa.Weapon.Target.HasTarget || wa.Weapon.Comp.IsAsleep || !wa.Weapon.Set.Enable || !wa.Weapon.Comp.State.Value.Online || !wa.Weapon.Comp.Set.Value.Overrides.Activate || Session.IsClient || !wa.Weapon.TrackTarget;

                    if (remove)
                    {
                        Removal.Add(wa);
                        continue;
                    }
                    Collector.Add(wa);
                }

                Asleep.Clear();

                for (int i = 0; i < Removal.Count; i++)
                    Remove(Removal[i]);

                WasAsleep = Collector.Count;

                ShellSort(Collector);

                LastSleepSlot = -1;
                for (int i = 0; i < Collector.Count; i++)
                {

                    var wa = Collector[i];
                    if (LastSleepSlot < AsleepBuckets - 1)
                    {

                        wa.SlotId = ++LastSleepSlot;

                        Asleep.Add(wa);
                    }
                    else
                    {

                        wa.SlotId = LastSleepSlot = 0;

                        Asleep.Add(wa);
                    }
                }
                Collector.Clear();
                Removal.Clear();
            }

            static void ShellSort(List<WeaponAcquire> list)
            {
                int length = list.Count;

                for (int h = length / 2; h > 0; h /= 2)
                {
                    for (int i = h; i < length; i += 1)
                    {
                        var tempValue = list[i];
                        var temp = list[i].Weapon.UniqueId;

                        int j;
                        for (j = i; j >= h && list[j - h].Weapon.UniqueId > temp; j -= h)
                        {
                            list[j] = list[j - h];
                        }

                        list[j] = tempValue;
                    }
                }
            }

            internal void Clean()
            {
                Awake.Clear();
                Asleep.Clear();
                Collector.Clear();
                Removal.Clear();
            }

        }

        public class WeaponAmmoMoveRequest
        {
            public Weapon Weapon;
            public List<InventoryMags> Inventories = new List<InventoryMags>();

            public void Clean()
            {
                Weapon = null;
                Inventories.Clear();
            }
        }

        public struct InventoryMags
        {
            public MyInventory Inventory;
            public int Amount;
        }

    }
}
