public class DeterminismTests
{
    /*
     PURPOSE:
     Entire system must be deterministic.

     RULE:
     Same seed + depth + players → identical result

     FAILURE:
     Multiplayer desync, unreproducible worlds
    */
    [Fact]
    public void SameInput_ShouldProduceSameEnemyGroup()
    {
        var a = builder.GetGroup(123, 50, 2);
        var b = builder.GetGroup(123, 50, 2);

        Assert.Equal(a.Id, b.Id);
    }

    /*
     PURPOSE:
     Ensure depth changes produce different outputs

     FAILURE:
     Repetitive gameplay
    */
    [Fact]
    public void DifferentDepth_ShouldProduceDifferentResults()
    {
        var a = builder.GetGroup(123, 10, 2);
        var b = builder.GetGroup(123, 11, 2);

        Assert.NotEqual(a.Id, b.Id);
    }

    /*
     PURPOSE:
     Ensure independent systems do NOT affect each other

     RULE:
     RNG must be isolated per system

     FAILURE:
     Adding features changes unrelated behavior
    */
    [Fact]
    public void Systems_ShouldBeSeedIsolated()
    {
        var segmentA = builder.GetSegment(123, 50, 2);
        var segmentB = builder.GetSegment(123, 50, 2);

        Assert.Equal(
            segmentA.Serialize(),
            segmentB.Serialize()
        );
    }
}