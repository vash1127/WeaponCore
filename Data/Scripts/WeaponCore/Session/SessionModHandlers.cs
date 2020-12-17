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
                var slaveDefArray = MyAPIGateway.Utilities.SerializeFromBinary<WeaponDefinition[]>(message);
                if (slaveDefArray != null)
                {
                    var subTypes = new HashSet<string>();
                    foreach (var wepDef in slaveDefArray)
                    {
                        WeaponDefinitions.Add(wepDef);

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
            }
            catch (Exception ex) { MyLog.Default.WriteLine($"Exception in Handler: {ex}"); }
        }
        public void ArmorHandler(object o)
        {
            try
            {
                var message = o as byte[];
                if (message == null) return;
                var armorCompatDefArray = MyAPIGateway.Utilities.SerializeFromBinary<ArmorCompatibilityDef[]>(message);
                if (armorCompatDefArray != null)
                {
                    foreach (var armorDef in armorCompatDefArray)
                    {
                        if (armorDef.Kind == ArmorCompatibilityDef.ArmorType.Heavy)
                        {
                            CustomArmorSubtypes.Add(MyStringHash.GetOrCompute(armorDef.SubtypeId));
                            CustomHeavyArmorSubtypes.Add(MyStringHash.GetOrCompute(armorDef.SubtypeId));
                        }
                        else if (armorDef.Kind == ArmorCompatibilityDef.ArmorType.Light)
                        {
                            CustomArmorSubtypes.Add(MyStringHash.GetOrCompute(armorDef.SubtypeId));
                        }
                    }
                }
            }
            catch (Exception ex) { MyLog.Default.WriteLine($"Exception in ArmorHandler: {ex}"); }
        }
    }
}
