public readonly struct ClientCommandPacket
{
    public ClientCommandPacket(
        ClientCommandKind kind,
        ControllerPacketKind controllerPacketKind,
        bool hasLookRotation,
        PlayerController controller)
    {
        Kind = kind;
        ControllerPacketKind = controllerPacketKind;
        HasLookRotation = hasLookRotation;
        Controller = controller;
    }

    public ClientId ClientId => Controller.PlayerId;
    public int Tick => Controller.Tick;
    public ClientCommandKind Kind { get; }
    public ControllerPacketKind ControllerPacketKind { get; }
    public bool HasLookRotation { get; }
    public PlayerController Controller { get; }
}
