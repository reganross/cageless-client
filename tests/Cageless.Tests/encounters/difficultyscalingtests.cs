public class DifficultyScalingTests
{
    /*
     PURPOSE:
     Ensure that difficulty increases as the player progresses deeper.

     DESIGN REQUIREMENT:
     - Difficulty must scale with depth
     - Scaling should feel gradual, not spiky

     IMPORTANT:
     - We are NOT scaling enemy stats (health/damage)
     - We ARE scaling group composition complexity

     FAILURE MEANS:
     - Late game becomes trivial OR early game becomes too hard
    */
    [Fact]
    public void Difficulty_ShouldIncreaseWithDepth()
    {
        var early = builder.GetGroup(123, 10, 1);
        var late  = builder.GetGroup(123, 100, 1);

        Assert.True(
            late.Difficulty > early.Difficulty,
            $"Expected increasing difficulty.\nEarly={early.Difficulty}, Late={late.Difficulty}"
        );
    }

    /*
     PURPOSE:
     Ensure difficulty stays within a controlled band around a target.

     DESIGN RULE:
     - Each depth has a "target difficulty"
     - Generated encounters must fall within tolerance of that target

     WHY:
     - Prevents random difficulty spikes
     - Prevents encounters that are too easy

     IMPLEMENTATION NOTE:
     - Use weighted selection toward target difficulty
     - Apply a tolerance window

     FAILURE MEANS:
     - Game feels unfair or inconsistent
    */
    [Fact]
    public void Difficulty_ShouldStayWithinTargetRange()
    {
        int depth = 50;
        int players = 2;

        var group = builder.GetGroup(123, depth, players);
        float target = DifficultyCurve.GetTarget(depth, players);

        Assert.InRange(
            group.Difficulty,
            target - 10,
            target + 10
        );
    }

    [Fact]
    public void Difficulty_ShouldNotSpikeBetweenDepths()
    {
        int last = 0;

        for (int depth = 0; depth < 100; depth++)
        {
            var group = builder.GetGroup(123, depth, 1);

            Assert.True(
                group.Difficulty <= last + 20,
                $"Spike at depth {depth}"
            );

            last = group.Difficulty;
        }
    }
}