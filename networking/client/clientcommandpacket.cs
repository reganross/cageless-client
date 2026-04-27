public readonly struct ClientCommandPacket
{
    public ClientCommandPacket(
        ClientCommandKind kind,
        PlayerController controller)
    {
        Kind = kind;
        Controller = controller;
    }

    public ClientId ClientId => Controller.PlayerId;
    public int Sequence => Controller.Sequence;
    public ClientCommandKind Kind { get; }
    public PlayerController Controller { get; }
}
