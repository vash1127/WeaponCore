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
                var gridAvail = (ai.GridAvailablePower * 0.98f);
                var availMinusDesired = gridAvail - charger.TotalDesired;
                var powerFree = availMinusDesired > 0;

                var halfAvail = gridAvail * 0.5f;
                var quarterAvail = gridAvail * 0.25f;
                var g0LeftOvers = halfAvail - charger.GroupRequested0;
                var allLeftOvers = gridAvail - (charger.GroupRequested0 + charger.GroupRequested1);
                var quarterPlusG0Remaining = g0LeftOvers > 0 ? quarterAvail + g0LeftOvers : quarterAvail;
                var quarterPlusAllRemaining = allLeftOvers > 0 ? quarterAvail + allLeftOvers : quarterAvail;

                var group0Count = charger.ChargeGroup0.Count;
                var group1Count = charger.ChargeGroup1.Count;
                var group2Count = charger.ChargeGroup2.Count;

                var group0Budget = halfAvail / group0Count;
                var group1Budget = quarterPlusG0Remaining / group1Count;
                var group2Budget = quarterPlusAllRemaining / group2Count;

                Log.Line($"{group0Budget} - {group1Budget} - {group2Budget}");
                for (int i = group0Count - 1; i >= 0; i--)
                {
                    var part = charger.ChargeGroup0[i];

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
                            if (WeaponCharged(ai, (Weapon)part, assignedPower)) 
                                charger.Remove(part, i);
                            break;
                    }
                }


                for (int i = group1Count - 1; i >= 0; i--)
                {
                    var part = charger.ChargeGroup1[i];

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
                            if (WeaponCharged(ai, (Weapon)part,assignedPower))
                                charger.Remove(part, i);
                            break;
                    }
                }


                for (int i = group2Count - 1; i >= 0; i--)
                {
                    var part = charger.ChargeGroup2[i];
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
                            if (WeaponCharged(ai, (Weapon)part, assignedPower))
                                charger.Remove(part, i);
                            break;
                    }

                }
            }
        }

        private bool WeaponCharged(Ai ai, Weapon w, float assignedPower)
        {
            var comp = w.Comp;
            var complete = IsServer && w.ChargeUntilTick <= Tick || IsClient && w.Reload.EndId > w.ClientEndId || !w.Loading || w.ExitCharger;
            var weaponFailure = !ai.HasPower || !comp.IsWorking;
            var invalidStates = ai != comp.Ai || comp.Ai.MarkedForClose || comp.Ai.TopEntity.MarkedForClose || comp.Ai.Concealed || comp.CoreEntity.MarkedForClose || comp.Platform.State != CorePlatform.PlatformState.Ready;
            
            if (complete || weaponFailure || invalidStates) {

                w.StopPowerDraw();
                if (w.Loading)
                    w.Reloaded();

                return true;
            }

            if (!w.BaseComp.UnlimitedPower) {

                if (!w.Charging)
                    w.DrawPower(assignedPower);
                else if (w.NewPowerNeeds)
                    w.AdjustPower(assignedPower);
            }

            if (Tick60) {

                if (w.EstimatedCharge + w.AssignedPower < w.MaxCharge)
                    w.EstimatedCharge += w.AssignedPower;
                else
                    w.EstimatedCharge = w.MaxCharge;
            }
            return false;
        }
    }
}
