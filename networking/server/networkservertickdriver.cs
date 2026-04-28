public class NetworkServerTickDriver
{
    private readonly NetworkServer server;
    private readonly NetworkTickClock tickClock;
    private readonly NetworkTickClock.Advancer ownedAdvancer;

    public NetworkServerTickDriver(
        NetworkServer server,
        double snapshotIntervalSeconds)
        : this(server, new NetworkTickClock(snapshotIntervalSeconds), ownsClockAdvancement: true)
    {
    }

    public NetworkServerTickDriver(
        NetworkServer server,
        NetworkTickClock tickClock)
        : this(server, tickClock, ownsClockAdvancement: false)
    {
    }

    private NetworkServerTickDriver(
        NetworkServer server,
        NetworkTickClock tickClock,
        bool ownsClockAdvancement)
    {
        this.server = server;
        this.tickClock = tickClock;
        ownedAdvancer = ownsClockAdvancement
            ? tickClock.CreateAdvancer()
            : null;
    }

    public void Tick(double delta)
    {
        if (ownedAdvancer == null)
        {
            return;
        }

        ownedAdvancer.Advance(delta);
        ProcessPendingTicks();
    }

    public int ProcessPendingTicks()
    {
        int processed = 0;
        while (tickClock.TryRequestTick(out _))
        {
            server.Tick();
            processed++;
        }

        return processed;
    }
}
