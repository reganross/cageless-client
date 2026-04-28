using System.Collections.Generic;

public class PlayerControllerManager
{
    private readonly Dictionary<ClientId, PlayerController> controllers = new();

    public PlayerController GetOrCreate(ClientId playerId)
    {
        if (!controllers.TryGetValue(playerId, out var controller))
        {
            controller = new PlayerController(playerId, tick: 0);
            controllers[playerId] = controller;
        }

        return controller;
    }

    public bool TryGet(ClientId playerId, out PlayerController controller)
    {
        return controllers.TryGetValue(playerId, out controller);
    }

    public bool Apply(PlayerController snapshot)
    {
        if (!snapshot.HasPlayerId || !controllers.TryGetValue(snapshot.PlayerId, out var controller))
        {
            return false;
        }

        controller.ApplySnapshot(snapshot);
        return true;
    }

    public bool Apply(ClientCommandPacket command)
    {
        if (!controllers.TryGetValue(command.ClientId, out var controller))
        {
            return false;
        }

        if (command.ControllerPacketKind == ControllerPacketKind.Full)
        {
            controller.ApplySnapshot(command.Controller);
        }
        else
        {
            controller.ApplyDelta(command.Controller, command.HasLookRotation);
        }

        return true;
    }

    public void Remove(ClientId playerId)
    {
        controllers.Remove(playerId);
    }
}
