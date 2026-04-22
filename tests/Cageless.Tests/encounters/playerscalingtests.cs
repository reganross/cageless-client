public class PlayerScalingTests
{
    /*
     PURPOSE:
     Ensure encounters scale with number of players.

     DESIGN RULE:
     - More players = more difficult encounters
     - This should be achieved via:
         - Larger groups
         - More complex compositions
     - NOT via stat inflation

     FAILURE MEANS:
     - Multiplayer becomes too easy OR too chaotic
    */
    [Fact]
    public void Difficulty_ShouldIncreaseWithPlayerCount()
    {
        var solo = builder.GetGroup(123, 50, 1);
        var group = builder.GetGroup(123, 50, 4);

        Assert.True(
            group.Difficulty > solo.Difficulty,
            $"Expected higher difficulty for more players.\nSolo={solo.Difficulty}, Group={group.Difficulty}"
        );
    }
}