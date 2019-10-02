using Sandbox.Game.EntityComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeaponCore.Support
{
    class CameraController : MyEntityRespawnComponentBase
    {

        public override string ComponentTypeDebugString
        {
            get
            {
                return "CameraController";
            }
        }
    }
}
