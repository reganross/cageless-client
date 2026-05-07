using System.IO;

public static class AttackCommandSerializer
{
    public static byte[] Serialize(AttackCommandPacket packet)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(packet.ClientId.Value);
        writer.Write(packet.Tick.Value);

        return stream.ToArray();
    }

    public static AttackCommandPacket Deserialize(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);

            return new AttackCommandPacket(
                new ClientId(reader.ReadInt32()),
                new Tick(reader.ReadInt32()));
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Attack command packet ended before all fields were read.", ex);
        }
    }
}
