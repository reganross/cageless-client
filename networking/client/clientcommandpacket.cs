public readonly struct ClientCommandPacket
{
    public ClientCommandPacket(
        ClientId clientId,
        int sequence,
        ClientCommandKind kind,
        MovementCommand movement)
    {
        ClientId = clientId;
        Sequence = sequence;
        Kind = kind;
        Movement = movement;
    }

    public ClientId ClientId { get; }
    public int Sequence { get; }
    public ClientCommandKind Kind { get; }
    public MovementCommand Movement { get; }
}
