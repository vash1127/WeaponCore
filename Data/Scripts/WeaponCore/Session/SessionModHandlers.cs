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

                    PickDef(baseDefArray);
                }
                else {
                    var legacyArray = MyAPIGateway.Utilities.SerializeFromBinary<WeaponDefinition[]>(message);
                    if (legacyArray != null)
                        AssemblePartDefinitions(legacyArray);
                }
            }
            catch (Exception ex) { MyLog.Default.WriteLine($"Exception in Handler: {ex}"); }
        }
        private void PickDef(ContainerDefinition baseDefArray)
        {
            if (baseDefArray.WeaponDefs != null)
                AssemblePartDefinitions(baseDefArray.WeaponDefs);
            else if (baseDefArray.SupportDefs != null)
                AssemblePartDefinitions(baseDefArray.SupportDefs);
            else if (baseDefArray.UpgradeDefs != null)
                AssemblePartDefinitions(baseDefArray.UpgradeDefs);
            else if (baseDefArray.PhantomDefs != null)
                AssemblePartDefinitions(baseDefArray.PhantomDefs);
            else if (baseDefArray.ArmorDefs != null)
                AssembleArmorDefinitions(baseDefArray.ArmorDefs);
        }

        public void AssemblePartDefinitions(WeaponDefinition[] partDefs)
        {
            var subTypes = new HashSet<string>();
            foreach (var wepDef in partDefs)
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

        public void AssemblePartDefinitions(UpgradeDefinition[] partDefs)
        {
            var subTypes = new HashSet<string>();
            foreach (var upgradeDef in partDefs)
            {
                UpgradeDefinitions.Add(upgradeDef);

                for (int i = 0; i < upgradeDef.Assignments.MountPoints.Length; i++)
                    subTypes.Add(upgradeDef.Assignments.MountPoints[i].SubtypeId);
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

        public void AssemblePartDefinitions(SupportDefinition[] partDefs)
        {
            var subTypes = new HashSet<string>();
            foreach (var supportDef in partDefs)
            {
                SupportDefinitions.Add(supportDef);

                for (int i = 0; i < supportDef.Assignments.MountPoints.Length; i++)
                    subTypes.Add(supportDef.Assignments.MountPoints[i].SubtypeId);
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

        public void AssemblePartDefinitions(PhantomDefinition[] partDefs)
        {
            foreach (var phantomDef in partDefs)
            {
                PhantomDefinitions.Add(phantomDef);
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
