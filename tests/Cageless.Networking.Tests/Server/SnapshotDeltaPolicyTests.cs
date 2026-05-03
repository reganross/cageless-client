using System.Collections.Generic;
using Godot;
using Xunit;

public class SnapshotDeltaPolicyTests
{
    /*
     PURPOSE:
     Ensure unchanged entities are omitted from delta snapshots.

     DESIGN RULE:
     - Full snapshots establish the last sent baseline
     - Delta snapshots include only entities that changed beyond thresholds

     FAILURE MEANS:
     - Server may resend unchanged entity state every update
     - Snapshot bandwidth may grow with total entity count instead of changed entity count
    */
    [Fact]
    public void CreatePacket_ShouldOmitUnchangedEntitiesFromDelta()
    {
        var policy = new SnapshotDeltaPolicy(positionThreshold: 0.1f);
        var baseline = new Dictionary<int, EntityState>();
        var frame = CreateFrame(42, new Vector3(1, 2, 3));

        policy.CreatePacket(frame, baseline, forceFull: true);

        var packet = policy.CreatePacket(frame, baseline, forceFull: false);

        Assert.Equal(SnapshotPacketKind.Delta, packet.Kind);
        Assert.Empty(packet.Frame.States);
    }

    /*
     PURPOSE:
     Ensure tiny movement below threshold is omitted from delta snapshots.

     DESIGN RULE:
     - Position changes below threshold are treated as unchanged
     - Delta snapshots suppress insignificant movement noise

     FAILURE MEANS:
     - Network packets may be sent for meaningless jitter
     - Bandwidth reduction may not work while entities are nearly stationary
    */
    [Fact]
    public void CreatePacket_ShouldOmitPositionChangeBelowThreshold()
    {
        var policy = new SnapshotDeltaPolicy(positionThreshold: 0.1f);
        var baseline = new Dictionary<int, EntityState>();

        policy.CreatePacket(CreateFrame(1, Vector3.Zero), baseline, forceFull: true);
        var packet = policy.CreatePacket(CreateFrame(2, new Vector3(0.05f, 0, 0)), baseline, forceFull: false);

        Assert.Empty(packet.Frame.States);
    }

    /*
     PURPOSE:
     Ensure meaningful movement is included in delta snapshots.

     DESIGN RULE:
     - Position changes above threshold are sent
     - Changed states update the last sent baseline

     FAILURE MEANS:
     - Clients may miss real entity movement
     - Reconciliation may compare against stale positions
    */
    [Fact]
    public void CreatePacket_ShouldIncludePositionChangeAboveThreshold()
    {
        var policy = new SnapshotDeltaPolicy(positionThreshold: 0.1f);
        var baseline = new Dictionary<int, EntityState>();

        policy.CreatePacket(CreateFrame(1, Vector3.Zero), baseline, forceFull: true);
        var packet = policy.CreatePacket(CreateFrame(2, new Vector3(0.2f, 0, 0)), baseline, forceFull: false);

        Assert.True(packet.Frame.States.ContainsKey(7));
        Assert.Equal(new Vector3(0.2f, 0, 0), baseline[7].Position);
    }

    /*
     PURPOSE:
     Ensure state flag changes are always included in delta snapshots.

     DESIGN RULE:
     - Flags represent discrete gameplay state
     - Flag changes are sent even when transform changes are below threshold

     FAILURE MEANS:
     - Clients may miss important state transitions
     - Gameplay flags may desync even while entities are stationary
    */
    [Fact]
    public void CreatePacket_ShouldIncludeStateFlagChanges()
    {
        var policy = new SnapshotDeltaPolicy(positionThreshold: 0.1f);
        var baseline = new Dictionary<int, EntityState>();

        policy.CreatePacket(CreateFrame(1, Vector3.Zero, stateFlags: 1), baseline, forceFull: true);
        var packet = policy.CreatePacket(CreateFrame(2, Vector3.Zero, stateFlags: 2), baseline, forceFull: false);

        Assert.True(packet.Frame.States.ContainsKey(7));
        Assert.Equal(2, packet.Frame.States[7].StateFlags);
    }

    private static SnapshotFrame CreateFrame(long tick, Vector3 position, int stateFlags = 0)
    {
        return new SnapshotFrame
        {
            Tick = tick,
            States = new Dictionary<int, EntityState>
            {
                [7] = new EntityState
                {
                    Position = position,
                    Rotation = Quaternion.Identity,
                    Velocity = Vector3.Zero,
                    StateFlags = stateFlags
                }
            }
        };
    }
}
