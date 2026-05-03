using System.Collections.Generic;
using Godot;
using Xunit;

public class ClientSceneReplicationRegistryTests
{
    /*
     PURPOSE:
     Ensure full snapshots spawn missing replicated entities.

     DESIGN RULE:
     - Snapshot entity type selects the registered replica factory
     - The registry tracks spawned replicas by network entity id

     FAILURE MEANS:
     - Joining clients may receive snapshots without creating scene nodes
     - Remote players may not appear in the combat scene
    */
    [Fact]
    public void ApplySnapshot_ShouldSpawnMissingEntityFromFullSnapshot()
    {
        var registry = new ClientSceneReplicationRegistry();
        registry.RegisterFactory(NetworkEntityType.Player, (entityId, state) => new TestReplica(entityId, state));

        registry.ApplySnapshot(new SnapshotPacket(
            SnapshotPacketKind.Full,
            CreateFrame(7, PlayerState(ownerId: 2, position: new Vector3(1, 2, 3)))));

        Assert.True(registry.TryGetReplica(7, out var replica));
        Assert.Equal(new Vector3(1, 2, 3), ((TestReplica)replica).LastState.Position);
    }

    /*
     PURPOSE:
     Ensure incoming snapshots update existing replicated entities.

     DESIGN RULE:
     - Existing replicas are updated in place
     - Delta snapshots can patch already spawned replicas

     FAILURE MEANS:
     - Remote nodes may stay frozen after spawning
     - Delta packets may create duplicate scene nodes
    */
    [Fact]
    public void ApplySnapshot_ShouldUpdateExistingReplica()
    {
        var registry = new ClientSceneReplicationRegistry();
        registry.RegisterFactory(NetworkEntityType.Player, (entityId, state) => new TestReplica(entityId, state));
        registry.ApplySnapshot(new SnapshotPacket(
            SnapshotPacketKind.Full,
            CreateFrame(7, PlayerState(ownerId: 2, position: Vector3.Zero))));

        registry.ApplySnapshot(new SnapshotPacket(
            SnapshotPacketKind.Delta,
            CreateFrame(7, PlayerState(ownerId: 2, position: new Vector3(4, 5, 6)))));

        Assert.True(registry.TryGetReplica(7, out var replica));
        Assert.Equal(new Vector3(4, 5, 6), ((TestReplica)replica).LastState.Position);
        Assert.Equal(2, ((TestReplica)replica).ApplyCount);
    }

    /*
     PURPOSE:
     Ensure full snapshots remove entities no longer owned by the server.

     DESIGN RULE:
     - Full snapshots are authoritative for current entity presence
     - Missing remote replicas are despawned during full snapshot application

     FAILURE MEANS:
     - Disconnected players may remain visible forever
     - Client scene state may drift from server authority
    */
    [Fact]
    public void ApplySnapshot_ShouldDespawnMissingReplicaFromFullSnapshot()
    {
        var registry = new ClientSceneReplicationRegistry();
        registry.RegisterFactory(NetworkEntityType.Player, (entityId, state) => new TestReplica(entityId, state));
        registry.ApplySnapshot(new SnapshotPacket(
            SnapshotPacketKind.Full,
            CreateFrame(7, PlayerState(ownerId: 2, position: Vector3.Zero))));

        registry.ApplySnapshot(new SnapshotPacket(
            SnapshotPacketKind.Full,
            new SnapshotFrame
            {
                Tick = 2,
                States = new Dictionary<int, EntityState>()
            }));

        Assert.False(registry.TryGetReplica(7, out _));
    }

    /*
     PURPOSE:
     Ensure unknown entity types are ignored safely.

     DESIGN RULE:
     - Replicas are spawned only from registered factories
     - Malformed or unsupported entity data does not crash snapshot application

     FAILURE MEANS:
     - One unsupported entity type may break all replication
     - Clients may instantiate the wrong scene for unknown data
    */
    [Fact]
    public void ApplySnapshot_ShouldIgnoreUnknownEntityType()
    {
        var registry = new ClientSceneReplicationRegistry();

        registry.ApplySnapshot(new SnapshotPacket(
            SnapshotPacketKind.Full,
            CreateFrame(7, new EntityState
            {
                TypeId = 999,
                OwnerId = 2
            })));

        Assert.False(registry.TryGetReplica(7, out _));
    }

