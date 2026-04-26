using System.IO;
using Godot;

public static class ClientCommandSerializer
{
    public static byte[] Serialize(ClientCommandPacket packet)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(packet.ClientId.Value);
        writer.Write(packet.Sequence);
        writer.Write((int)packet.Kind);
        writer.Write(packet.Movement.MoveDirection.X);
        writer.Write(packet.Movement.MoveDirection.Y);
        writer.Write(packet.Movement.JumpPressed);

        return stream.ToArray();
    }

    public static ClientCommandPacket Deserialize(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);

            var clientId = new ClientId(reader.ReadInt32());
            int sequence = reader.ReadInt32();
            var kind = (ClientCommandKind)reader.ReadInt32();
            if (kind != ClientCommandKind.Movement)
            {
                throw new InvalidDataException("Client command kind is invalid.");
            }

            var movement = new MovementCommand(
                new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                reader.ReadBoolean());

            return new ClientCommandPacket(clientId, sequence, kind, movement);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Client command packet ended before all fields were read.", ex);
        }
    }
}
