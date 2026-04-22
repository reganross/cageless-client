public class BossRulesTests
{
    /*
     PURPOSE:
     Control boss encounter frequency.

     DESIGN RULE:
     - Bosses spawn at fixed depth intervals
     - Must NOT appear randomly

     WHY:
     - Bosses are pacing anchors
     - Random bosses feel unfair

     FAILURE MEANS:
     - Players encounter bosses too frequently or unpredictably
    */
    [Fact]
    public void Boss_ShouldOnlySpawnAtDefinedIntervals()
    {
        int interval = 10;

        for (int depth = 1; depth < 100; depth++)
        {
            var segment = builder.GetSegment(123, depth, 1);

            if (segment.HasBoss)
            {
                Assert.True(
                    depth % interval == 0,
                    $"Boss spawned incorrectly at depth {depth}"
                );
            }
        }
    }
}