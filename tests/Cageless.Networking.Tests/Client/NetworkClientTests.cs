using System.Collections.Generic;
using System.IO;
using Godot;
using Xunit;

public class NetworkClientTests
{
    /*
     PURPOSE:
     Ensure the client can establish its local connection identity.

     DESIGN RULE:
     - Connect stores the assigned client id
     - Connect sends an explicit connect signal through the transport

     FAILURE MEANS:
     - Client controller packets may not be tied to a server player
     - Server may never learn that the client wants to join
    */
    [Fact]
    public void Connect_ShouldAssignClientIdAndSendConnectSignal()
    {
        var transport = new FakeClientTransport();
        var client = new NetworkClient(transport);
        var clientId = new ClientId(7);

        var connected = client.Connect(clientId);

        Assert.True(connected);
        Assert.True(client.IsConnected);
        Assert.Equal(clientId, client.ClientId);
        Assert.True(client.Controller.HasPlayerId);
        Assert.Equal(clientId, client.Controller.PlayerId);
        Assert.Equal(ClientPacketKind.Connect, ReadKind(transport.Sent[0]));
    }

    /*
     PURPOSE:
     Ensure changed controller state is sent on a 20Hz tick.

     DESIGN RULE:
     - Controller updates are serialized as delta command packets
     - Controller ticks advance at 20Hz

     FAILURE MEANS:
     - Server may not receive changed client input intent
     - Client tick cadence may drift from the expected simulation rate
    */
    [Fact]
    public void Tick_ShouldSendDeltaWhenControllerStateChanges()
    {
        var transport = new FakeClientTransport();
        var client = new NetworkClient(transport);

        client.Connect(new ClientId(3));
        client.Controller.SetActionStrength("forward", 1);
        client.Controller.SetLookRotation(1.25f, -0.5f);

        var sent = client.Tick(0.05);
        var command = ReadCommand(transport.Sent[1]);

        Assert.Equal(1, sent);
        Assert.Equal(ClientPacketKind.Controller, ReadKind(transport.Sent[1]));
        Assert.Equal(ControllerPacketKind.Delta, command.ControllerPacketKind);
        Assert.Equal(new ClientId(3), command.ClientId);
        Assert.Equal(1, command.Tick);
        Assert.Equal(1, command.Controller.GetActionStrength("forward"));
        Assert.True(command.HasLookRotation);
        Assert.Equal(1.25f, command.Controller.LookYaw);
        Assert.Equal(-0.5f, command.Controller.LookPitch);
    }

    /*
     PURPOSE:
     Ensure controller ticks advance at a 20Hz cadence.

     DESIGN RULE:
     - Less than 50ms does not advance a controller tick
     - One 50ms interval advances one controller tick

     FAILURE MEANS:
     - Client may send input faster or slower than intended
     - Server command ticks may not match client simulation ticks
    */
    [Fact]
    public void Tick_ShouldUseTwentyHertzCadence()
    {
        var transport = new FakeClientTransport();
        var client = new NetworkClient(transport);

        client.Connect(new ClientId(5));
        client.Controller.SetActionStrength("forward", 1);

        Assert.Equal(0, client.Tick(0.049));
        Assert.Single(transport.Sent);

        Assert.Equal(1, client.Tick(0.001));
        Assert.Equal(1, ReadCommand(transport.Sent[1]).Tick);
    }

    /*
     PURPOSE:
     Ensure the client can consume ticks from an externally driven clock.

     DESIGN RULE:
     - Local player physics can advance the network tick clock
     - NetworkClient sends packets only when it processes pending ticks

     FAILURE MEANS:
     - Player physics and networking may use separate tick sources
     - NetworkClient may be forced to own the local physics clock
    */
    [Fact]
    public void ProcessPendingTicks_ShouldUseExternallyAdvancedClock()
    {
        var transport = new FakeClientTransport();
        var clock = new NetworkTickClock();
        var client = new NetworkClient(transport, clock);

        client.Connect(new ClientId(5));
        client.Controller.SetActionStrength("forward", 1);
        var advancer = clock.CreateAdvancer();
        advancer.Advance(0.05);

        Assert.Single(transport.Sent);
        Assert.Equal(1, client.ProcessPendingTicks());
        Assert.Equal(1, ReadCommand(transport.Sent[1]).Tick);
    }

