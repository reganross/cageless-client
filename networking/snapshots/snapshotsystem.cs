public class SnapshotSystem
{
    private readonly EntityRegistry registry;

    private readonly SnapshotFrame[] frames;
    private int index;

    public SnapshotSystem(EntityRegistry registry, int historySize)
    {
        this.registry = registry;
        frames = new SnapshotFrame[historySize];
    }

    public void Capture(long tick)
    {
        var frame = new SnapshotFrame
        {
            Tick = tick,
            States = new Dictionary<EntityId, EntityState>()
        };

        foreach (var kv in registry.All)
        {
            var entity = kv.Value;
            frame.States[new EntityId(kv.Key)] = entity.CaptureState();
        }

        frames[index] = frame;
        index = (index + 1) % frames.Length;
    }
}