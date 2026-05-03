using System.Collections.Generic;
using Xunit;

public class NetworkServerRuntimeTests
{
    /*
     PURPOSE:
     Ensure runtime polls incoming client packets before ticking snapshots.

     DESIGN RULE:
     - Client ingress is processed before the authoritative snapshot tick
     - Newly connected clients can receive the next server snapshot immediately

     FAILURE MEANS:
     - Controller packets may miss the current simulation tick
     - New clients may wait an extra tick before receiving snapshots
    */
    [Fact]
    public void Tick_ShouldProcessIncomingBeforeSnapshotTick()
    {
        var clientId = new ClientId(1);
        var transport = new FakeRuntimeTransport((server, fakeTransport) =>
        {
            server.ConnectClient(clientId);
            fakeTransport.Connect(clientId);
        });
        var server = new NetworkServer(historySize: 4, transport);
        using var runtime = new NetworkServerRuntime(
            server,
            transport,
            snapshotIntervalSeconds: 0.1);

        runtime.Tick(delta: 0.1);

        Assert.Equal(1, transport.ProcessIncomingCalls);
        var sentSnapshot = Assert.Single(transport.SentSnapshots);
        Assert.Equal(clientId, sentSnapshot.ClientId);
    }

    /*
     PURPOSE:
     Ensure runtime owns transport startup and cleanup.

     DESIGN RULE:
     - Runtime starts its transport on the configured port
     - Runtime disposes transport resources when stopped

     FAILURE MEANS:
     - UDP sockets may never bind before gameplay starts
     - Server sockets may leak across scenes
    */
    [Fact]
    public void StartAndDispose_ShouldManageTransportLifetime()
    {
        var transport = new FakeRuntimeTransport();
        var server = new NetworkServer(historySize: 4, transport);
        var runtime = new NetworkServerRuntime(
            server,
            transport,
            snapshotIntervalSeconds: 0.1);

        runtime.Start(port: 4444);
        runtime.Dispose();

        Assert.Equal(4444, transport.StartedPort);
        Assert.True(transport.Disposed);
    }

    /*
     PURPOSE:
     Ensure scenes can start a server through a plain C# host object.

     DESIGN RULE:
     - Server lifetime is controlled by a non-Node host
     - Scene scripts can access the pure NetworkServer for registration and activity state

     FAILURE MEANS:
     - Server ownership may remain tied to Godot node lifecycle
     - Scenes may not be able to start/stop networking explicitly
    */
    [Fact]
    public void Start_ShouldCreatePlainServerHostAndExposeServer()
    {
        var transport = new FakeRuntimeTransport();

        using var host = NetworkServerHost.Start(
            transport,
            port: 7777,
            snapshotIntervalSeconds: 0.1,
            historySize: 4);

        Assert.NotNull(host.Server);
        Assert.Equal(7777, transport.StartedPort);
    }

    /*
     PURPOSE:
     Ensure plain host ticks delegate to runtime behavior.

     DESIGN RULE:
     - Host Tick polls ingress before driving snapshot ticks
     - Host preserves the same runtime behavior without a Node wrapper

     FAILURE MEANS:
     - Scene-owned server host may not process real network traffic
     - Host may diverge from tested runtime ordering
    */
    [Fact]
    public void Tick_ShouldDelegateToRuntime()
    {
        var clientId = new ClientId(1);
        var transport = new FakeRuntimeTransport((server, fakeTransport) =>
        {
            server.ConnectClient(clientId);
            fakeTransport.Connect(clientId);
        });
        using var host = NetworkServerHost.Start(
            transport,
            port: 7777,
            snapshotIntervalSeconds: 0.1,
            historySize: 4);

        host.Tick(delta: 0.1);

        Assert.Equal(1, transport.ProcessIncomingCalls);
        Assert.Single(transport.SentSnapshots);
    }

    /*
     PURPOSE:
     Ensure a scene-owned tick clock can drive the server host.

     DESIGN RULE:
     - The host can consume ticks from a shared NetworkTickClock
     - The host must not require its own clock advancer

     FAILURE MEANS:
     - Server timing may be forced onto a separate hidden clock
     - A scene-level network tick driver cannot coordinate server ticks
    */
    [Fact]
    public void Tick_ShouldUseExternallyAdvancedClock()
    {
        var clientId = new ClientId(1);
        var clock = new NetworkTickClock(tickIntervalSeconds: 0.1);
        using var advancer = clock.CreateAdvancer();
        var transport = new FakeRuntimeTransport((server, fakeTransport) =>
        {
            server.ConnectClient(clientId);
            fakeTransport.Connect(clientId);
        });
        using var host = NetworkServerHost.Start(
            transport,
            port: 7777,
            tickClock: clock,
            historySize: 4);

        advancer.Advance(0.1);
        host.Tick(delta: 0);

        Assert.Equal(1, transport.ProcessIncomingCalls);
        Assert.Single(transport.SentSnapshots);
    }

    /*
     PURPOSE:
     Ensure host construction does not take over clock advancement when a clock is supplied.

     DESIGN RULE:
     - The scene-level driver owns the clock advancer
     - Server host only consumes pending ticks from the provided clock

     FAILURE MEANS:
     - Creating a host may fail when the scene already owns the NetworkTickClock advancer
     - Multiple objects may fight over clock advancement ownership
    */
    [Fact]
    public void Start_ShouldAcceptClockThatAlreadyHasAdvancer()
    {
        var clock = new NetworkTickClock(tickIntervalSeconds: 0.1);
        using var advancer = clock.CreateAdvancer();
        var transport = new FakeRuntimeTransport();

        using var host = NetworkServerHost.Start(
            transport,
            port: 7777,
            tickClock: clock,
            historySize: 4);

        Assert.NotNull(host.Server);
        Assert.Equal(7777, transport.StartedPort);
    }

    private sealed class FakeRuntimeTransport : IServerNetworkTransport
    {
        private readonly System.Action<NetworkServer, FakeRuntimeTransport> processIncoming;
        private readonly List<ClientId> connectedClients = new();

        public FakeRuntimeTransport()
            : this((_, _) => { })
        {
        }

        public FakeRuntimeTransport(System.Action<NetworkServer, FakeRuntimeTransport> processIncoming)
        {
            this.processIncoming = processIncoming;
        }

        public IReadOnlyCollection<ClientId> ConnectedClients => connectedClients;
        public List<SentSnapshot> SentSnapshots { get; } = new();
        public int ProcessIncomingCalls { get; private set; }
        public int StartedPort { get; private set; }
        public bool Disposed { get; private set; }

        public void Start(int port)
        {
            StartedPort = port;
        }

        public int ProcessIncoming(NetworkServer server)
        {
            ProcessIncomingCalls++;
            processIncoming(server, this);
            return 1;
        }

        public void Connect(ClientId clientId)
        {
            if (!connectedClients.Contains(clientId))
            {
                connectedClients.Add(clientId);
            }
        }

        public void SendSnapshot(ClientId clientId, SnapshotPacket packet)
        {
            SentSnapshots.Add(new SentSnapshot(clientId, packet));
        }

        public void Dispose()
        {
            Disposed = true;
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
    }
}
