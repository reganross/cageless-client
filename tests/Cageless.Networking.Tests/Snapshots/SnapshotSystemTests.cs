using Godot;
using Xunit;

public class SnapshotSystemTests
{
    /*
     PURPOSE:
     Ensure registered network entities are captured into snapshots.

     DESIGN RULE:
     - Registered entities provide their current network state
     - Snapshot captures store state by entity id

     FAILURE MEANS:
     - Entity state will not replicate correctly
     - Clients may receive incomplete snapshots
    */
    [Fact]
    public void Capture_ShouldStoreRegisteredEntityState()
    {
        var registry = new EntityRegistry();
        var entity = new TestNetworkEntity(new EntityState
        {
            Position = new Vector3(1, 2, 3),
            Rotation = Quaternion.Identity,
            Velocity = new Vector3(4, 5, 6),
            StateFlags = 7
        });
        var entityId = registry.Create(entity);
        var snapshotSystem = new SnapshotSystem(registry, historySize: 4);

        snapshotSystem.Capture(tick: 42);

        var frame = snapshotSystem.GetLatest();
        Assert.Equal(42, frame.Tick);
        Assert.True(frame.States.ContainsKey(entityId.Value));
        Assert.Equal(entity.CaptureState().Position, frame.States[entityId.Value].Position);
        Assert.Equal(entity.CaptureState().Rotation, frame.States[entityId.Value].Rotation);
        Assert.Equal(entity.CaptureState().Velocity, frame.States[entityId.Value].Velocity);
        Assert.Equal(entity.CaptureState().StateFlags, frame.States[entityId.Value].StateFlags);
    }

    /*
     PURPOSE:
     Ensure deregistered network entities are excluded from new snapshots.

     DESIGN RULE:
     - Deleted entities must deregister before capture
     - Snapshot captures only include currently registered entities

     FAILURE MEANS:
     - Deleted entities may continue replicating
     - Clients may keep stale entities alive
    */
    [Fact]
    public void Capture_ShouldNotStoreDeregisteredEntityState()
    {
        var registry = new EntityRegistry();
        var entity = new TestNetworkEntity(new EntityState
        {
            Position = new Vector3(1, 2, 3)
        });
        var entityId = registry.Create(entity);
        var snapshotSystem = new SnapshotSystem(registry, historySize: 4);

        registry.Remove(entityId);
        snapshotSystem.Capture(tick: 42);

        var frame = snapshotSystem.GetLatest();
        Assert.False(frame.States.ContainsKey(entityId.Value));
    }

    /*
     PURPOSE:
     Ensure snapshot history can return recent captured frames.

     DESIGN RULE:
     - Step 0 is the latest snapshot
     - Higher steps move backward through capture history

     FAILURE MEANS:
     - Reconciliation cannot inspect previous snapshots
     - Late network updates may compare against the wrong frame
    */
    [Fact]
    public void Get_ShouldReturnCapturedFramesByStepsBack()
    {
        var registry = new EntityRegistry();
        var entity = new TestNetworkEntity(new EntityState
        {
            Position = new Vector3(1, 2, 3)
        });
        registry.Create(entity);
        var snapshotSystem = new SnapshotSystem(registry, historySize: 4);

        snapshotSystem.Capture(tick: 100);
        entity.State = new EntityState
        {
            Position = new Vector3(9, 8, 7)
        };
        snapshotSystem.Capture(tick: 101);

        Assert.Equal(101, snapshotSystem.Get(0).Tick);
        Assert.Equal(100, snapshotSystem.Get(1).Tick);
    }

    private sealed class TestNetworkEntity : INetworkEntity
    {
        public TestNetworkEntity(EntityState state)
        {
            State = state;
        }

        public EntityState State { get; set; }

        public EntityId Id { get; } = new(0);

        public EntityState CaptureState() => State;
    }
}
