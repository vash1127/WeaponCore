using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Support;
using VRage.Game;
namespace CoreSystems
{
    public partial class Session
    {
        private void UpdateChargeWeapons() //Fully Inlined due to keen's mod profiler
        {
            foreach (var charger in ChargingParts) {

                charger.PowerUsed = 0;
                var ai = charger.Ai;
                var gridAvail = (ai.GridAvailablePower * 0.98);
                var availMinusDesired = gridAvail - charger.TotalDesired;
                var powerFree = availMinusDesired > 0;

                var group0Count = charger.ChargeGroup0.Count;
                var group0Budget = (float)(gridAvail * 0.5f) / group0Count;
                for (int i = group0Count - 1; i >= 0; i--)
                {
                    var part = charger.ChargeGroup0[i];
                    var comp = part.BaseComp;
                    if (comp.Ai == null || ai.TopEntity.MarkedForClose || ai.Concealed || !ai.HasPower || comp.CoreEntity.MarkedForClose || !comp.IsWorking || comp.Platform.State != CorePlatform.PlatformState.Ready) {

                        if (part.DrawingPower)
                            part.StopPowerDraw();

                        part.Loading = false;

                        charger.Remove(part, i);
                        continue;
                    }

                    var assignedPower = powerFree ? part.DesiredPower : group0Budget;

                    switch (part.BaseComp.Type)
                    {
                        case CoreComponent.CompType.Upgrade:
                            break;
                        case CoreComponent.CompType.Support:
                            break;
                        case CoreComponent.CompType.Phantom:
                            break;
                        case CoreComponent.CompType.Weapon:
                            if (WeaponCharged((Weapon)part, assignedPower)) 
                                charger.Remove(part, i);
                            break;
                    }
                }

                var group1Count = charger.ChargeGroup1.Count;
                var group1Budget = ((float)(gridAvail * 0.25f) / group1Count);
                for (int i = group1Count - 1; i >= 0; i--)
                {
                    var part = charger.ChargeGroup1[i];
                    var comp = part.BaseComp;
                    if (comp.Ai == null || ai.TopEntity.MarkedForClose || ai.Concealed || !ai.HasPower || comp.CoreEntity.MarkedForClose || !comp.IsWorking || comp.Platform.State != CorePlatform.PlatformState.Ready) {

                        if (part.DrawingPower)
                            part.StopPowerDraw();

                        part.Loading = false;

                        charger.Remove(part, i);
                        continue;
                    }
                    var assignedPower = powerFree ? part.DesiredPower : group1Budget;

                    switch (part.BaseComp.Type)
                    {
                        case CoreComponent.CompType.Upgrade:
                            break;
                        case CoreComponent.CompType.Support:
                            break;
                        case CoreComponent.CompType.Phantom:
                            break;
                        case CoreComponent.CompType.Weapon:
                            if (WeaponCharged((Weapon)part,assignedPower))
                                charger.Remove(part, i);
                            break;
                    }
                }

                var group2Count = charger.ChargeGroup2.Count;
                var group2Budget = ((float)(gridAvail * 0.25f) / group2Count);
                for (int i = charger.ChargeGroup2.Count - 1; i >= 0; i--)
                {
                    var part = charger.ChargeGroup2[i];
                    var comp = part.BaseComp;
                    if (comp.Ai == null || ai.TopEntity.MarkedForClose || ai.Concealed || !ai.HasPower || comp.CoreEntity.MarkedForClose || !comp.IsWorking || comp.Platform.State != CorePlatform.PlatformState.Ready) {

                        if (part.DrawingPower)
                            part.StopPowerDraw();

                        part.Loading = false;

                        charger.Remove(part, i);
                        continue;
                    }
                    var assignedPower = powerFree ? part.DesiredPower : group2Budget;

                    switch (part.BaseComp.Type)
                    {
                        case CoreComponent.CompType.Upgrade:
                            break;
                        case CoreComponent.CompType.Support:
                            break;
                        case CoreComponent.CompType.Phantom:
                            break;
                        case CoreComponent.CompType.Weapon:
                            if (WeaponCharged((Weapon)part, assignedPower))
                                charger.Remove(part, i);
                            break;
                    }

                }

            }
        }

        private bool WeaponCharged(Weapon w, float assignedPower)
        {
            if (IsServer && w.ChargeUntilTick <= Tick || IsClient && w.Reload.EndId > w.ClientEndId || !w.Loading) {

                if (w.Loading)
                    w.Reloaded();

                if (w.DrawingPower)
                    w.StopPowerDraw();

                return true;
            }

            if (!w.DrawingPower) {

                if (!w.BaseComp.UnlimitedPower)
                    w.DrawPower(assignedPower);

                w.ChargeDelayTicks = 0;
            }

            if (Tick60 && w.DrawingPower) {

                if ((w.ProtoWeaponAmmo.CurrentCharge + w.AssignedPower) < w.MaxCharge) 
                    w.ProtoWeaponAmmo.CurrentCharge += w.AssignedPower;
                else 
                    w.ProtoWeaponAmmo.CurrentCharge = w.MaxCharge;
            }
            return false;
        }
    }
}
