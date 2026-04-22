public class VariationTests
{
    /*
     PURPOSE:
     Ensure the system produces variation across depths.

     DESIGN RULE:
     - World must not feel repetitive
     - Adjacent depths should usually differ

     NOTE:
     This is not a strict guarantee per step,
     but over a range we expect diversity.

     FAILURE MEANS:
     - Player sees repeating patterns
     - Procedural system feels fake
    */
    [Fact]
    public void Results_ShouldVaryAcrossDepthRange()
    {
        var results = new List<string>();

        for (int depth = 0; depth < 20; depth++)
        {
            var group = builder.GetGroup(123, depth, 1);
            results.Add(group.Id);
        }

        int uniqueCount = results.Distinct().Count();

        Assert.True(
            uniqueCount > 5,
            $"Insufficient variation.\nUnique={uniqueCount}\nResults={string.Join(",", results)}"
        );
    }
}