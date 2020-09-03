using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using WeaponCore.Support;

namespace Jakaria
{
    public class WaterModAPI
    {
        public const ushort ModHandlerID = 50271;
        public const int ModAPIVersion = 8;

        /// <summary>
        /// List of all water objects in the world, null if not registered
        /// </summary>
        public List<Water> Waters { get; private set; }

        /// <summary>
        /// Invokes when the API recieves data from the Water Mod
        /// </summary>
        public event Action RecievedData;

        /// <summary>
        /// Invokes when a water is added to the Waters list
        /// </summary>
        public event Action WaterCreatedEvent;

        /// <summary>
        /// Invokes when a water is removed from the Waters list
        /// </summary>
        public event Action WaterRemovedEvent;

        /// <summary>
        /// Invokes when the water API becomes registered and ready to work
        /// </summary>
        public event Action OnRegisteredEvent;

        /// <summary>
        /// True if the API is registered/alive
        /// </summary>
        public bool Registered { get; private set; } = false;

        /// <summary>
        /// Used to tell in chat what mod is out of date
        /// </summary>
        private string _modName = "UnknownMod";

        //Water API Guide
        //Drag WaterModAPI.cs and Water.cs into your mod
        //Create a new WaterModAPI object in your mod's script, "WaterModAPI api = new WaterModAPI();"
        //Register the api at the start of your session with api.Register()
        //Unregister the api at the end of your session with api.Unregister()
        //Run api.UpdateRadius() inside of an update method

        /// <summary>
        /// Register with a mod name so version control can recognize what mod may be out of date
        /// </summary>
        public void Register(string modName)
        {
            _modName = modName;
            MyAPIGateway.Utilities.RegisterMessageHandler(ModHandlerID, ModHandler);
        }

        /// <summary>
        /// Unregister to prevent odd behavior after reloading your save/game
        /// </summary>
        public void Unregister()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(ModHandlerID, ModHandler);
        }

        /// <summary>
        /// Do not use, for interfacing with Water Mod
        /// </summary>
        private void ModHandler(object data)
        {
            if (data == null)
                return;

            if (!Registered)
            {
                Registered = true;
                OnRegisteredEvent?.Invoke();
            }

            var bytes = data as byte[];
            if (bytes != null)
            {
                Waters = MyAPIGateway.Utilities.SerializeFromBinary<List<Water>>(bytes);

                if (Waters == null)
                {
                    Waters = new List<Water>();
                    Log.Line($"Waters was null!");
                }
                else {

                    foreach (var w in Waters) {
                        if (w.PlanetId != 0)
                            w.Init(w.PlanetId);
                        else Log.Line($"planetId was 0");
                    }
                }

                int count = Waters.Count;
                RecievedData?.Invoke();

                if (count > Waters.Count)
                    WaterCreatedEvent?.Invoke();
                if (count < Waters.Count)
                    WaterRemovedEvent?.Invoke();
            }

            if (data is int && (int)data != ModAPIVersion)
            {
                MyLog.Default.WriteLine("Water API V" + ModAPIVersion + " for " + _modName + " is outdated, expected V" + (int)data);
                MyAPIGateway.Utilities.ShowMessage(_modName, "Water API V" + ModAPIVersion + " is outdated, expected V" + (int)data);
            }
        }

        /// <summary>
        /// Recalculates the CurrentRadius for all waters
        /// </summary>
        public void UpdateRadius()
        {
            foreach (var water in Waters)
            {
                water.WaveTimer++;
                water.CurrentRadius = (float)Math.Max(water.Radius + (Math.Sin((water.WaveTimer) * water.WaveSpeed) * water.WaveHeight), 0);
            }
        }
    }
}