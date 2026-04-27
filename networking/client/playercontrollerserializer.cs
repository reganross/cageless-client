using System.IO;
using System.Collections.Generic;

public static class PlayerControllerSerializer
{
    public static byte[] Serialize(PlayerController controller)
    {
        if (!controller.HasPlayerId)
        {
            throw new InvalidDataException("Cannot serialize a local controller without a player id.");
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(controller.PlayerId.Value);
        writer.Write(controller.Sequence);
        writer.Write(controller.LookYaw);
        writer.Write(controller.LookPitch);
        writer.Write(controller.Actions.Count);

        foreach (var action in controller.Actions)
        {
            writer.Write(action.ActionName);
            writer.Write(action.Strength);
        }

        return stream.ToArray();
    }

    public static PlayerController Deserialize(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);

            var playerId = new ClientId(reader.ReadInt32());
            int sequence = reader.ReadInt32();
            float lookYaw = reader.ReadSingle();
            float lookPitch = reader.ReadSingle();
            int actionCount = reader.ReadInt32();
            if (actionCount < 0)
            {
                throw new InvalidDataException("Player controller action count cannot be negative.");
            }

            var actions = new List<InputActionState>();
            for (int i = 0; i < actionCount; i++)
            {
                actions.Add(new InputActionState(
                    reader.ReadString(),
                    reader.ReadSingle()));
            }

            var controller = new PlayerController(
                playerId,
                sequence,
                actions);
            controller.SetLookRotation(lookYaw, lookPitch);
            return controller;
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Player controller packet ended before all fields were read.", ex);
        }
    }
}