    /*
     PURPOSE:
     Ensure scene reconciliation can spawn entities already present in the client cache.

     DESIGN RULE:
     - The scene can recover if a spawn snapshot was consumed before the scene registry existed
     - Cached server truth is enough to create missing scene replicas

     FAILURE MEANS:
     - Entities may appear in debug snapshot logs without appearing in the Godot scene
     - Late-created scene scripts may miss existing server entities
    */
    [Fact]
    public void ReconcileEntities_ShouldSpawnMissingReplicaFromCachedState()
    {
        var registry = new ClientSceneReplicationRegistry();
        registry.RegisterFactory(NetworkEntityType.Spearman, (entityId, state) => new TestReplica(entityId, state));
        var states = new Dictionary<int, EntityState>
        {
            [9] = new EntityState
            {
                TypeId = (int)NetworkEntityType.Spearman,
                Position = new Vector3(2, 3, 4)
            }
        };

        registry.ReconcileEntities(states);

        Assert.True(registry.TryGetReplica(9, out var replica));
        Assert.Equal(new Vector3(2, 3, 4), ((TestReplica)replica).LastState.Position);
    }

    /*
     PURPOSE:
     Ensure reconciliation does not duplicate existing scene replicas.

     DESIGN RULE:
     - Reconciliation only creates missing nodes
     - Existing replicas continue to receive updates through snapshot application

     FAILURE MEANS:
     - Repeated reconciliation may add duplicate scene nodes every frame
     - Entity ids may no longer map to one scene node
    */
    [Fact]
    public void ReconcileEntities_ShouldNotDuplicateExistingReplica()
    {
        int spawnCount = 0;
        var registry = new ClientSceneReplicationRegistry();
        registry.RegisterFactory(NetworkEntityType.Spearman, (entityId, state) =>
        {
            spawnCount++;
            return new TestReplica(entityId, state);
        });
        var states = new Dictionary<int, EntityState>
        {
            [9] = new EntityState
            {
                TypeId = (int)NetworkEntityType.Spearman,
                Position = new Vector3(2, 3, 4)
            }
        };

        registry.ReconcileEntities(states);
        registry.ReconcileEntities(states);

        Assert.Equal(1, spawnCount);
    }

    /*
     PURPOSE:
     Ensure reconciliation honors the local player skip rule.

     DESIGN RULE:
     - Local-owned player is still created by combat scene logic
     - Cache reconciliation should not create a duplicate local player

     FAILURE MEANS:
     - Joined clients may spawn a second copy of themselves
     - Local prediction may fight a remote replica of the same player
    */
    [Fact]
    public void ReconcileEntities_ShouldSkipLocalOwnedPlayer()
    {
        var registry = new ClientSceneReplicationRegistry(localOwnerId: 2);
        registry.RegisterFactory(NetworkEntityType.Player, (entityId, state) => new TestReplica(entityId, state));
        var states = new Dictionary<int, EntityState>
        {
            [7] = PlayerState(ownerId: 2, position: Vector3.Zero)
        };

        registry.ReconcileEntities(states);

        Assert.False(registry.TryGetReplica(7, out _));
    }

    /*
     PURPOSE:
     Ensure the local client's own player is not spawned as a remote replica.

     DESIGN RULE:
     - Local player is created by the combat scene
     - Replication spawns only remote entities for the local client

     FAILURE MEANS:
     - Joining clients may see duplicate copies of themselves
     - Local input may fight with replicated server state
    */
    [Fact]
    public void ApplySnapshot_ShouldSkipLocalOwnedPlayer()
    {
        var registry = new ClientSceneReplicationRegistry(localOwnerId: 2);
        registry.RegisterFactory(NetworkEntityType.Player, (entityId, state) => new TestReplica(entityId, state));

        registry.ApplySnapshot(new SnapshotPacket(
            SnapshotPacketKind.Full,
            CreateFrame(7, PlayerState(ownerId: 2, position: Vector3.Zero))));

        Assert.False(registry.TryGetReplica(7, out _));
    }

    private static SnapshotFrame CreateFrame(int entityId, EntityState state)
    {
        return new SnapshotFrame
        {
            Tick = 1,
            States = new Dictionary<int, EntityState>
            {
                [entityId] = state
            }
        };
    }

    private static EntityState PlayerState(int ownerId, Vector3 position)
    {
        return new EntityState
        {
            TypeId = (int)NetworkEntityType.Player,
            OwnerId = ownerId,
            Position = position,
            Rotation = Quaternion.Identity
        };
    }

    private sealed class TestReplica : IClientSceneReplica
    {
        public TestReplica(int entityId, EntityState state)
        {
            EntityId = entityId;
            ApplyState(state);
        }

        public int EntityId { get; }
        public EntityState LastState { get; private set; }
        public int ApplyCount { get; private set; }
        public bool Despawned { get; private set; }

        public void ApplyState(EntityState state)
        {
            LastState = state;
            ApplyCount++;
        }

        public void Despawn()
        {
            Despawned = true;
        }
    }
}
