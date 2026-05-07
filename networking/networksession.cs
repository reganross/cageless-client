using System;
using System.Collections.Generic;

public enum NetworkSessionMode
{
    Disconnected,
    Host,
    Client
}

public static class NetworkSession
{
    public const string DefaultHost = "71.91.25.119";
    public const int DefaultPort = 7777;

    public static NetworkSessionMode Mode { get; private set; } = NetworkSessionMode.Disconnected;
    public static NetworkTickClock TickClock { get; private set; }
    public static NetworkTickClock.Advancer TickClockAdvancer { get; private set; }
    public static NetworkServerHost ServerHost { get; private set; }
    public static NetworkClient Client { get; private set; }

    public static bool HasNetwork => ServerHost != null || Client != null;

    /// <summary>
    /// Offline / local play: same Host + Client processing as multiplayer, without UDP.
    /// </summary>
    public static void StartLocalPlay()
    {
        Reset();
        Mode = NetworkSessionMode.Host;
        TickClock = new NetworkTickClock();
        TickClockAdvancer = TickClock.CreateAdvancer();

        var serverIngress = new Queue<byte[]>();
        var clientIngress = new Queue<byte[]>();
        var serverTransport = new LoopbackServerTransport(serverIngress, clientIngress);
        ServerHost = NetworkServerHost.Start(serverTransport, port: 0, TickClock);

        Client = new NetworkClient(new LoopbackClientTransport(serverIngress, clientIngress), TickClock);
        Client.Connect(CreateClientId());
    }

    public static void StartHost(int port = DefaultPort)
    {
        Reset();
        Mode = NetworkSessionMode.Host;
        TickClock = new NetworkTickClock();
        TickClockAdvancer = TickClock.CreateAdvancer();
        ServerHost = NetworkServerHost.StartUdp(port, TickClock);
        Client = new NetworkClient(new UdpClientTransport("127.0.0.1", port), TickClock);
        Client.Connect(CreateClientId());
    }

    public static void StartClient(string host, int port = DefaultPort)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Server host is required.", nameof(host));
        }

        Reset();
        Mode = NetworkSessionMode.Client;
        TickClock = new NetworkTickClock();
        TickClockAdvancer = TickClock.CreateAdvancer();
        Client = new NetworkClient(new UdpClientTransport(host, port), TickClock);
        Client.Connect(CreateClientId());
    }

    public static void Tick(double delta)
    {
        ServerHost?.Tick(delta);
        Client?.ProcessPendingTicks();
        Client?.ReceiveSnapshots();
    }

    public static void Reset()
    {
        Client?.Dispose();
        Client = null;

        ServerHost?.Dispose();
        ServerHost = null;

        TickClockAdvancer?.Dispose();
        TickClockAdvancer = null;
        TickClock = null;
        Mode = NetworkSessionMode.Disconnected;
    }

    private static ClientId CreateClientId()
    {
        int value = Math.Abs(Environment.TickCount);
        return new ClientId(value == 0 ? 1 : value);
    }
}
