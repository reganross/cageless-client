public class NetworkServerTickDriver
{
    private readonly NetworkServer server;
    private readonly double snapshotIntervalSeconds;
    private double accumulatedSeconds;

    public NetworkServerTickDriver(
        NetworkServer server,
        double snapshotIntervalSeconds)
    {
        this.server = server;
        this.snapshotIntervalSeconds = snapshotIntervalSeconds;
    }

    public void Tick(double delta)
    {
        accumulatedSeconds += delta;

        while (accumulatedSeconds >= snapshotIntervalSeconds)
        {
            server.Tick();
            accumulatedSeconds -= snapshotIntervalSeconds;
        }
    }
}