    /*
     PURPOSE:
     Ensure externally owned clocks are not reset by client connection lifecycle.

     DESIGN RULE:
     - A shared scene-level clock can outlive one client object
     - NetworkClient consumes future ticks without rewinding shared time

     FAILURE MEANS:
     - A client connect/disconnect could desync other systems using the same tick driver
     - Server and client code could disagree on the current network tick
    */
    [Fact]
    public void Connect_ShouldNotResetExternallyOwnedClock()
    {
        var transport = new FakeClientTransport();
        var clock = new NetworkTickClock();
        var advancer = clock.CreateAdvancer();
        advancer.Advance(0.15);
        var client = new NetworkClient(transport, clock);

        client.Connect(new ClientId(5));
        client.Controller.SetActionStrength("forward", 1);
        advancer.Advance(0.05);

        Assert.Equal(4, clock.CurrentTick);
        Assert.Equal(1, client.ProcessPendingTicks());
        Assert.Equal(4, ReadCommand(transport.Sent[1]).Tick);
    }

    /*
     PURPOSE:
     Ensure unchanged controller state is not sent every tick.

     DESIGN RULE:
     - Normal 20Hz ticks send only when controller state changes
     - Full controller packets refresh state every 250ms

     FAILURE MEANS:
     - Client may waste bandwidth sending unchanged controller state
     - Server may not receive periodic full controller baselines
    */
    [Fact]
    public void Tick_ShouldSkipUnchangedStateUntilFullRefresh()
    {
        var transport = new FakeClientTransport();
        var client = new NetworkClient(transport);

        client.Connect(new ClientId(5));

        Assert.Equal(0, client.Tick(0.05));
        Assert.Equal(0, client.Tick(0.05));
        Assert.Equal(0, client.Tick(0.05));
        Assert.Equal(0, client.Tick(0.05));
        Assert.Single(transport.Sent);

        Assert.Equal(1, client.Tick(0.05));
        var command = ReadCommand(transport.Sent[1]);
        Assert.Equal(5, command.Tick);
        Assert.Equal(ControllerPacketKind.Full, command.ControllerPacketKind);
    }

    /*
     PURPOSE:
     Ensure delta packets contain only changed controller fields.

     DESIGN RULE:
     - Delta packets include changed action strengths
     - Unchanged look rotation is omitted from the delta packet

     FAILURE MEANS:
     - Delta packets may grow toward full controller packet size
     - Server may overwrite unchanged fields unnecessarily
    */
    [Fact]
    public void Tick_ShouldSendOnlyChangedFieldsInDeltaPacket()
    {
        var transport = new FakeClientTransport();
        var client = new NetworkClient(transport);

        client.Connect(new ClientId(6));
        client.Controller.SetActionStrength("forward", 1);
        client.Tick(0.05);
        client.Controller.SetActionStrength("right", 1);

        client.Tick(0.05);
        var command = ReadCommand(transport.Sent[2]);

        Assert.Equal(ControllerPacketKind.Delta, command.ControllerPacketKind);
        Assert.False(command.HasLookRotation);
        Assert.Equal(1, command.Controller.GetActionStrength("right"));
        Assert.Equal(0, command.Controller.GetActionStrength("forward"));
        Assert.Single(command.Controller.Actions);
    }

    /*
     PURPOSE:
     Ensure snapshots from the server are received into client state.

     DESIGN RULE:
     - Client drains snapshot bytes from its transport
     - Latest valid snapshot packet is retained for rendering/simulation

     FAILURE MEANS:
     - Server state updates may never reach client-side state
     - Client may render stale world state indefinitely
    */
    [Fact]
    public void ReceiveSnapshots_ShouldStoreLatestSnapshotPacket()
    {
        var transport = new FakeClientTransport();
        var client = new NetworkClient(transport);
        var packet = new SnapshotPacket(
            SnapshotPacketKind.Full,
            new SnapshotFrame
            {
                Tick = 12,
                States = new Dictionary<int, EntityState>
                {
                    [4] = new EntityState
                    {
                        Position = new Vector3(1, 2, 3),
                        Rotation = Quaternion.Identity,
                        Velocity = Vector3.Zero,
                        StateFlags = 9
                    }
                }
            });

        transport.QueueIncoming(SnapshotSerializer.Serialize(packet));
        var received = client.ReceiveSnapshots();

        Assert.Equal(1, received);
        Assert.True(client.TryGetLatestSnapshot(out var latest));
        Assert.Equal(SnapshotPacketKind.Full, latest.Kind);
        Assert.Equal(12, latest.Frame.Tick);
        Assert.Equal(new Vector3(1, 2, 3), latest.Frame.States[4].Position);
    }

