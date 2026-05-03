public class SnapshotBuffer<TSize>
    where TSize : struct, IHistorySize
{
    private readonly SnapshotFrame[] frames;
    private int index;

    public SnapshotBuffer()
    {
        frames = new SnapshotFrame[TSize.Value];
        index = 0;
    }

    public void AddSnapshot(SnapshotFrame frame)
    {
        frames[index] = frame;
        index = (index + 1) % frames.Length;
    }

    public SnapshotFrame Get(int stepsBack)
    {
        if (stepsBack < 0 || stepsBack >= frames.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(stepsBack));
        }

        int i = (index - 1 - stepsBack + frames.Length) % frames.Length;
        return frames[i];
    }
}