public class SideRoomTests
{
    /*
     PURPOSE:
     Ensure side room generation stays within defined limits.

     DESIGN RULE:
     - Each segment may have 0..N side rooms
     - Must NOT exceed max allowed

     WHY:
     - Prevents overcrowding
     - Keeps performance stable
     - Maintains pacing

     FAILURE MEANS:
     - Visual clutter
     - Performance degradation
    */
    [Fact]
    public void SideRooms_ShouldNotExceedLimit()
    {
        var segment = builder.GetSegment(123, 50, 2);

        Assert.True(segment.SideRooms.Count <= 3);
    }
}