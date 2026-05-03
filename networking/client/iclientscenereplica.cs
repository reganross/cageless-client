public interface IClientSceneReplica
{
    int EntityId { get; }

    void ApplyState(EntityState state);

    void Despawn();
}
