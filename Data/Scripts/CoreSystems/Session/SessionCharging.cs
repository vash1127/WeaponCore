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
                var gridAvail = ((ai.GridAvailablePower * 0.98f) + ai.GridAssignedPower);
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

                var group0Budget = group0Count > 0 ? halfAvail / group0Count : float.MaxValue;
                var group1Budget = group1Count > 0 ? quarterPlusG0Remaining / group1Count : float.MaxValue;
                var group2Budget = group2Count > 0 ? quarterPlusAllRemaining / group2Count : float.MaxValue;

                if (Tick180)
                    Log.Line($"[charging] [fullPower:{powerFree} - [avail:{gridAvail} - desired:{charger.TotalDesired}]]");

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
            ChargingParts.ApplyRemovals();
        }

        private bool WeaponCharged(Ai ai, Weapon w, float assignedPower)
        {
            var comp = w.Comp;

            if (!w.BaseComp.UnlimitedPower) {

                if (!w.Charging)
                    w.DrawPower(assignedPower);
                else if (w.NewPowerNeeds)
                    w.AdjustPower(assignedPower);
            }

            w.ProtoWeaponAmmo.CurrentCharge += w.AssignedPower;
            if (Tick180)
                Log.Line($"[{w.System.PartName}] [current:{w.ProtoWeaponAmmo.CurrentCharge} >= target:{w.MaxCharge}]]");

            var complete = IsServer && w.ProtoWeaponAmmo.CurrentCharge >= w.MaxCharge || IsClient && w.Reload.EndId > w.ClientEndId || w.ExitCharger;
            var weaponFailure = !ai.HasPower || !comp.IsWorking;
            var invalidStates = ai != comp.Ai || comp.Ai.MarkedForClose || comp.Ai.TopEntity.MarkedForClose || comp.Ai.Concealed || comp.CoreEntity.MarkedForClose || comp.Platform.State != CorePlatform.PlatformState.Ready;
            
            if (complete || weaponFailure || invalidStates) {

                w.StopPowerDraw(weaponFailure || invalidStates);
                if (w.Loading)
                    w.Reloaded();

                return true;
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
