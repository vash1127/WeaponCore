using ProtoBuf;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.Models;
using VRageMath;
using WeaponCore.Support;

namespace Jakaria
{
    [ProtoContract(UseProtoMembersOnly = true)]
    public class Water
    {
        /// <summary>the entity ID of a planet</summary>
        [ProtoMember(5)]
        public long PlanetId;

        /// <summary>the average radius of the water</summary>
        [ProtoMember(10)]
        public float Radius;
        /// <summary>the current radius of the water</summary>
        [ProtoMember(11)]
        public float CurrentRadius;

        /// <summary>the maximum height of waves</summary>
        [ProtoMember(15)]
        public float WaveHeight = 1f;
        /// <summary>how fast a wave will oscillate</summary>
        [ProtoMember(16)]
        public float WaveSpeed = 0.004f;
        /// <summary>timer value for syncing waves between clients</summary>
        [ProtoMember(17)]
        public double WaveTimer = 0;

        /// <summary>center position of the water</summary>
        [ProtoMember(20)]
        public Vector3 Position;

        //Physics properties

        /// <summary>Viscosity of the water</summary>
        [ProtoMember(25)]
        public float Viscosity = 0.1f;

        /// <summary>Buoyancy multiplier of the water</summary>
        [ProtoMember(26)]
        public float Buoyancy = 1f;

        /// <summary>Whether or not the water can support fish</summary>
        [ProtoMember(30)]
        public bool EnableFish = true;

        /// <summary>Whether or not the water can support seagulls</summary>
        [ProtoMember(31)]
        public bool EnableSeagulls = true;

        /// <summary>All entites currently under the water</summary>
        [XmlIgnore, ProtoIgnore]
        public List<MyEntity> UnderWaterEntities = new List<MyEntity>();

        /// <summary>The planet entity</summary>
        [XmlIgnore, ProtoIgnore]
        public MyPlanet Planet;

        /// <summary>Provide a planet entity and it will set everything up for you</summary>
        public void Init(long planetId, float radiusMultiplier = 1.02f)
        {
            PlanetId = planetId;
            if (MyEntities.TryGetEntityById(planetId, out Planet, true))
            {
                Position = Planet.PositionComp.GetPosition();
                Radius = Planet.MinimumRadius * radiusMultiplier;
                CurrentRadius = Radius;

                EnableFish = (Planet.Generator.Atmosphere?.Breathable == true) && (Planet.Generator.DefaultSurfaceTemperature == VRage.Game.MyTemperatureLevel.Cozy);
                EnableSeagulls = EnableFish;
            }
            else Log.Line($"couldn't find planet");

        }

        /// <summary>Returns the closest point to water without regard to voxels</summary>
        public Vector3D GetClosestSurfacePoint(Vector3D position, float altitudeOffset = 0)
        {
            return this.Position + ((Vector3.Normalize(position - this.Position) * (this.CurrentRadius + altitudeOffset)));
        }

        /// <summary>Returns true if the given position is underwater</summary>
        public bool IsUnderwater(Vector3D position, float altitudeOffset = 0)
        {
            return Vector3D.Distance(this.Position, position) - (this.CurrentRadius + altitudeOffset) <= 0;
        }

        ///<summary>Returns true if the given position is underwater without a square root function</summary>
        public bool IsUnderwaterSquared(Vector3D position, float altitudeOffset = 0)
        {
            return Vector3D.DistanceSquared(this.Position, position) - (this.CurrentRadius + altitudeOffset) <= 0;
        }

        /// <summary>Overwater = 0, ExitsWater = 1, EntersWater = 2, Underwater = 3</summary>
        public int Intersects(Vector3D from, Vector3D to)
        {
            if (IsUnderwater(from))
            {
                if (IsUnderwater(to))
                    return 3; //Underwater
                else
                    return 1; //ExitsWater
            }
            else
            {
                if (IsUnderwater(to))
                    return 2; //EntersWater
                else
                    return 0; //Overwater
            }
        }

        /// <summary>Overwater = 0, ExitsWater = 1, EntersWater = 2, Underwater = 3</summary>
        public int Intersects(LineD line)
        {
            if (IsUnderwater(line.From))
            {
                if (IsUnderwater(line.To))
                    return 3; //Underwater
                else
                    return 1; //ExitsWater
            }
            else
            {
                if (IsUnderwater(line.To))
                    return 2; //EntersWater
                else
                    return 0; //Overwater
            }
        }

        /// <summary>Returns the depth of water a position is at, negative numbers are underwater</summary>
        public float GetDepth(Vector3 position)
        {
            return Vector3.Distance(this.Position, position) - this.CurrentRadius;
        }

        /// <summary>Returns the depth of water a position is at without a square root function, negative numbers are underwater</summary>
        public float GetDepthSquared(Vector3 position)
        {
            return Vector3.DistanceSquared(this.Position, position) - this.CurrentRadius;
        }

        /// <summary>Returns the depth of water a position is at using sea level, negative numbers are underwater</summary>
        public float GetDepthSimple(Vector3 position)
        {
            return Vector3.Distance(this.Position, position) - this.Radius;
        }

        /// <summary>Returns the depth of water a position is at using sea level without a square root function, negative numbers are underwater</summary>
        public float GetDepthSimpleSquared(Vector3 position)
        {
            return Vector3.DistanceSquared(this.Position, position) - this.Radius;
        }



        /// <summary>Returns the up direction at a position</summary>
        public Vector3 GetUpDirection(Vector3 position)
        {
            return Vector3.Normalize(position - this.Position);
        }
    }
}
