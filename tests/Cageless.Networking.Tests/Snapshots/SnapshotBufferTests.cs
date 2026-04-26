using Xunit;

public class SnapshotBufferTests
{
    /*
     PURPOSE:
     Ensure snapshot buffers return the most recent captured frame.

     DESIGN RULE:
     - Step 0 is the latest snapshot
     - Higher steps move backward through capture history

     FAILURE MEANS:
     - Reconciliation may read an empty or stale frame
     - Snapshot consumers may compare against the wrong tick
    */
    [Fact]
    public void Get_ShouldReturnSnapshotsByStepsBack()
    {
        var buffer = new SnapshotBuffer<History3>();

        buffer.AddSnapshot(new SnapshotFrame { Tick = 100 });
        buffer.AddSnapshot(new SnapshotFrame { Tick = 101 });

        Assert.Equal(101, buffer.Get(0).Tick);
        Assert.Equal(100, buffer.Get(1).Tick);
    }

    /*
     PURPOSE:
     Ensure snapshot buffers overwrite the oldest frame after history fills.

     DESIGN RULE:
     - Buffer size limits retained history
     - New snapshots replace the oldest stored frame

     FAILURE MEANS:
     - Snapshot history may grow unbounded
     - Old frames may be returned instead of recent network state
    */
    [Fact]
    public void AddSnapshot_ShouldOverwriteOldestFrameWhenHistoryIsFull()
    {
        var buffer = new SnapshotBuffer<History3>();

        buffer.AddSnapshot(new SnapshotFrame { Tick = 100 });
        buffer.AddSnapshot(new SnapshotFrame { Tick = 101 });
        buffer.AddSnapshot(new SnapshotFrame { Tick = 102 });
        buffer.AddSnapshot(new SnapshotFrame { Tick = 103 });

        Assert.Equal(103, buffer.Get(0).Tick);
        Assert.Equal(102, buffer.Get(1).Tick);
        Assert.Equal(101, buffer.Get(2).Tick);
    }

    /*
     PURPOSE:
     Ensure snapshot buffers reject history access outside their capacity.

     DESIGN RULE:
     - Negative steps are invalid
     - Steps equal to or larger than history size are invalid

     FAILURE MEANS:
     - Snapshot consumers may silently read the wrong frame
     - Invalid reconciliation requests may hide bugs
    */
    [Fact]
    public void Get_ShouldRejectOutOfBoundsSteps()
    {
        var buffer = new SnapshotBuffer<History3>();

        Assert.Throws<System.ArgumentOutOfRangeException>(() => buffer.Get(-1));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => buffer.Get(3));
    }

    private struct History3 : IHistorySize
    {
        public static int Value => 3;
    }
}
