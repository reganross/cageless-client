public readonly struct SnapshotPacket
{
    public SnapshotPacket(SnapshotPacketKind kind, SnapshotFrame frame)
    {
        Kind = kind;
        Frame = frame;
    }

    public SnapshotPacketKind Kind { get; }
    public SnapshotFrame Frame { get; }
}
