public class TargetDifficultyTests
{
    /*
     PURPOSE:
     Enforce "target difficulty" system.

     DESIGN RULE:
     - Each depth has a target difficulty
     - Generated encounters must be close to it

     THIS IS THE CORE OF YOUR DESIGN:
     - Not random difficulty
     - Not stat scaling
     - Controlled encounter selection

     FAILURE MEANS:
     - Random spikes
     - Unpredictable gameplay difficulty
    */
    [Fact]
    public void Group_ShouldBeNearTargetDifficulty()
    {
        int depth = 60;
        int players = 2;

        var group = builder.GetGroup(123, depth, players);
        float target = DifficultyCurve.GetTarget(depth, players);

        float diff = Math.Abs(group.Difficulty - target);

        Assert.True(
            diff <= 10,
            $"Difficulty out of range.\nTarget={target}\nActual={group.Difficulty}\nDiff={diff}"
        );
    }
}