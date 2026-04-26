using Godot;
using Xunit;

public class NetworkServerCommandTests
{
    /*
     PURPOSE:
     Ensure valid client movement commands are queued for simulation.

     DESIGN RULE:
     - Server accepts commands only from connected clients
     - Accepted commands are stored until authoritative simulation consumes them

     FAILURE MEANS:
     - Client input may be dropped before simulation
     - Networking may bypass the authoritative command queue
    */
    [Fact]
    public void ReceiveCommand_ShouldQueueMovementCommandForConnectedClient()
    {
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);
        var command = CreateCommand(clientId, sequence: 1);

        server.ConnectClient(clientId);
        var accepted = server.ReceiveCommand(command);

        Assert.True(accepted);
        Assert.True(server.TryDequeueCommand(clientId, out var dequeued));
        Assert.Equal(command.Sequence, dequeued.Sequence);
        Assert.Equal(command.Movement.MoveDirection, dequeued.Movement.MoveDirection);
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
        server.ReceiveCommand(CreateCommand(firstClient, sequence: 1, moveDirection: Vector2.Right));
        server.ReceiveCommand(CreateCommand(secondClient, sequence: 1, moveDirection: Vector2.Left));

        Assert.True(server.TryDequeueCommand(firstClient, out var firstCommand));
        Assert.True(server.TryDequeueCommand(secondClient, out var secondCommand));
        Assert.Equal(Vector2.Right, firstCommand.Movement.MoveDirection);
        Assert.Equal(Vector2.Left, secondCommand.Movement.MoveDirection);
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

        var accepted = server.ReceiveCommand(CreateCommand(clientId, sequence: 1));

        Assert.False(accepted);
        Assert.False(server.TryDequeueCommand(clientId, out _));
    }

    /*
     PURPOSE:
     Ensure duplicate or older command sequences are rejected.

     DESIGN RULE:
     - Sequence numbers increase per client
     - Server accepts each client command sequence once

     FAILURE MEANS:
     - Duplicate UDP packets may apply input more than once
     - Out-of-order packets may rewind client intent
    */
    [Fact]
    public void ReceiveCommand_ShouldRejectDuplicateOrOlderSequences()
    {
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);

        server.ConnectClient(clientId);
        Assert.True(server.ReceiveCommand(CreateCommand(clientId, sequence: 2)));
        Assert.False(server.ReceiveCommand(CreateCommand(clientId, sequence: 2)));
        Assert.False(server.ReceiveCommand(CreateCommand(clientId, sequence: 1)));
    }

    private static ClientCommandPacket CreateCommand(
        ClientId clientId,
        int sequence,
        Vector2? moveDirection = null)
    {
        return new ClientCommandPacket(
            clientId,
            sequence,
            ClientCommandKind.Movement,
            new MovementCommand(moveDirection ?? Vector2.Up, jumpPressed: false));
    }
}
