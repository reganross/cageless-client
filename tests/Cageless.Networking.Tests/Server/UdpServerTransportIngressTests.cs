using System.IO;
using System.Net;
using Xunit;

public class UdpServerTransportIngressTests
{
    /*
     PURPOSE:
     Ensure server transport can parse incoming connection packets.

     DESIGN RULE:
     - Client-provided ids are accepted for now
     - Accepted connect packets register the sender endpoint and server client state

     FAILURE MEANS:
     - UDP clients may never enter the server connection list
     - Snapshot sends may not know where to send packets
    */
    [Fact]
    public void ProcessClientPacket_ShouldConnectClientEndpoint()
    {
        using var transport = new UdpServerTransport();
        var server = new NetworkServer(historySize: 4);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);

        var processed = transport.ProcessClientPacket(
            server,
            CreateClientIdPacket(ClientPacketKind.Connect, new ClientId(1)),
            endpoint);

        Assert.True(processed);
        Assert.Contains(new ClientId(1), transport.ConnectedClients);
        Assert.True(server.ReceiveCommand(CreateCommand(new ClientId(1), tick: 1)));
    }

    /*
     PURPOSE:
     Ensure controller datagrams enter authoritative server command handling.

     DESIGN RULE:
     - Controller payloads are decoded with ClientCommandSerializer
     - Accepted commands update the server controller manager

     FAILURE MEANS:
     - Client input may never affect server-side controller state
     - UDP ingress may bypass command tick validation
    */
    [Fact]
    public void ProcessClientPacket_ShouldApplyControllerCommand()
    {
        using var transport = new UdpServerTransport();
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);

        transport.ProcessClientPacket(server, CreateClientIdPacket(ClientPacketKind.Connect, clientId), endpoint);
        var processed = transport.ProcessClientPacket(
            server,
            CreateControllerPacket(CreateCommand(clientId, tick: 1, actionName: "right")),
            endpoint);

        Assert.True(processed);
        Assert.True(server.Controllers.TryGet(clientId, out var controller));
        Assert.Equal(1, controller.GetActionStrength("right"));
    }

    /*
     PURPOSE:
     Ensure disconnect datagrams remove server connection state.

     DESIGN RULE:
     - Disconnect packets unregister transport endpoints
     - Server-side queues/controllers are removed for that client

     FAILURE MEANS:
     - Disconnected clients may keep receiving snapshots
     - Stale controllers may remain in authoritative simulation
    */
    [Fact]
    public void ProcessClientPacket_ShouldDisconnectClientEndpoint()
    {
        using var transport = new UdpServerTransport();
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);

        transport.ProcessClientPacket(server, CreateClientIdPacket(ClientPacketKind.Connect, clientId), endpoint);
        var processed = transport.ProcessClientPacket(
            server,
            CreateClientIdPacket(ClientPacketKind.Disconnect, clientId),
            endpoint);

        Assert.True(processed);
        Assert.DoesNotContain(clientId, transport.ConnectedClients);
        Assert.False(server.ReceiveCommand(CreateCommand(clientId, tick: 1)));
    }

    private static byte[] CreateClientIdPacket(ClientPacketKind kind, ClientId clientId)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((int)kind);
        writer.Write(clientId.Value);
        return stream.ToArray();
    }

    private static byte[] CreateControllerPacket(ClientCommandPacket command)
    {
        var payload = ClientCommandSerializer.Serialize(command);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((int)ClientPacketKind.Controller);
        writer.Write(payload.Length);
        writer.Write(payload);
        return stream.ToArray();
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
