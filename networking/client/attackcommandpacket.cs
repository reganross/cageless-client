public readonly struct AttackCommandPacket
{
    public AttackCommandPacket(ClientId clientId, Tick tick)
    {
        ClientId = clientId;
        Tick = tick;
    }

    public ClientId ClientId { get; }
    public Tick Tick { get; }
}
