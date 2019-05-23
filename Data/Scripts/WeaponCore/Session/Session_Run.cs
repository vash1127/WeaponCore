using System;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using WeaponCore.Support;

namespace WeaponCore
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation, int.MinValue)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            try
            {
                BeforeStartInit();
                MyConfig.Init();
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }

        public override void Draw()
        {
            try
            {
                if (!DedicatedServer)
                {
                    for (int i = 0; i < _projectiles.Wait.Length; i++)
                        lock (_projectiles.Wait[i]) DrawLists(_projectiles.DrawProjectiles[i]);
                    if (_shrinking.Count > 0)
                        Shrink();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();
                if (!_projectiles.Hits.IsEmpty) ProcessHits();
                UpdateWeaponPlatforms();
                MyAPIGateway.Parallel.Start(_projectiles.Update);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void LoadData()
        {
            Instance = this;
            //MyEntities.OnEntityCreate += MyEntities_OnEntityCreate;
        }

        protected override void UnloadData()
        {
            SApi.Unload();

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PACKET_ID, ReceivedPacket);


            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;
            /*
            for (int i = 0; i < _projectiles.Wait.Length; i++)
            {
                lock (_projectiles.Wait[i])
                {
                    foreach (var a in _projectiles.ProjectilePool[i].Active)
                    {
                        a.Effect1.Clear();
                        //a.Effect1.Stop(true);
                    }
                    foreach (var m in _projectiles.ProjectilePool[i].Marked)
                    {
                        m.Effect1.Clear();
                        //m.Effect1.Stop(true);
                    }
                }
            }
            */
            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
}

