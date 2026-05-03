using System;
using System.Collections.Generic;

public sealed class ClientSceneReplicationRegistry
{
    private readonly int localOwnerId;
    private readonly Dictionary<int, IClientSceneReplica> replicas = new();
    private readonly Dictionary<NetworkEntityType, Func<int, EntityState, IClientSceneReplica>> factories = new();

    public ClientSceneReplicationRegistry(int localOwnerId = 0)
    {
        this.localOwnerId = localOwnerId;
    }

    public void RegisterFactory(
        NetworkEntityType entityType,
        Func<int, EntityState, IClientSceneReplica> factory)
    {
        factories[entityType] = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public bool TryGetReplica(int entityId, out IClientSceneReplica replica)
    {
        return replicas.TryGetValue(entityId, out replica);
    }

    public void ApplySnapshot(SnapshotPacket packet)
    {
        var seenEntityIds = new HashSet<int>();

        foreach (var kv in packet.Frame.States)
        {
            int entityId = kv.Key;
            var state = kv.Value;
            if (ShouldSkipLocalReplica(state))
            {
                continue;
            }

            if (replicas.TryGetValue(entityId, out var replica))
            {
                replica.ApplyState(state);
                seenEntityIds.Add(entityId);
                continue;
            }

            if (!factories.TryGetValue((NetworkEntityType)state.TypeId, out var factory))
            {
                continue;
            }

            var spawnedReplica = factory(entityId, state);
            if (spawnedReplica != null)
            {
                replicas[entityId] = spawnedReplica;
                seenEntityIds.Add(entityId);
            }
        }

        if (packet.Kind == SnapshotPacketKind.Full)
        {
            DespawnMissingReplicas(seenEntityIds);
        }
    }

    public void ReconcileEntities(IReadOnlyDictionary<int, EntityState> states)
    {
        foreach (var kv in states)
        {
            int entityId = kv.Key;
            if (replicas.ContainsKey(entityId))
            {
                continue;
            }

            var state = kv.Value;
            if (ShouldSkipLocalReplica(state)
                || !factories.TryGetValue((NetworkEntityType)state.TypeId, out var factory))
            {
                continue;
            }

            var spawnedReplica = factory(entityId, state);
            if (spawnedReplica != null)
            {
                replicas[entityId] = spawnedReplica;
            }
        }
    }

    private bool ShouldSkipLocalReplica(EntityState state)
    {
        return localOwnerId != 0
            && state.TypeId == (int)NetworkEntityType.Player
            && state.OwnerId == localOwnerId;
    }

    private void DespawnMissingReplicas(HashSet<int> seenEntityIds)
    {
        var missingEntityIds = new List<int>();
        foreach (var entityId in replicas.Keys)
        {
            if (!seenEntityIds.Contains(entityId))
            {
                missingEntityIds.Add(entityId);
            }
        }

        foreach (int entityId in missingEntityIds)
        {
            replicas[entityId].Despawn();
            replicas.Remove(entityId);
        }
    }
}
