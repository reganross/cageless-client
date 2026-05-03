using System.Collections.Generic;

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
            States = new Dictionary<int, EntityState>()
        };

        foreach (var kv in registry.All)
        {
            var entity = kv.Value;
            frame.States[kv.Key] = entity.CaptureState();
        }

        frames[index] = frame;
        index = (index + 1) % frames.Length;
    }

    public SnapshotFrame GetLatest()
    {
        return Get(0);
    }

    public SnapshotFrame Get(int stepsBack)
    {
        if (stepsBack < 0 || stepsBack >= frames.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(stepsBack));
        }

        var frameIndex = (index - 1 - stepsBack + frames.Length) % frames.Length;
        return frames[frameIndex];
    }
}