    /*
     PURPOSE:
     Ensure corrupt snapshot bytes do not replace valid client state.

     DESIGN RULE:
     - Malformed snapshot packets are rejected predictably
     - Last valid server snapshot remains available

     FAILURE MEANS:
     - A corrupt UDP payload may erase useful world state
     - Client snapshot handling may fail unpredictably
    */
    [Fact]
    public void ReceiveSnapshots_ShouldIgnoreCorruptSnapshotBytes()
    {
        var transport = new FakeClientTransport();
        var client = new NetworkClient(transport);
        var valid = new SnapshotPacket(
            SnapshotPacketKind.Full,
            new SnapshotFrame
            {
                Tick = 3,
                States = new Dictionary<int, EntityState>()
            });

        transport.QueueIncoming(SnapshotSerializer.Serialize(valid));
        transport.QueueIncoming(new byte[] { 1, 2 });

        var received = client.ReceiveSnapshots();

        Assert.Equal(1, received);
        Assert.True(client.TryGetLatestSnapshot(out var latest));
        Assert.Equal(3, latest.Frame.Tick);
    }

    /*
     PURPOSE:
     Ensure disconnected clients do not send controller packets.

     DESIGN RULE:
     - Controller sends require an active connection
     - Local-only controllers are not serialized directly

     FAILURE MEANS:
     - Server may receive packets from clients without identities
     - Client may throw while sending before connect
    */
    [Fact]
    public void Tick_ShouldRejectDisconnectedClient()
    {
        var transport = new FakeClientTransport();
        var client = new NetworkClient(transport);

        Assert.Equal(0, client.Tick(0.05));
        Assert.Empty(transport.Sent);
    }

    /*
     PURPOSE:
     Ensure clients can signal an intentional disconnect.

     DESIGN RULE:
     - Disconnect sends an explicit disconnect packet
     - Disconnected clients clear their connection state

     FAILURE MEANS:
     - Server may keep disconnected clients registered
     - Client may continue sending commands after leaving
    */
    [Fact]
    public void Disconnect_ShouldSendDisconnectSignalAndClearConnection()
    {
        var transport = new FakeClientTransport();
        var client = new NetworkClient(transport);

        client.Connect(new ClientId(8));
        var disconnected = client.Disconnect();

        Assert.True(disconnected);
        Assert.False(client.IsConnected);
        Assert.Equal(ClientPacketKind.Disconnect, ReadKind(transport.Sent[1]));
        Assert.False(client.SendControllerUpdate());
    }

    private static ClientPacketKind ReadKind(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        return (ClientPacketKind)reader.ReadInt32();
    }

    private static ClientCommandPacket ReadCommand(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        Assert.Equal(ClientPacketKind.Controller, (ClientPacketKind)reader.ReadInt32());
        int payloadLength = reader.ReadInt32();
        return ClientCommandSerializer.Deserialize(reader.ReadBytes(payloadLength));
    }

    private sealed class FakeClientTransport : IClientTransport
    {
        private readonly Queue<byte[]> incoming = new();

        public List<byte[]> Sent { get; } = new();

        public void Send(byte[] bytes)
        {
            Sent.Add(bytes);
        }

        public bool TryReceive(out byte[] bytes)
        {
            if (incoming.Count == 0)
            {
                bytes = System.Array.Empty<byte>();
                return false;
            }

            bytes = incoming.Dequeue();
            return true;
        }

        public void QueueIncoming(byte[] bytes)
        {
            incoming.Enqueue(bytes);
        }

        public void Dispose()
        {
        }
    }
}
