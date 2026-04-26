using Godot;

public partial class NetworkServerNode : Node
{
    [Export]
    public double SnapshotIntervalSeconds { get; set; } = 0.05;

    public NetworkServer Server { get; private set; }
    private NetworkServerTickDriver driver;

    public void UseServer(NetworkServer server)
    {
        Server = server;
        driver = server == null
            ? null
            : new NetworkServerTickDriver(server, SnapshotIntervalSeconds);
    }

    public override void _PhysicsProcess(double delta)
    {
        driver?.Tick(delta);
    }

    public override void _ExitTree()
    {
        driver = null;
    }

    public void SetClientActivityState(ClientId clientId, NetworkActivityState activityState)
    {
        Server?.SetClientActivityState(clientId, activityState);
    }
}
