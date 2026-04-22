public class RepetitionTests
{
    /*
     PURPOSE:
     Prevent excessive repetition of the same encounter.

     DESIGN RULE:
     - Same enemy group should not appear too frequently
     - System should bias away from recently used groups

     FAILURE MEANS:
     - Gameplay feels repetitive
     - Player notices patterns quickly
    */
    [Fact]
    public void SameGroup_ShouldNotRepeatTooFrequently()
    {
        var results = new List<string>();

        for (int depth = 0; depth < 30; depth++)
        {
            results.Add(builder.GetGroup(123, depth, 1).Id);
        }

        int maxRepeats = results
            .GroupBy(x => x)
            .Max(g => g.Count());

        Assert.True(
            maxRepeats < 6,
            $"Repetition too high.\nMaxRepeats={maxRepeats}\nResults={string.Join(",", results)}"
        );
    }
}