using System;

public enum NetworkSessionMode
{
    SinglePlayer,
    Host,
    Client
}

public static class NetworkSession
{
    public const string DefaultHost = "71.91.25.119";
    public const int DefaultPort = 7777;

    public static NetworkSessionMode Mode { get; private set; } = NetworkSessionMode.SinglePlayer;
    public static NetworkTickClock TickClock { get; private set; }
    public static NetworkTickClock.Advancer TickClockAdvancer { get; private set; }
    public static NetworkServerHost ServerHost { get; private set; }
    public static NetworkClient Client { get; private set; }

    public static bool HasNetwork => ServerHost != null || Client != null;

    public static void StartSinglePlayer()
    {
        Reset();
        Mode = NetworkSessionMode.SinglePlayer;
        TickClock = new NetworkTickClock();
        TickClockAdvancer = TickClock.CreateAdvancer();
    }

    public static void StartHost(int port = DefaultPort)
    {
        Reset();
        Mode = NetworkSessionMode.Host;
        TickClock = new NetworkTickClock();
        TickClockAdvancer = TickClock.CreateAdvancer();
        ServerHost = NetworkServerHost.StartUdp(port, TickClock);
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
        Mode = NetworkSessionMode.SinglePlayer;
    }

    private static ClientId CreateClientId()
    {
        int value = Math.Abs(Environment.TickCount);
        return new ClientId(value == 0 ? 1 : value);
    }
}
