using CoreSystems.Platform;
using CoreSystems.Support;
using VRageMath;

namespace CoreSystems
{
    public partial class Session
    {
        private void UpdateChargeWeapons() //Fully Inlined due to keen's mod profiler
        {
            foreach (var charger in ChargingParts) {

                var ai = charger.Ai;
                var gridAvail = ((ai.GridAvailablePower + ai.GridAssignedPower) * 0.94f);
                var availMinusDesired = gridAvail - charger.TotalDesired;
                var powerFree = availMinusDesired > 0;
                var rebalance = !powerFree && charger.Rebalance;
                charger.Rebalance = false;

                var group0Count = charger.ChargeGroup0.Count;
                var group1Count = charger.ChargeGroup1.Count;
                var group2Count = charger.ChargeGroup2.Count;
                
                var g0Power = gridAvail * charger.G0Power[charger.State];
                var g1Power = gridAvail * charger.G1Power[charger.State];
                var g2Power = gridAvail * charger.G2Power[charger.State];

                var g0LeftOvers = g0Power - charger.GroupRequested0;
                var allLeftOvers = gridAvail - (charger.GroupRequested0 + charger.GroupRequested1);
                var g1PlusG0Remaining = g0LeftOvers > 0 ? g1Power + g0LeftOvers : g1Power;
                var g2AllRemaining = allLeftOvers > 0 ? g2Power + allLeftOvers : g2Power;

                var group0Budget = group0Count > 0 ? g0Power / group0Count : float.MaxValue;
                var group1Budget = group1Count > 0 ? g1PlusG0Remaining / group1Count : float.MaxValue;
                var group2Budget = group2Count > 0 ? g2AllRemaining / group2Count : float.MaxValue;

                //if (Tick180)
                    //Log.Line($"[charging] [fullPower:{powerFree} - [avail:{gridAvail}({g0Power}) - desired:{charger.TotalDesired}]] - g0:{group0Budget}({group0Count}) - g1:{group1Budget}({group1Count}) - g2:{group2Budget}({group2Count})");

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
                            if (WeaponCharged(ai, (Weapon)part, assignedPower, rebalance)) 
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
                            if (WeaponCharged(ai, (Weapon)part,assignedPower, rebalance))
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
                            if (WeaponCharged(ai, (Weapon)part, assignedPower, rebalance))
                                charger.Remove(part, i);
                            break;
                    }

                }
            }
            ChargingParts.ApplyRemovals();
        }

        private bool WeaponCharged(Ai ai, Weapon w, float assignedPower, bool rebalance = false)
        {
            var comp = w.Comp;

            if (!w.BaseComp.UnlimitedPower) {

                if (!w.Charging)
                    w.DrawPower(assignedPower);
                else if (w.NewPowerNeeds || rebalance)
                    w.AdjustPower(assignedPower);
            }


            w.ProtoWeaponAmmo.CurrentCharge = MathHelper.Clamp(w.ProtoWeaponAmmo.CurrentCharge + w.AssignedPower, 0, w.MaxCharge);

            //if (Tick180)
                //Log.Line($"[{w.System.PartName}] [current:{w.ProtoWeaponAmmo.CurrentCharge} >= target:{w.MaxCharge}]] - CurrentAmmo:{w.ProtoWeaponAmmo.CurrentAmmo} == MaxAmmo:{w.ActiveAmmoDef.AmmoDef.Const.MagazineSize} - ReloadTime:{w.System.ReloadTime} - StayCharged:{w.StayCharged}");

            var complete = IsServer && w.ProtoWeaponAmmo.CurrentCharge >= w.MaxCharge || IsClient && w.Reload.EndId > w.ClientEndId || w.ExitCharger;
            var weaponFailure = !ai.HasPower || !comp.IsWorking;
            var invalidStates = ai != comp.Ai || comp.Ai.MarkedForClose || comp.Ai.TopEntity.MarkedForClose || comp.Ai.Concealed || comp.CoreEntity.MarkedForClose || comp.Platform.State != CorePlatform.PlatformState.Ready;
            
            if (complete || weaponFailure || invalidStates) {

                var fullyCharged = w.ProtoWeaponAmmo.CurrentAmmo == w.ActiveAmmoDef.AmmoDef.Const.MagazineSize;
                if (!fullyCharged && w.Loading)
                    w.Reloaded();

                if (!complete || fullyCharged) {
                    w.StopPowerDraw(weaponFailure || invalidStates);
                    return true;
                }
                w.Loading = true;
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
