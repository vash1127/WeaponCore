using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.ObjectBuilders;
using WeaponCore.Support;

namespace WeaponCore.Data.Scripts.WeaponCore.Support
{
    class CustomController : MyCockpit
    {
        public CustomController() : base() {
            
        }

        public override void Init(MyObjectBuilder_CubeBlock objectBuilder, MyCubeGrid cubeGrid)
        {
            base.Init(objectBuilder, cubeGrid);
        }
        /*
        public new bool CanSwitchToWeapon(MyDefinitionId? weapon)
            {
                Log.Line($"Made It");
                if (weapon == null)
                {
                    return true;
                }
                MyObjectBuilderType typeId = weapon.Value.TypeId;
                return typeId == typeof(MyObjectBuilder_Drill) || typeId == typeof(MyObjectBuilder_SmallMissileLauncher) || typeId == typeof(MyObjectBuilder_SmallGatlingGun) || typeId == typeof(MyObjectBuilder_ShipGrinder) || typeId == typeof(MyObjectBuilder_ShipWelder) || typeId == typeof(MyObjectBuilder_SmallMissileLauncherReload) ||Session.Instance.WeaponPlatforms.ContainsKey(weapon.Value.SubtypeId);
            }
        */

    }
}
