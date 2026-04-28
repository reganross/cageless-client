using System.Collections.Generic;

public class NetworkServer
{
    public const int MaxClients = 4;

    private readonly EntityRegistry registry = new();
    private readonly SnapshotSystem snapshotSystem;
    private readonly Dictionary<ClientId, Queue<SnapshotPacket>> outboundSnapshots = new();
    private readonly Dictionary<ClientId, Queue<ClientCommandPacket>> inboundCommands = new();
    private readonly Dictionary<ClientId, NetworkActivityState> clientActivityStates = new();
    private readonly Dictionary<ClientId, double> clientAccumulatedSeconds = new();
    private readonly Dictionary<ClientId, int> lastCommandTicks = new();
    private readonly Dictionary<ClientId, Dictionary<int, EntityState>> lastSentStatesByClient = new();
    private readonly IServerSnapshotTransport transport;
    private readonly SnapshotDeltaPolicy deltaPolicy;
    private readonly NetworkTickRatePolicy tickRatePolicy;
    private readonly int fullSnapshotInterval;
    private long nextSnapshotTick;
    private int ticksSinceFullSnapshot;
    private bool hasRecordedSnapshot;

    public PlayerControllerManager Controllers { get; } = new();

    public NetworkServer(int historySize)
        : this(historySize, null, new SnapshotDeltaPolicy(), fullSnapshotInterval: 10)
    {
    }

    public NetworkServer(int historySize, IServerSnapshotTransport transport)
        : this(historySize, transport, new SnapshotDeltaPolicy(), fullSnapshotInterval: 10)
    {
    }

    public NetworkServer(
        int historySize,
        IServerSnapshotTransport transport,
        SnapshotDeltaPolicy deltaPolicy,
        int fullSnapshotInterval)
        : this(historySize, transport, deltaPolicy, fullSnapshotInterval, null)
    {
    }

    public NetworkServer(
        int historySize,
        IServerSnapshotTransport transport,
        SnapshotDeltaPolicy deltaPolicy,
        int fullSnapshotInterval,
        NetworkTickRatePolicy tickRatePolicy)
    {
        snapshotSystem = new SnapshotSystem(registry, historySize);
        this.transport = transport;
        this.deltaPolicy = deltaPolicy;
        this.fullSnapshotInterval = fullSnapshotInterval;
        this.tickRatePolicy = tickRatePolicy;
    }

    public EntityId RegisterEntity(INetworkEntity entity)
    {
        return registry.Create(entity);
    }

    public void DeregisterEntity(EntityId id)
    {
        registry.Remove(id);
    }

    public bool ConnectClient(ClientId clientId)
    {
        if (outboundSnapshots.ContainsKey(clientId))
        {
            return true;
        }

        if (outboundSnapshots.Count >= MaxClients)
        {
            return false;
        }

        outboundSnapshots[clientId] = new Queue<SnapshotPacket>();
        inboundCommands[clientId] = new Queue<ClientCommandPacket>();
        lastSentStatesByClient[clientId] = new Dictionary<int, EntityState>();
        Controllers.GetOrCreate(clientId);

        if (hasRecordedSnapshot)
        {
            QueueSnapshotForClient(clientId, forceFull: true);
        }

        return true;
    }

    public void DisconnectClient(ClientId clientId)
    {
        outboundSnapshots.Remove(clientId);
        inboundCommands.Remove(clientId);
        clientActivityStates.Remove(clientId);
        clientAccumulatedSeconds.Remove(clientId);
        lastCommandTicks.Remove(clientId);
        lastSentStatesByClient.Remove(clientId);
        Controllers.Remove(clientId);
    }

    public void RecordSnapshot(long tick)
    {
        snapshotSystem.Capture(tick);
        hasRecordedSnapshot = true;
    }

    public SnapshotFrame GetLatestSnapshot()
    {
        return snapshotSystem.GetLatest();
    }

    public void Tick()
    {
        if (transport != null)
        {
            SyncConnectedClients(transport.ConnectedClients);
        }

        RecordSnapshot(nextSnapshotTick++);
        QueueLatestSnapshot();

        if (transport != null)
        {
            FlushSnapshots(transport);
        }
    }

