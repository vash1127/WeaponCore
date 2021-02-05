using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Utils;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        public void Handler(object o)
        {
            try
            {
                var message = o as byte[];
                if (message == null) return;

                ContainerDefinition baseDefArray = null;
                try { baseDefArray = MyAPIGateway.Utilities.SerializeFromBinary<ContainerDefinition>(message); }
                catch (Exception e) {
                    // ignored
                }

                if (baseDefArray != null) {

                    AssemblePartDefinitions(baseDefArray.PartDefs);
                    AssembleArmorDefinitions(baseDefArray.ArmorDefs);
                }
                else {
                    var legacyArray = MyAPIGateway.Utilities.SerializeFromBinary<PartDefinition[]>(message);
                    if (legacyArray != null)
                        AssemblePartDefinitions(legacyArray);
                }
            }
            catch (Exception ex) { MyLog.Default.WriteLine($"Exception in Handler: {ex}"); }
        }

        public void AssemblePartDefinitions(PartDefinition[] partDefs)
        {
            var subTypes = new HashSet<string>();
            foreach (var wepDef in partDefs)
            {
                PartDefinitions.Add(wepDef);

                for (int i = 0; i < wepDef.Assignments.MountPoints.Length; i++)
                    subTypes.Add(wepDef.Assignments.MountPoints[i].SubtypeId);
            }
            var group = MyStringHash.GetOrCompute("Charging");

            foreach (var def in AllDefinitions)
            {
                if (subTypes.Contains(def.Id.SubtypeName))
                {
                    if (def is MyLargeTurretBaseDefinition)
                    {
                        var weaponDef = def as MyLargeTurretBaseDefinition;
                        weaponDef.ResourceSinkGroup = group;
                    }
                    else if (def is MyConveyorSorterDefinition)
                    {
                        var weaponDef = def as MyConveyorSorterDefinition;
                        weaponDef.ResourceSinkGroup = group;
                    }
                }
            }
        }

        public void AssembleArmorDefinitions(ArmorDefinition[] armorDefs)
        {
            foreach (var armorDef in armorDefs)
            {
                if (armorDef.Kind == ArmorDefinition.ArmorType.Heavy)
                {
                    var type = MyStringHash.GetOrCompute(armorDef.SubtypeId);
                    CustomArmorSubtypes.Add(type);
                    CustomHeavyArmorSubtypes.Add(type);
                }
                else if (armorDef.Kind == ArmorDefinition.ArmorType.Light)
                {
                    CustomArmorSubtypes.Add(MyStringHash.GetOrCompute(armorDef.SubtypeId));
                }
            }
        }
    }
}
