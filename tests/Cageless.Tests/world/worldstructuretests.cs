public class WorldStructureTests
{
    /*
     PURPOSE:
     Ensure main path is continuous
    */
    [Fact]
    public void MainPath_ShouldAlwaysBeContinuous()
    {
        for (int depth = 0; depth < 100; depth++)
        {
            var segment = builder.GetSegment(123, depth, 1);

            Assert.True(
                segment.ConnectsForward,
                $"Broken path at depth {depth}"
            );
        }
    }

    /*
     PURPOSE:
     Ensure boss always has loot room after
    */
    [Fact]
    public void Boss_ShouldAlwaysHaveLootRoomAfter()
    {
        var segment = builder.GetSegment(123, 100, 1);

        if (segment.HasBoss)
        {
            Assert.True(segment.HasLootAfterBoss);
        }
    }

    /*
     PURPOSE:
     Ensure side rooms are reachable
    */
    [Fact]
    public void SideRooms_ShouldBeAccessible()
    {
        var segment = builder.GetSegment(123, 50, 1);

        foreach (var room in segment.SideRooms)
        {
            Assert.True(room.IsConnectedToMainPath);
        }
    }
}