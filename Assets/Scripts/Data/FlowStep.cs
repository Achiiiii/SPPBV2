namespace SPPB.Data
{
    /// <summary>
    /// All steps in the SPPB test flow (21 steps total)
    /// </summary>
    public enum FlowStep
    {
        // Home Page
        Home = 0,

        // Test Introduction
        TestIntro = 1,

        // APose Calibration
        APoseCalibration_Teaching = 2, // Calibration - Teaching
        APoseCalibration = 3,          // Calibration - Execution

        // Balance Test
        BalanceIntro = 4,                  // Balance Test Introduction
        BalanceSideBySide_Teaching = 5,    // Side-by-Side Stance - Teaching
        BalanceSideBySide_Test = 6,        // Side-by-Side Stance - Test
        BalanceSemiTandem_Teaching = 7,    // Semi-Tandem Stance - Teaching
        BalanceSemiTandem_Test = 8,        // Semi-Tandem Stance - Test
        BalanceTandem_Teaching = 9,        // Tandem Stance - Teaching
        BalanceTandem_Test = 10,           // Tandem Stance - Test

        // Sit-Stand Test
        SitStandIntro = 11,            // Sit-Stand Test Introduction
        SitStand_Teaching = 12,        // Sit-Stand Test - Teaching
        SitStand_Test = 13,            // Sit-Stand Test - Test

        // Walking Test
        WalkIntro = 14,                // Walking Test Introduction
        Walk_Teaching = 15,            // Walking Test - Teaching
        Walk_Test = 16,                // Walking Test - Test

        // Score Page (single page displaying all results)
        Score = 17                     // Score Page - Displays all test results and total score
    }
}
