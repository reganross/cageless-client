using System;

public sealed class NetworkServerHost : IDisposable
{
    private readonly NetworkServerRuntime runtime;
    private bool disposed;

    private NetworkServerHost(NetworkServerRuntime runtime)
    {
        this.runtime = runtime;
        Server = runtime.Server;
    }

    public NetworkServer Server { get; }

    public static NetworkServerHost StartUdp(
        int port,
        double snapshotIntervalSeconds = 0.05,
        int historySize = 64)
    {
        return Start(
            new UdpServerTransport(),
            port,
            snapshotIntervalSeconds,
            historySize);
    }

    public static NetworkServerHost StartUdp(
        int port,
        NetworkTickClock tickClock,
        int historySize = 64)
    {
        return Start(
            new UdpServerTransport(),
            port,
            tickClock,
            historySize);
    }

    public static NetworkServerHost Start(
        IServerNetworkTransport transport,
        int port,
        double snapshotIntervalSeconds = 0.05,
        int historySize = 64)
    {
        if (transport == null)
        {
            throw new ArgumentNullException(nameof(transport));
        }

        var server = new NetworkServer(historySize, transport);
        var runtime = new NetworkServerRuntime(server, transport, snapshotIntervalSeconds);
        runtime.Start(port);
        return new NetworkServerHost(runtime);
    }

    public static NetworkServerHost Start(
        IServerNetworkTransport transport,
        int port,
        NetworkTickClock tickClock,
        int historySize = 64)
    {
        if (transport == null)
        {
            throw new ArgumentNullException(nameof(transport));
        }

        if (tickClock == null)
        {
            throw new ArgumentNullException(nameof(tickClock));
        }

        var server = new NetworkServer(historySize, transport);
        var runtime = new NetworkServerRuntime(server, transport, tickClock);
        runtime.Start(port);
        return new NetworkServerHost(runtime);
    }

    public void Tick(double delta)
    {
        ThrowIfDisposed();
        runtime.Tick(delta);
    }

    public void Stop()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        runtime.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(NetworkServerHost));
        }
    }
}
