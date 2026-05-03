using System.Collections.Generic;
using System.IO;

public static class ClientCommandSerializer
{
    public static byte[] Serialize(ClientCommandPacket packet)
    {
        if (!packet.Controller.HasPlayerId)
        {
            throw new InvalidDataException("Cannot serialize a local controller command without a player id.");
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((int)packet.Kind);
        writer.Write((int)packet.ControllerPacketKind);
        writer.Write(packet.Controller.PlayerId.Value);
        writer.Write(packet.Controller.Tick);
        writer.Write(packet.HasLookRotation);

        if (packet.HasLookRotation)
        {
            writer.Write(packet.Controller.LookYaw);
            writer.Write(packet.Controller.LookPitch);
        }

        writer.Write(packet.Controller.Actions.Count);
        foreach (var action in packet.Controller.Actions)
        {
            writer.Write(action.ActionName);
            writer.Write(action.Strength);
        }

        return stream.ToArray();
    }

    public static ClientCommandPacket Deserialize(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);

            var kind = (ClientCommandKind)reader.ReadInt32();
            if (kind != ClientCommandKind.Controller)
            {
                throw new InvalidDataException("Client command kind is invalid.");
            }

            var controllerPacketKind = (ControllerPacketKind)reader.ReadInt32();
            if (controllerPacketKind != ControllerPacketKind.Full
                && controllerPacketKind != ControllerPacketKind.Delta)
            {
                throw new InvalidDataException("Controller packet kind is invalid.");
            }

            var playerId = new ClientId(reader.ReadInt32());
            int tick = reader.ReadInt32();
            bool hasLookRotation = reader.ReadBoolean();
            float lookYaw = 0;
            float lookPitch = 0;

            if (hasLookRotation)
            {
                lookYaw = reader.ReadSingle();
                lookPitch = reader.ReadSingle();
            }

            int actionCount = reader.ReadInt32();
            if (actionCount < 0)
            {
                throw new InvalidDataException("Controller command action count cannot be negative.");
            }

            var actions = new List<InputActionState>();
            for (int i = 0; i < actionCount; i++)
            {
                actions.Add(new InputActionState(
                    reader.ReadString(),
                    reader.ReadSingle()));
            }

            var controller = new PlayerController(playerId, tick, actions);
            if (hasLookRotation)
            {
                controller.SetLookRotation(lookYaw, lookPitch);
            }

            return new ClientCommandPacket(
                kind,
                controllerPacketKind,
                hasLookRotation,
                controller);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Client command packet ended before all fields were read.", ex);
        }
    }
}
