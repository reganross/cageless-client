public static class ClientCommandSerializer
{
    public static byte[] Serialize(ClientCommandPacket packet)
    {
        return PlayerControllerSerializer.Serialize(packet.Controller);
    }

    public static ClientCommandPacket Deserialize(byte[] bytes)
    {
        return new ClientCommandPacket(
            ClientCommandKind.Controller,
            PlayerControllerSerializer.Deserialize(bytes));
    }
}
