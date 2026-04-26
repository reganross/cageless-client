using System.Collections.Generic;
using Godot;
using Xunit;

public class NetworkServerTests
{
    /*
     PURPOSE:
     Ensure the server records snapshots for registered network entities.

     DESIGN RULE:
     - Server owns the entity registry used by snapshot capture
     - Recording a snapshot captures currently registered entity state

     FAILURE MEANS:
     - Server snapshots may miss authoritative entity state
     - Clients may receive incomplete world updates
    */
    [Fact]
    public void RecordSnapshot_ShouldCaptureRegisteredEntities()
    {
        var server = new NetworkServer(historySize: 4);
        var entity = new TestNetworkEntity(new EntityState
        {
            Position = new Vector3(1, 2, 3),
            Rotation = Quaternion.Identity,
            Velocity = new Vector3(4, 5, 6),
            StateFlags = 7
        });

        var entityId = server.RegisterEntity(entity);
        server.RecordSnapshot(tick: 42);

        var snapshot = server.GetLatestSnapshot();
        Assert.Equal(42, snapshot.Tick);
        Assert.True(snapshot.States.ContainsKey(entityId.Value));
        Assert.Equal(entity.CaptureState().Position, snapshot.States[entityId.Value].Position);
    }

    /*
     PURPOSE:
     Ensure recording snapshots does not automatically queue network sends.

     DESIGN RULE:
     - Snapshot recording and outbound sending are separate steps
     - Clients only receive snapshots after the server queues them

     FAILURE MEANS:
     - Server ticks may accidentally send packets
     - Network timing becomes coupled to snapshot capture
    */
    [Fact]
    public void RecordSnapshot_ShouldNotQueueSnapshotForClients()
    {
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);

        server.ConnectClient(clientId);
        server.RecordSnapshot(tick: 42);

