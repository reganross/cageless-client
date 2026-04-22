public interface IHistorySize
{
    static abstract int Value { get; }
}

public struct History240ms : IHistorySize
{
    public static int Value => 8; // 240ms / 30ms ≈ 8 frames
}