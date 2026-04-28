using System.Collections.Generic;
using Xunit;

public class NetworkServerNodeTests
{
    /*
     PURPOSE:
     Ensure the Godot server wrapper waits for the configured snapshot interval.

     DESIGN RULE:
     - Physics frames accumulate elapsed time
     - Snapshots are recorded only when the configured interval is reached

     FAILURE MEANS:
     - Server may send snapshots too frequently
     - Network traffic may become tied to frame rate
    */
    [Fact]
    public void Tick_ShouldNotRecordSnapshotBeforeIntervalElapses()
    {
        var transport = new FakeServerSnapshotTransport(new ClientId(1));
        var driver = CreateDriver(transport, snapshotIntervalSeconds: 0.1);

        driver.Tick(delta: 0.05);

        Assert.Empty(transport.SentSnapshots);
    }

    /*
     PURPOSE:
     Ensure the Godot server wrapper records snapshots at the configured interval.

     DESIGN RULE:
     - Reaching the interval records one authoritative snapshot
     - Recorded snapshots are queued and flushed to connected clients

     FAILURE MEANS:
     - Clients may not receive regular authoritative updates
     - Snapshot timing may drift from the configured server rate
    */
    [Fact]
    public void Tick_ShouldRecordAndFlushSnapshotWhenIntervalElapses()
    {
        var transport = new FakeServerSnapshotTransport(new ClientId(1));
        var driver = CreateDriver(transport, snapshotIntervalSeconds: 0.1);

        driver.Tick(delta: 0.1);

        Assert.Single(transport.SentSnapshots);
        Assert.Equal(0, transport.SentSnapshots[0].Snapshot.Tick);
    }

    /*
     PURPOSE:
     Ensure large physics deltas catch up missed snapshot intervals.

     DESIGN RULE:
     - One physics frame may record multiple fixed snapshots
     - Snapshot ticks advance once per recorded interval

     FAILURE MEANS:
     - Server may drop snapshots during frame spikes
     - Clients may receive uneven history for reconciliation
    */
    [Fact]
    public void Tick_ShouldRecordMultipleSnapshotsForLargeDelta()
    {
        var transport = new FakeServerSnapshotTransport(new ClientId(1));
        var driver = CreateDriver(transport, snapshotIntervalSeconds: 0.1);

        driver.Tick(delta: 0.35);

        Assert.Equal(3, transport.SentSnapshots.Count);
        Assert.Equal(0, transport.SentSnapshots[0].Snapshot.Tick);
        Assert.Equal(1, transport.SentSnapshots[1].Snapshot.Tick);
        Assert.Equal(2, transport.SentSnapshots[2].Snapshot.Tick);
    }

    /*
     PURPOSE:
     Ensure queued snapshots are flushed through the server transport.

     DESIGN RULE:
     - The wrapper sends snapshots through the transport abstraction
     - Transport clients are connected to the pure server before sending

     FAILURE MEANS:
     - UDP transport may never receive snapshots to send
     - Connected clients may be missing from the server queue
    */
    [Fact]
    public void Tick_ShouldFlushQueuedSnapshotsThroughTransport()
    {
        var clientId = new ClientId(7);
        var transport = new FakeServerSnapshotTransport(clientId);
        var driver = CreateDriver(transport, snapshotIntervalSeconds: 0.1);

        driver.Tick(delta: 0.1);

        var sentSnapshot = Assert.Single(transport.SentSnapshots);
        Assert.Equal(clientId, sentSnapshot.ClientId);
        Assert.Equal(0, sentSnapshot.Snapshot.Tick);
    }

    /*
     PURPOSE:
     Ensure the server tick driver can use an externally owned network clock.

     DESIGN RULE:
     - Server and client timing use the same NetworkTickClock type
     - The driver consumes pending ticks requested from that clock

     FAILURE MEANS:
     - Server snapshot timing may diverge from shared clock behavior
     - Server tick driver may keep separate ad hoc accumulation logic
    */
    [Fact]
    public void ProcessPendingTicks_ShouldUseExternallyAdvancedClock()
    {
        var transport = new FakeServerSnapshotTransport(new ClientId(1));
        var server = new NetworkServer(historySize: 4, transport);
        var clock = new NetworkTickClock(tickIntervalSeconds: 0.1);
        var driver = new NetworkServerTickDriver(server, clock);
        var advancer = clock.CreateAdvancer();

        advancer.Advance(0.1);
        driver.ProcessPendingTicks();

        Assert.Single(transport.SentSnapshots);
        Assert.Equal(0, transport.SentSnapshots[0].Snapshot.Tick);
    }

    private static NetworkServerTickDriver CreateDriver(
        FakeServerSnapshotTransport transport,
        double snapshotIntervalSeconds)
    {
        var server = new NetworkServer(historySize: 4, transport);

        return new NetworkServerTickDriver(
            server,
            snapshotIntervalSeconds);
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
