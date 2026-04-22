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
        int i = (index - stepsBack + frames.Length) % frames.Length;
        return frames[i];
    }
}