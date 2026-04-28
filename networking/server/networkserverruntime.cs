using System;

public sealed class NetworkServerRuntime : IDisposable
{
    private readonly IServerNetworkTransport transport;
    private readonly NetworkServerTickDriver tickDriver;
    private bool disposed;

    public NetworkServerRuntime(
        NetworkServer server,
        IServerNetworkTransport transport,
        double snapshotIntervalSeconds)
        : this(
            server,
            transport,
            new NetworkServerTickDriver(server, snapshotIntervalSeconds))
    {
    }

    public NetworkServerRuntime(
        NetworkServer server,
        IServerNetworkTransport transport,
        NetworkTickClock tickClock)
        : this(
            server,
            transport,
            new NetworkServerTickDriver(
                server ?? throw new ArgumentNullException(nameof(server)),
                tickClock ?? throw new ArgumentNullException(nameof(tickClock))))
    {
    }

    private NetworkServerRuntime(
        NetworkServer server,
        IServerNetworkTransport transport,
        NetworkServerTickDriver tickDriver)
    {
        Server = server ?? throw new ArgumentNullException(nameof(server));
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.tickDriver = tickDriver ?? throw new ArgumentNullException(nameof(tickDriver));
    }

    public NetworkServer Server { get; }

    public void Start(int port)
    {
        ThrowIfDisposed();
        transport.Start(port);
    }

    public void Tick(double delta)
    {
        ThrowIfDisposed();
        transport.ProcessIncoming(Server);
        tickDriver.Tick(delta);
        Server.FlushSnapshots(transport);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        tickDriver.Dispose();
        transport.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(NetworkServerRuntime));
        }
    }
}
