public class EncounterQualityTests
{
    /*
     PURPOSE:
     Prevent repetitive encounters

     RULE:
     Same group should not appear too frequently

     FAILURE:
     Player notices repetition quickly
    */
    [Fact]
    public void SameGroup_ShouldNotRepeatTooFrequently()
    {
        var results = new List<string>();

        for (int depth = 0; depth < 30; depth++)
        {
            results.Add(builder.GetGroup(123, depth, 1).Id);
        }

        int maxRepeats = results.GroupBy(x => x).Max(g => g.Count());

        Assert.True(
            maxRepeats < 6,
            $"Too many repeats: {maxRepeats}"
        );
    }

    /*
     PURPOSE:
     Ensure sufficient variation

     FAILURE:
     Procedural system feels fake
    */
    [Fact]
    public void Results_ShouldVaryAcrossDepthRange()
    {
        var results = new List<string>();

        for (int i = 0; i < 20; i++)
        {
            results.Add(builder.GetGroup(123, i, 1).Id);
        }

        int unique = results.Distinct().Count();

        Assert.True(
            unique > 5,
            $"Low variation: {unique}"
        );
    }

    /*
     PURPOSE:
     Prevent overcrowded encounters

     FAILURE:
     Performance issues, unfair fights
    */
    [Fact]
    public void EnemyCount_ShouldStayWithinLimits()
    {
        var segment = builder.GetSegment(123, 50, 2);

        int total = segment.Encounters.Sum(e => e.EnemyCount);

        Assert.True(
            total <= 20,
            $"Too many enemies: {total}"
        );
    }
}