public class SegmentGenerationTests
{
    /*
     PURPOSE:
     Ensure every generated segment contains a valid main path.

     DESIGN REQUIREMENT:
     - The game is structured around a continuous forward path (railroad)
     - Side rooms branch off this path
     - The main path MUST always exist

     FAILURE MEANS:
     - Player progression breaks
     - World becomes non-traversable
    */
    [Fact]
    public void Segment_ShouldAlwaysContainMainPath()
    {
        var segment = builder.GetSegment(123, 50, 2);

        Assert.NotNull(segment.MainPath);
    }

    /*
     PURPOSE:
     Ensure full segment generation is deterministic.

     DESIGN RULE:
     - Entire segment (not just enemies) must be reproducible
     - Includes:
         - side rooms
         - enemy groups
         - boss presence

     FAILURE MEANS:
     - Multiplayer desync
     - Inconsistent world reconstruction
    */
    [Fact]
    public void SegmentGeneration_ShouldBeDeterministic()
    {
        var a = builder.GetSegment(123, 50, 2);
        var b = builder.GetSegment(123, 50, 2);

        Assert.Equal(a.Serialize(), b.Serialize());
    }
}