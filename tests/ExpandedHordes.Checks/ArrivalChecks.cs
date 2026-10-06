using System;
using ExpandedHordes;

internal static class ArrivalChecks
{
    internal static void Run(Action<bool, string> check)
    {
        check(ArrivalRules.WaitSeconds(100) == 0.1f, "100% retains the native wait");
        check(ArrivalRules.WaitSeconds(200) == 0.05f, "200% halves the native wait");
        check(ArrivalRules.WaitSeconds(400) == 0.025f, "400% quarters the native wait");
        check(ArrivalRules.WaitSeconds(int.MinValue) == 0.1f && ArrivalRules.WaitSeconds(int.MaxValue) == 0.025f,
            "Spawn rate clamps to 100..400 without overflow");
        check(ArrivalRules.Directions(0) == 1 && ArrivalRules.Directions(5) == 4, "Direction count clamps to 1..4");
        check(ArrivalRules.SectorAngle(1, 3) == 120f && ArrivalRules.SectorAngle(3, 4) == 270f,
            "Sectors divide the full circle evenly");
        ArrivalRules.Rotate(50, 0, 1, 4, out float quarterX, out float quarterZ);
        check(Math.Abs(quarterX) < 0.0001f && Math.Abs(quarterZ + 50) < 0.0001f, "Quarter turn follows Unity's vertical axis");
        for (int directions = 1; directions <= 4; directions++)
            for (int sector = 0; sector < directions; sector++)
            {
                ArrivalRules.Rotate(30, 40, sector, directions, out float x, out float z);
                check(Math.Abs(x * x + z * z - 2500) < 0.001f, "Rotation preserves the native horizontal spawn distance");
                // Rotate a point at the edge of the original local radius too.
                ArrivalRules.Rotate(36, 48, sector, directions, out float edgeX, out float edgeZ);
                check(Math.Abs((edgeX - x) * (edgeX - x) + (edgeZ - z) * (edgeZ - z) - 100) < 0.001f,
                    "Rotation preserves the local placement radius");
            }
        check(ArrivalRules.PlacementFailed(-10000, 100, 100), "Native sentinel counts as a placement failure");
        check(ArrivalRules.PlacementFailed(10, 39.99f, 0) && !ArrivalRules.PlacementFailed(10, 40, 0),
            "Placement follows the native strict 40 metre horizontal retry boundary");
        var lanes = new ArrivalSectors(4);
        for (int ordinal = 0; ordinal < 12; ordinal++)
        {
            int sector = lanes.Select(0);
            check(sector == ordinal % 4, "Successful placements rotate round robin");
            lanes.Record(sector, true, 0);
        }
        lanes.Record(0, false, 1); lanes.Record(0, false, 1);
        check(lanes.Select(1) == 0, "Two failures retain the current sector");
        lanes.Record(0, false, 1);
        check(lanes.Select(1) == 1 && lanes.Select(10.99f) == 1, "Three failures skip the sector for ten seconds");
        check(lanes.Select(11) == 0, "Cooled sector becomes eligible exactly at expiry");
        lanes.Record(0, true, 11);
        lanes.Record(1, false, 11); lanes.Record(1, false, 11); lanes.Record(1, true, 11);
        lanes.Record(2, true, 11); lanes.Record(3, true, 11); lanes.Record(0, true, 11);
        lanes.Record(1, false, 11);
        check(lanes.Select(11) == 1, "A success resets that sector's consecutive failure count");
        var blocked = new ArrivalSectors(4);
        for (int sector = 0; sector < 4; sector++)
        {
            check(blocked.Select(2) == sector, "Blocked sectors are skipped without extra placement calls");
            for (int failure = 0; failure < 3; failure++) blocked.Record(sector, false, 2);
        }
        check(blocked.Select(2) == 0 && blocked.Select(11.99f) == 0, "All cooling sectors fall back to native sector zero");
        check(blocked.Select(12) == 0 && new ArrivalSectors(4).Select(2) == 0, "Cooldown expiry and a new horde start at native sector zero");
    }
}
