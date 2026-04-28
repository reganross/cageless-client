using Xunit;

public class NetworkServerCommandTests
{
    /*
     PURPOSE:
     Ensure valid client controller commands are queued for simulation.

     DESIGN RULE:
     - Server accepts commands only from connected clients
     - Accepted commands are stored until authoritative simulation consumes them

     FAILURE MEANS:
     - Client input may be dropped before simulation
     - Networking may bypass the authoritative command queue
    */
    [Fact]
    public void ReceiveCommand_ShouldQueueControllerCommandForConnectedClient()
    {
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);
        var command = CreateCommand(clientId, tick: 1);

        server.ConnectClient(clientId);
        var accepted = server.ReceiveCommand(command);

        Assert.True(accepted);
        Assert.True(server.TryDequeueCommand(clientId, out var dequeued));
        Assert.Equal(command.Tick, dequeued.Tick);
        Assert.Equal(1, dequeued.Controller.GetActionStrength("forward"));
    }

    /*
     PURPOSE:
     Ensure each client has an independent command queue.

     DESIGN RULE:
     - Draining one client's commands does not drain another client's commands
     - Commands remain associated with their sending client

     FAILURE MEANS:
     - One client may consume another client's input
     - Authoritative simulation may apply commands to the wrong player
    */
    [Fact]
    public void TryDequeueCommand_ShouldUseIndependentClientQueues()
    {
        var server = new NetworkServer(historySize: 4);
        var firstClient = new ClientId(1);
        var secondClient = new ClientId(2);

        server.ConnectClient(firstClient);
        server.ConnectClient(secondClient);
        server.ReceiveCommand(CreateCommand(firstClient, tick: 1, actionName: "right"));
        server.ReceiveCommand(CreateCommand(secondClient, tick: 1, actionName: "left"));

        Assert.True(server.TryDequeueCommand(firstClient, out var firstCommand));
        Assert.True(server.TryDequeueCommand(secondClient, out var secondCommand));
        Assert.Equal(1, firstCommand.Controller.GetActionStrength("right"));
        Assert.Equal(1, secondCommand.Controller.GetActionStrength("left"));
    }

    /*
     PURPOSE:
     Ensure commands from unknown clients are rejected.

     DESIGN RULE:
     - Only connected clients can submit commands
     - Unknown client commands are ignored predictably

     FAILURE MEANS:
     - Unregistered clients may affect server simulation
     - Spoofed command packets may enter the authoritative queue
    */
    [Fact]
    public void ReceiveCommand_ShouldRejectUnknownClient()
    {
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);

        var accepted = server.ReceiveCommand(CreateCommand(clientId, tick: 1));

        Assert.False(accepted);
        Assert.False(server.TryDequeueCommand(clientId, out _));
    }

    /*
     PURPOSE:
     Ensure duplicate or older command ticks are rejected.

     DESIGN RULE:
     - Controller tick numbers increase per client
     - Server accepts each client command tick once

     FAILURE MEANS:
     - Duplicate UDP packets may apply input more than once
     - Out-of-order packets may rewind client intent
    */
    [Fact]
    public void ReceiveCommand_ShouldRejectDuplicateOrOlderTicks()
    {
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);

        server.ConnectClient(clientId);
        Assert.True(server.ReceiveCommand(CreateCommand(clientId, tick: 2)));
        Assert.False(server.ReceiveCommand(CreateCommand(clientId, tick: 2)));
        Assert.False(server.ReceiveCommand(CreateCommand(clientId, tick: 1)));
    }

    /*
     PURPOSE:
     Ensure accepted commands update the server controller manager.

     DESIGN RULE:
     - Command packets update the managed controller for that player
     - Stored controller state is available to authoritative simulation

     FAILURE MEANS:
     - Server simulation may read stale controller input
     - Command queue and controller manager may diverge
    */
    [Fact]
    public void ReceiveCommand_ShouldUpdateManagedController()
    {
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);

        server.ConnectClient(clientId);
        var accepted = server.ReceiveCommand(CreateCommand(clientId, tick: 1, actionName: "right"));

        Assert.True(accepted);
        Assert.True(server.Controllers.TryGet(clientId, out var controller));
        Assert.Equal(1, controller.GetActionStrength("right"));
    }

    /*
     PURPOSE:
     Ensure accepted delta commands update managed controller state in place.

     DESIGN RULE:
     - Delta controller packets merge into the existing server controller
     - Missing delta fields do not clear previous controller state

     FAILURE MEANS:
     - Server simulation may lose held inputs between full controller packets
     - Delta packets may replace instead of mutate managed controllers
    */
    [Fact]
    public void ReceiveCommand_ShouldApplyDeltaControllerCommand()
    {
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);

        server.ConnectClient(clientId);
        server.ReceiveCommand(CreateCommand(clientId, tick: 1, actionName: "forward"));
        var delta = new PlayerController(
            clientId,
            tick: 2,
            new[]
            {
                new InputActionState("right", 1)
            });
        var accepted = server.ReceiveCommand(new ClientCommandPacket(
            ClientCommandKind.Controller,
            ControllerPacketKind.Delta,
            hasLookRotation: false,
            delta));

        Assert.True(accepted);
        Assert.True(server.Controllers.TryGet(clientId, out var controller));
        Assert.Equal(1, controller.GetActionStrength("forward"));
        Assert.Equal(1, controller.GetActionStrength("right"));
    }

    private static ClientCommandPacket CreateCommand(
        ClientId clientId,
        int tick,
        string actionName = "forward")
    {
        return new ClientCommandPacket(
            ClientCommandKind.Controller,
            ControllerPacketKind.Full,
            hasLookRotation: true,
            new PlayerController(
                clientId,
                tick,
                new[]
                {
                    new InputActionState(actionName, 1)
                }));
    }
}
