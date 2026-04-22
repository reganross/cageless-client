public class ConstraintTests
{
    /*
     PURPOSE:
     Ensure invalid encounter combinations never occur.

     DESIGN RULES:
     - Certain tags must not appear together
     - Certain combinations break gameplay

     EXAMPLES:
     - Too many elites
     - Conflicting mechanics

     FAILURE MEANS:
     - Unfair or broken encounters
    */
    [Fact]
    public void InvalidTagCombinations_ShouldNeverAppear()
    {
        var segment = builder.GetSegment(123, 50, 2);

        var allTags = segment.Encounters.SelectMany(e => e.Tags);

        bool hasInvalidCombo =
            allTags.Contains("elite") && allTags.Count(t => t == "elite") > 1;

        Assert.False(
            hasInvalidCombo,
            $"Invalid tag combination detected.\nTags={string.Join(",", allTags)}"
        );
    }
}