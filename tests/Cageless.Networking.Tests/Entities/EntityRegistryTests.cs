using Xunit;

public class EntityRegistryTests
{
    /*
     PURPOSE:
     Ensure registering network entities assigns stable unique ids.

     DESIGN RULE:
     - Each registered entity receives a distinct id
     - Entity ids are positive and start from the registry sequence

     FAILURE MEANS:
     - Snapshot frames may overwrite entity state
     - Network messages may target the wrong entity
    */
    [Fact]
    public void Create_ShouldAssignUniqueEntityIds()
    {
        var registry = new EntityRegistry();

        var firstId = registry.Create(new TestNetworkEntity());
        var secondId = registry.Create(new TestNetworkEntity());

        Assert.NotEqual(firstId, secondId);
        Assert.Equal(1, firstId.Value);
        Assert.Equal(2, secondId.Value);
    }

    /*
     PURPOSE:
     Ensure registered entities are visible to snapshot capture.

     DESIGN RULE:
     - Registry exposes currently registered entities
     - Entities are keyed by their assigned entity id

     FAILURE MEANS:
     - Snapshot capture may miss live entities
     - Entity state may be associated with the wrong id
    */
    [Fact]
    public void Create_ShouldExposeEntityByAssignedId()
    {
        var registry = new EntityRegistry();
        var entity = new TestNetworkEntity();

        var entityId = registry.Create(entity);

        Assert.True(registry.All.ContainsKey(entityId.Value));
        Assert.Same(entity, registry.All[entityId.Value]);
    }

    /*
     PURPOSE:
     Ensure removed entities are no longer visible to snapshot capture.

     DESIGN RULE:
     - Removing an entity deregisters its id
     - Registry only exposes currently active entities

     FAILURE MEANS:
     - Deleted entities may continue appearing in snapshots
     - Clients may preserve entities that should be gone
    */
    [Fact]
    public void Remove_ShouldDeregisterEntity()
    {
        var registry = new EntityRegistry();
        var entityId = registry.Create(new TestNetworkEntity());

        registry.Remove(entityId);

        Assert.False(registry.All.ContainsKey(entityId.Value));
    }

    private sealed class TestNetworkEntity : INetworkEntity
    {
        public EntityId Id { get; } = new(0);

        public EntityState CaptureState() => new();
    }
}