    public void Tick(double delta)
    {
        if (tickRatePolicy == null)
        {
            Tick();
            return;
        }

        if (transport != null)
        {
            SyncConnectedClients(transport.ConnectedClients);
        }

        RecordSnapshot(nextSnapshotTick++);
        var forceFull = ShouldForceFullSnapshot();
        bool queuedFull = false;
        bool queuedAny = false;

        foreach (var clientId in outboundSnapshots.Keys)
        {
            double accumulatedSeconds = clientAccumulatedSeconds.TryGetValue(clientId, out var current)
                ? current + delta
                : delta;
            var interval = tickRatePolicy.GetInterval(GetClientActivityState(clientId));

            while (accumulatedSeconds >= interval)
            {
                var packet = QueueSnapshotForClient(clientId, forceFull);
                queuedAny = true;
                queuedFull = queuedFull || packet.Kind == SnapshotPacketKind.Full;
                accumulatedSeconds -= interval;
            }

            clientAccumulatedSeconds[clientId] = accumulatedSeconds;
        }

        if (transport != null)
        {
            FlushSnapshots(transport);
        }

        if (queuedAny)
        {
            AdvanceFullSnapshotCounter(queuedFull);
        }
    }

    public void QueueLatestSnapshot()
    {
        var forceFull = ShouldForceFullSnapshot();
        bool queuedFull = false;
        bool queuedAny = false;

        foreach (var clientId in outboundSnapshots.Keys)
        {
            var packet = QueueSnapshotForClient(clientId, forceFull);
            queuedAny = true;
            queuedFull = queuedFull || packet.Kind == SnapshotPacketKind.Full;
        }

        if (queuedAny)
        {
            AdvanceFullSnapshotCounter(queuedFull);
        }
    }

    public bool TryDequeueSnapshot(ClientId clientId, out SnapshotFrame snapshot)
    {
        if (TryDequeueSnapshotPacket(clientId, out var packet))
        {
            snapshot = packet.Frame;
            return true;
        }

        snapshot = default;
        return false;
    }

    public bool TryDequeueSnapshotPacket(ClientId clientId, out SnapshotPacket packet)
    {
        if (!outboundSnapshots.TryGetValue(clientId, out var queue) || queue.Count == 0)
        {
            packet = default;
            return false;
        }

        packet = queue.Dequeue();
        return true;
    }

    public void SyncConnectedClients(IReadOnlyCollection<ClientId> clientIds)
    {
        foreach (var clientId in clientIds)
        {
            ConnectClient(clientId);
        }
    }

    public void SetClientActivityState(ClientId clientId, NetworkActivityState activityState)
    {
        clientActivityStates[clientId] = activityState;
    }

    public bool ReceiveCommand(ClientCommandPacket command)
    {
        if (!inboundCommands.TryGetValue(command.ClientId, out var queue))
        {
            return false;
        }

        if (lastCommandTicks.TryGetValue(command.ClientId, out var lastTick)
            && command.Tick <= lastTick)
        {
            return false;
        }

        queue.Enqueue(command);
        Controllers.Apply(command);
        lastCommandTicks[command.ClientId] = command.Tick;
        return true;
    }

    public bool TryDequeueCommand(ClientId clientId, out ClientCommandPacket command)
    {
        if (!inboundCommands.TryGetValue(clientId, out var queue) || queue.Count == 0)
        {
            command = default;
            return false;
        }

        command = queue.Dequeue();
        return true;
    }

    public void FlushSnapshots(IServerSnapshotTransport transport)
    {
        foreach (var clientId in transport.ConnectedClients)
        {
            while (TryDequeueSnapshotPacket(clientId, out var packet))
            {
                transport.SendSnapshot(clientId, packet);
            }
        }
    }

    private bool ShouldForceFullSnapshot()
    {
        return fullSnapshotInterval <= 1
            || ticksSinceFullSnapshot >= fullSnapshotInterval - 1;
    }

    private SnapshotPacket QueueSnapshotForClient(ClientId clientId, bool forceFull)
    {
        if (!outboundSnapshots.TryGetValue(clientId, out var queue))
        {
            return default;
        }

        var snapshot = snapshotSystem.GetLatest();
        var packet = deltaPolicy.CreatePacket(
            snapshot,
            lastSentStatesByClient[clientId],
            forceFull || lastSentStatesByClient[clientId].Count == 0);

        queue.Enqueue(packet);
        return packet;
    }

    private void AdvanceFullSnapshotCounter(bool forceFull)
    {
        ticksSinceFullSnapshot = forceFull
            ? 0
            : ticksSinceFullSnapshot + 1;
    }

    private NetworkActivityState GetClientActivityState(ClientId clientId)
    {
        return clientActivityStates.TryGetValue(clientId, out var activityState)
            ? activityState
            : NetworkActivityState.Exploring;
    }
}
