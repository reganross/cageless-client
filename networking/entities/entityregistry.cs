public class EntityRegistry
{
    private int nextId = 1;

    private readonly Dictionary<int, INetworkEntity> entities = new();

    public EntityId Create(INetworkEntity entity)
    {
        var id = new EntityId(nextId++);
        entities[id.Value] = entity;
        return id;
    }

    public void Remove(EntityId id)
    {
        entities.Remove(id.Value);
    }

    public IReadOnlyDictionary<int, INetworkEntity> All => entities;
}