        Assert.False(server.TryDequeueSnapshot(clientId, out _));
    }

    /*
     PURPOSE:
     Ensure the server can queue the latest snapshot for connected clients.

     DESIGN RULE:
     - Sending uses the most recently recorded snapshot
     - Connected clients receive queued outbound snapshots

     FAILURE MEANS:
     - Clients may never receive authoritative updates
     - Outbound snapshot queues may contain the wrong frame
    */
    [Fact]
    public void QueueLatestSnapshot_ShouldQueueFrameForConnectedClients()
    {
        var server = new NetworkServer(historySize: 4);
        var clientId = new ClientId(1);

        server.ConnectClient(clientId);
        server.RecordSnapshot(tick: 42);
        server.QueueLatestSnapshot();

        Assert.True(server.TryDequeueSnapshot(clientId, out var snapshot));
        Assert.Equal(42, snapshot.Tick);
    }

    /*
     PURPOSE:
     Ensure each connected client has an independent outbound snapshot queue.

     DESIGN RULE:
     - Draining one client queue must not drain another client queue
     - Each connected client receives its own copy of queued snapshots

     FAILURE MEANS:
     - One client may consume updates intended for another
     - Multiplayer clients may desync from missing snapshots
    */
    [Fact]
    public void TryDequeueSnapshot_ShouldUseIndependentClientQueues()
    {
        var server = new NetworkServer(historySize: 4);
        var firstClient = new ClientId(1);
        var secondClient = new ClientId(2);

        server.ConnectClient(firstClient);
        server.ConnectClient(secondClient);
        server.RecordSnapshot(tick: 42);
        server.QueueLatestSnapshot();

        Assert.True(server.TryDequeueSnapshot(firstClient, out var firstSnapshot));
        Assert.True(server.TryDequeueSnapshot(secondClient, out var secondSnapshot));
        Assert.Equal(42, firstSnapshot.Tick);
        Assert.Equal(42, secondSnapshot.Tick);
    }

    /*
     PURPOSE:
     Ensure unknown clients cannot drain outbound snapshot queues.

     DESIGN RULE:
     - Only connected clients have outbound queues
     - Unknown client access fails predictably without throwing

     FAILURE MEANS:
     - Invalid client ids may hide connection bugs
     - Server send code may crash while flushing clients
    */
    [Fact]
    public void TryDequeueSnapshot_ShouldReturnFalseForUnknownClient()
    {
        var server = new NetworkServer(historySize: 4);

        var foundSnapshot = server.TryDequeueSnapshot(new ClientId(999), out var snapshot);

        Assert.False(foundSnapshot);
        Assert.Equal(default, snapshot);
    }

    /*
     PURPOSE:
     Ensure the persistent server can advance one authoritative network tick.

     DESIGN RULE:
     - Public server tick records the next snapshot
     - Public server tick flushes queued snapshots through its transport

     FAILURE MEANS:
     - Scene nodes cannot drive a persistent server safely
     - Connected clients may not receive fixed-rate updates
    */
    [Fact]
    public void Tick_ShouldRecordAndFlushSnapshotThroughTransport()
    {
        var clientId = new ClientId(1);
        var transport = new FakeServerSnapshotTransport(clientId);
        var server = new NetworkServer(historySize: 4, transport);

        server.Tick();

        var sentSnapshot = Assert.Single(transport.SentSnapshots);
        Assert.Equal(clientId, sentSnapshot.ClientId);
        Assert.Equal(0, sentSnapshot.Snapshot.Tick);
    }

    /*
     PURPOSE:
     Ensure persistent server ticks advance snapshot history.

     DESIGN RULE:
     - Each public server tick records exactly one new snapshot
     - Snapshot ticks increase monotonically

     FAILURE MEANS:
     - Clients may receive duplicate snapshot ticks
     - Reconciliation history may be ordered incorrectly
    */
    [Fact]
    public void Tick_ShouldAdvanceSnapshotTickEachCall()
    {
        var transport = new FakeServerSnapshotTransport(new ClientId(1));
        var server = new NetworkServer(historySize: 4, transport);

        server.Tick();
        server.Tick();

        Assert.Equal(2, transport.SentSnapshots.Count);
        Assert.Equal(0, transport.SentSnapshots[0].Snapshot.Tick);
        Assert.Equal(1, transport.SentSnapshots[1].Snapshot.Tick);
    }

    /*
     PURPOSE:
     Ensure the server sends a full snapshot before delta snapshots.

     DESIGN RULE:
     - First outbound snapshot establishes the client baseline
     - Later snapshots may be deltas against that baseline

     FAILURE MEANS:
     - Clients may receive deltas without a complete starting state
     - Entity state reconstruction may fail on connection
    */
    [Fact]
    public void Tick_ShouldSendFullSnapshotFirst()
    {
        var transport = new FakeServerSnapshotTransport(new ClientId(1));
        var server = new NetworkServer(historySize: 4, transport);

        server.Tick();

        Assert.Equal(SnapshotPacketKind.Full, transport.SentSnapshots[0].Packet.Kind);
    }

    /*
     PURPOSE:
     Ensure unchanged entities are omitted from normal delta sends.

     DESIGN RULE:
     - First tick sends a full snapshot
     - Next tick sends only changed entities

     FAILURE MEANS:
     - Server may send full entity state every update
     - Bandwidth usage may scale poorly with total entity count
    */
    [Fact]
    public void Tick_ShouldSendDeltaWithoutUnchangedEntitiesAfterBaseline()
    {
        var transport = new FakeServerSnapshotTransport(new ClientId(1));
        var server = new NetworkServer(historySize: 4, transport);
        server.RegisterEntity(new TestNetworkEntity(new EntityState
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity
        }));

        server.Tick();
        server.Tick();

        Assert.Equal(SnapshotPacketKind.Delta, transport.SentSnapshots[1].Packet.Kind);
        Assert.Empty(transport.SentSnapshots[1].Snapshot.States);
    }

    /*
     PURPOSE:
     Ensure periodic full snapshots are sent even when entities are unchanged.

     DESIGN RULE:
     - Full snapshots are forced on a configured interval
     - Full snapshots include unchanged entities for recovery

     FAILURE MEANS:
     - Clients may never recover from dropped UDP delta packets
     - Late clients may lack a complete authoritative state
    */
    [Fact]
    public void Tick_ShouldForcePeriodicFullSnapshots()
    {
        var transport = new FakeServerSnapshotTransport(new ClientId(1));
        var server = new NetworkServer(
            historySize: 4,
            transport,
            new SnapshotDeltaPolicy(),
            fullSnapshotInterval: 2);
        server.RegisterEntity(new TestNetworkEntity(new EntityState
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity
        }));

        server.Tick();
        server.Tick();
        server.Tick();

        Assert.Equal(SnapshotPacketKind.Full, transport.SentSnapshots[0].Packet.Kind);
        Assert.Equal(SnapshotPacketKind.Delta, transport.SentSnapshots[1].Packet.Kind);
        Assert.Equal(SnapshotPacketKind.Full, transport.SentSnapshots[2].Packet.Kind);
        Assert.Single(transport.SentSnapshots[2].Snapshot.States);
    }

    /*
     PURPOSE:
     Ensure network update rate is tracked per client connection.

     DESIGN RULE:
     - Client activity state controls only that client's send cadence
     - Combat clients receive updates more frequently than idle clients

     FAILURE MEANS:
     - One client's activity may force bandwidth usage for every client
     - Idle clients may receive unnecessary combat-rate updates
    */
    [Fact]
    public void Tick_ShouldApplyActivityRatePerClient()
    {
        var combatClient = new ClientId(1);
        var idleClient = new ClientId(2);
        var transport = new FakeServerSnapshotTransport(combatClient, idleClient);
        var server = new NetworkServer(
            historySize: 4,
            transport,
            new SnapshotDeltaPolicy(),
            fullSnapshotInterval: 10,
            new NetworkTickRatePolicy(
                inCombatIntervalSeconds: 0.05,
                exploringIntervalSeconds: 0.1,
                idleIntervalSeconds: 0.5));

        server.SetClientActivityState(combatClient, NetworkActivityState.InCombat);
        server.SetClientActivityState(idleClient, NetworkActivityState.Idle);
        server.Tick(delta: 0.1);

        Assert.Equal(2, transport.CountSentTo(combatClient));
        Assert.Equal(0, transport.CountSentTo(idleClient));
    }

    /*
     PURPOSE:
     Ensure changing one client's activity state changes only that client's cadence.

     DESIGN RULE:
     - Activity state is stored per connection
     - Existing accumulated time can be used when a client enters a faster state

     FAILURE MEANS:
     - Per-client callback state may be ignored
     - Network rate changes may affect unrelated clients
    */
    [Fact]
    public void Tick_ShouldUseUpdatedActivityStatePerClient()
    {
        var clientId = new ClientId(1);
        var transport = new FakeServerSnapshotTransport(clientId);
        var server = new NetworkServer(
            historySize: 4,
            transport,
            new SnapshotDeltaPolicy(),
            fullSnapshotInterval: 10,
            new NetworkTickRatePolicy(
                inCombatIntervalSeconds: 0.05,
                exploringIntervalSeconds: 0.1,
                idleIntervalSeconds: 0.5));

        server.SetClientActivityState(clientId, NetworkActivityState.Idle);
        server.Tick(delta: 0.1);
        server.SetClientActivityState(clientId, NetworkActivityState.InCombat);
        server.Tick(delta: 0);

        Assert.Equal(2, transport.CountSentTo(clientId));
    }

    private sealed class TestNetworkEntity : INetworkEntity
    {
        public TestNetworkEntity(EntityState state)
        {
            State = state;
        }

        public EntityState State { get; set; }

        public EntityId Id { get; } = new(0);

        public EntityState CaptureState() => State;
    }

    private sealed class FakeServerSnapshotTransport : IServerSnapshotTransport
    {
        private readonly List<ClientId> connectedClients = new();

        public FakeServerSnapshotTransport(params ClientId[] clients)
        {
            connectedClients.AddRange(clients);
        }

        public IReadOnlyCollection<ClientId> ConnectedClients => connectedClients;

        public List<SentSnapshot> SentSnapshots { get; } = new();

        public void Start(int port)
        {
        }

        public void SendSnapshot(ClientId clientId, SnapshotPacket packet)
        {
            SentSnapshots.Add(new SentSnapshot(clientId, packet));
        }

        public int CountSentTo(ClientId clientId)
        {
            int count = 0;
            foreach (var sentSnapshot in SentSnapshots)
            {
                if (sentSnapshot.ClientId.Equals(clientId))
                {
                    count++;
                }
            }

            return count;
        }

        public void Dispose()
        {
        }
    }

    private readonly struct SentSnapshot
    {
        public SentSnapshot(ClientId clientId, SnapshotPacket packet)
        {
            ClientId = clientId;
            Packet = packet;
        }

        public ClientId ClientId { get; }
        public SnapshotPacket Packet { get; }
        public SnapshotFrame Snapshot => Packet.Frame;
    }
}
