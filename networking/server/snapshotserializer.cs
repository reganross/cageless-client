using System.Collections.Generic;
using System.IO;

public static class SnapshotSerializer
{
    public static byte[] Serialize(SnapshotPacket packet)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((int)packet.Kind);
        WriteFrame(writer, packet.Frame);

        return stream.ToArray();
    }

    public static byte[] Serialize(SnapshotFrame frame)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteFrame(writer, frame);

        return stream.ToArray();
    }

    public static SnapshotPacket DeserializePacket(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);

            var kind = (SnapshotPacketKind)reader.ReadInt32();
            if (kind != SnapshotPacketKind.Full && kind != SnapshotPacketKind.Delta)
            {
                throw new InvalidDataException("Snapshot packet kind is invalid.");
            }

            return new SnapshotPacket(kind, ReadFrame(reader));
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Snapshot packet ended before all fields were read.", ex);
        }
    }

    private static void WriteFrame(BinaryWriter writer, SnapshotFrame frame)
    {
        writer.Write(frame.Tick);
        writer.Write(frame.States?.Count ?? 0);

        if (frame.States == null)
        {
            return;
        }

        foreach (KeyValuePair<int, EntityState> kv in frame.States)
        {
            var state = kv.Value;

            writer.Write(kv.Key);
            writer.Write(state.Position.X);
            writer.Write(state.Position.Y);
            writer.Write(state.Position.Z);
            writer.Write(state.Rotation.X);
            writer.Write(state.Rotation.Y);
            writer.Write(state.Rotation.Z);
            writer.Write(state.Rotation.W);
            writer.Write(state.Velocity.X);
            writer.Write(state.Velocity.Y);
            writer.Write(state.Velocity.Z);
            writer.Write(state.StateFlags);
        }
    }

    public static SnapshotFrame Deserialize(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);

            return ReadFrame(reader);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Snapshot packet ended before all fields were read.", ex);
        }
    }

    private static SnapshotFrame ReadFrame(BinaryReader reader)
    {
        var frame = new SnapshotFrame
        {
            Tick = reader.ReadInt64(),
            States = new Dictionary<int, EntityState>()
        };

        int stateCount = reader.ReadInt32();
        if (stateCount < 0)
        {
            throw new InvalidDataException("Snapshot state count cannot be negative.");
        }

        for (int i = 0; i < stateCount; i++)
        {
            int entityId = reader.ReadInt32();
            var state = new EntityState
            {
                Position = new Godot.Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()),
                Rotation = new Godot.Quaternion(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()),
                Velocity = new Godot.Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()),
                StateFlags = reader.ReadInt32()
            };

            frame.States[entityId] = state;
        }

        return frame;
    }
}
