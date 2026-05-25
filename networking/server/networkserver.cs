using System.Collections.Generic;
using Godot;

public class NetworkServer
{
    public const int MaxClients = 4;

    private readonly EntityRegistry registry = new();
    private readonly SnapshotSystem snapshotSystem;
    private readonly Dictionary<ClientId, Queue<SnapshotPacket>> outboundSnapshots = new();
    private readonly Dictionary<ClientId, Queue<ClientCommandPacket>> inboundCommands = new();
    private readonly Dictionary<ClientId, Queue<AttackCommandPacket>> inboundAttackCommands = new();
    private readonly Dictionary<ClientId, NetworkActivityState> clientActivityStates = new();
    private readonly Dictionary<ClientId, double> clientAccumulatedSeconds = new();
    private readonly Dictionary<ClientId, Tick> lastCommandTicks = new();
    private readonly Dictionary<ClientId, Tick> lastAttackCommandTicks = new();
    private readonly Dictionary<ClientId, Dictionary<int, EntityState>> lastSentStatesByClient = new();
    private readonly Dictionary<ClientId, ServerPlayerEntity> playerEntitiesByClient = new();
    private readonly IServerSnapshotTransport transport;
    private readonly SnapshotDeltaPolicy deltaPolicy;
    private readonly NetworkTickRatePolicy tickRatePolicy;
    private readonly int fullSnapshotInterval;
    private Tick nextSnapshotTick;
    private Tick lastRecordedSnapshotTick;
    private int ticksSinceFullSnapshot;
    private bool hasRecordedSnapshot;

    public PlayerControllerManager Controllers { get; } = new();

    /// <summary>
    /// World-space spawn used for new player entities and <see cref="SyncAuthoritativePlayerSpawn"/>.
    /// Defaults to match combat scene <c>PlayerSpawnPosition</c> export (feet above floor).
    /// </summary>
    public Vector3 AuthoritativePlayerSpawn { get; set; } = new Vector3(0f, 0.25f, 0f);

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
        inboundAttackCommands[clientId] = new Queue<AttackCommandPacket>();
        lastSentStatesByClient[clientId] = new Dictionary<int, EntityState>();
        Controllers.GetOrCreate(clientId);
        CreatePlayerEntity(clientId);

        if (hasRecordedSnapshot)
        {
            RecordSnapshot(lastRecordedSnapshotTick);
            QueueSnapshotForClient(clientId, forceFull: true);
        }

        return true;
    }

    public void DisconnectClient(ClientId clientId)
    {
        outboundSnapshots.Remove(clientId);
        inboundCommands.Remove(clientId);
        inboundAttackCommands.Remove(clientId);
        clientActivityStates.Remove(clientId);
        clientAccumulatedSeconds.Remove(clientId);
        lastCommandTicks.Remove(clientId);
        lastAttackCommandTicks.Remove(clientId);
        lastSentStatesByClient.Remove(clientId);
        Controllers.Remove(clientId);
        RemovePlayerEntity(clientId);
    }

    public void RecordSnapshot(Tick tick)
    {
        SimulateAuthoritativePlayers();
        snapshotSystem.Capture(tick);
        lastRecordedSnapshotTick = tick;
        hasRecordedSnapshot = true;
    }

    public void RecordSnapshot(int tick)
    {
        RecordSnapshot(new Tick(tick));
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

        RecordSnapshot(nextSnapshotTick);
        nextSnapshotTick++;
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

        RecordSnapshot(nextSnapshotTick);
        nextSnapshotTick++;
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

    public bool ReceiveAttackCommand(AttackCommandPacket command)
    {
        if (!inboundAttackCommands.TryGetValue(command.ClientId, out var queue))
        {
            return false;
        }

        if (lastAttackCommandTicks.TryGetValue(command.ClientId, out var lastTick)
            && command.Tick <= lastTick)
        {
            return false;
        }

        queue.Enqueue(command);
        lastAttackCommandTicks[command.ClientId] = command.Tick;
        GD.Print(
            $"Attack packet received: clientId={command.ClientId.Value} tick={command.Tick.Value}");
        return true;
    }

    public bool TryDequeueAttackCommand(ClientId clientId, out AttackCommandPacket command)
    {
        if (!inboundAttackCommands.TryGetValue(clientId, out var queue) || queue.Count == 0)
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

    private void CreatePlayerEntity(ClientId clientId)
    {
        if (playerEntitiesByClient.ContainsKey(clientId))
        {
            return;
        }

        var playerEntity = new ServerPlayerEntity(clientId, AuthoritativePlayerSpawn);
        var entityId = RegisterEntity(playerEntity);
        playerEntity.AssignEntityId(entityId);
        playerEntitiesByClient[clientId] = playerEntity;
    }

    private void RemovePlayerEntity(ClientId clientId)
    {
        if (!playerEntitiesByClient.TryGetValue(clientId, out var playerEntity))
        {
            return;
        }

        DeregisterEntity(playerEntity.Id);
        playerEntitiesByClient.Remove(clientId);
    }

    private void SimulateAuthoritativePlayers()
    {
        foreach (var kv in playerEntitiesByClient)
        {
            Controllers.TryGet(kv.Key, out var controller);
            kv.Value.Simulate(controller);
        }
    }

    /// <summary>
    /// Align all connected players with the combat/scene spawn so snapshots match the local CharacterBody.
    /// </summary>
    public void SyncAuthoritativePlayerSpawn(Vector3 worldSpawn)
    {
        AuthoritativePlayerSpawn = worldSpawn;
        foreach (var kv in playerEntitiesByClient)
        {
            kv.Value.ResetToSpawn(worldSpawn);
        }
    }
}
