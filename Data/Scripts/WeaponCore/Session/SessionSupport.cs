using System;
using System.Collections.Concurrent;
using ParallelTasks;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;
using WeaponCore.Platform;
using Sandbox.Game;
using VRage.Game.ModAPI;

namespace WeaponCore
{
    public partial class Session
    {
        internal void Timings()
        {
            _paused = false;
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
            Tick10 = Tick % 10 == 0;
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick120 = Tick % 120 == 0;
            Tick180 = Tick % 180 == 0;
            Tick300 = Tick % 300 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;
            if (Tick60) ExplosionCounter = 0;
            if (Count++ == 119)
            {
                Count = 0;
                UiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                UiOpacity = MyAPIGateway.Session.Config.UIOpacity;
            }
            LCount++;
            if (LCount == 129)
            {
                LCount = 0;
                ECount++;
                if (ECount == 10) ECount = 0;
            }
            if (!GameLoaded)
            {
                if (FirstLoop)
                {
                    if (!MiscLoaded)
                    {
                        MiscLoaded = true;
                        if (!IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);

                    }
                    GameLoaded = true;
                }
                else if (!FirstLoop)
                {
                    FirstLoop = true;
                    foreach (var t in AllDefinitions)
                    {
                        var name = t.Id.SubtypeName;
                        var contains = name.Contains("BlockArmor");
                        if (contains)
                        {
                            AllArmorBaseDefinitions.Add(t);
                            if (name.Contains("HeavyBlockArmor")) HeavyArmorBaseDefinitions.Add(t);
                        }
                    }
                }
            }

            if (ShieldMod && !ShieldApiLoaded && SApi.Load())
                ShieldApiLoaded = true;
        }

        internal int LoadAssigner()
        {
            if (_loadCounter + 1 > 119) _loadCounter = 0;
            else ++_loadCounter;

            return _loadCounter;
        }

        internal ConcurrentDictionary<MyDefinitionId, ConcurrentDictionary<MyInventory, MyFixedPoint>> GetMasterInventory()
        {

            if (InventoryPool.Count > 0) return InventoryPool.Pop();
            return new ConcurrentDictionary<MyDefinitionId, ConcurrentDictionary<MyInventory, MyFixedPoint>>(AmmoInventoriesMaster, MyDefinitionId.Comparer);
        }

        internal void ReturnMasterInventory(ConcurrentDictionary<MyDefinitionId, ConcurrentDictionary<MyInventory, MyFixedPoint>> inventory)
        {
            InventoryPool.Push(inventory);
        }

        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                Players[id] = player;
                PlayerEventId++;
                if (player.SteamUserId == AuthorSteamId) AuthorPlayerId = player.IdentityId;
            }
            return false;
        }

        internal static double ModRadius(double radius, bool largeBlock)
        {
            if (largeBlock && radius < 3) radius = 3;
            else if (largeBlock && radius > 25) radius = 25;
            else if (!largeBlock && radius > 5) radius = 5;

            radius = Math.Ceiling(radius);
            return radius;
        }

        public void WeaponDebug(Weapon w)
        {
            DsDebugDraw.DrawLine(w.MyPivotTestLine, Color.Green, 0.05f);
            DsDebugDraw.DrawLine(w.MyBarrelTestLine, Color.Red, 0.05f);
            DsDebugDraw.DrawLine(w.MyCenterTestLine, Color.Blue, 0.05f);
            DsDebugDraw.DrawLine(w.MyAimTestLine, Color.Black, 0.07f);
            //DsDebugDraw.DrawSingleVec(w.MyPivotPos, 1f, Color.White);
            //DsDebugDraw.DrawBox(w.targetBox, Color.Plum);
            DsDebugDraw.DrawLine(w.LimitLine.From, w.LimitLine.To, Color.Orange, 0.05f);

            if (w.Target.State == Target.Targets.Acquired)
                DsDebugDraw.DrawLine(w.MyShootAlignmentLine, Color.Yellow, 0.05f);
        }

        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        private void Paused()
        {
            Pause = true;
            Log.Line($"Stopping all AV due to pause");
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                foreach (var comp in gridAi.WeaponBase.Values)
                    comp.StopAllAv();
            }

            foreach (var p in Projectiles.ProjectilePool.Active)
                p.PauseAv();
        }

        public bool TaskHasErrors(ref Task task, string taskName)
        {
            if (task.Exceptions != null && task.Exceptions.Length > 0)
            {
                foreach (var e in task.Exceptions)
                {
                    Log.Line($"{taskName} thread!\n{e}");
                }

                return true;
            }

            return false;
        }
    }
}