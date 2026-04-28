using System.Collections.Generic;
using System.IO;

public class NetworkClient
{
    private const int FullControllerIntervalTicks = 5;

    private readonly IClientTransport transport;
    private readonly NetworkTickClock tickClock;
    private readonly NetworkTickClock.Advancer ownedAdvancer;
    private SnapshotPacket latestSnapshot;
    private bool hasLatestSnapshot;
    private PlayerController lastSentController;

    public NetworkClient(IClientTransport transport)
        : this(transport, new NetworkTickClock(), ownsClockAdvancement: true)
    {
    }

    public NetworkClient(IClientTransport transport, NetworkTickClock tickClock)
        : this(transport, tickClock, ownsClockAdvancement: false)
    {
    }

    private NetworkClient(
        IClientTransport transport,
        NetworkTickClock tickClock,
        bool ownsClockAdvancement)
    {
        this.transport = transport;
        this.tickClock = tickClock ?? throw new System.ArgumentNullException(nameof(tickClock));
        ownedAdvancer = ownsClockAdvancement
            ? tickClock.CreateAdvancer()
            : null;
        Controller = new PlayerController();
    }

    public bool IsConnected { get; private set; }
    public ClientId ClientId { get; private set; }
    public PlayerController Controller { get; private set; }
    public NetworkTickClock TickClock => tickClock;

    public bool Connect(ClientId clientId)
    {
        if (IsConnected)
        {
            return false;
        }

        ClientId = clientId;
        Controller = new PlayerController(clientId, tick: 0);
        lastSentController = CopyController(Controller, tick: 0);
        tickClock.Reset();
        IsConnected = true;

        transport.Send(CreateClientIdPacket(ClientPacketKind.Connect, clientId));
        return true;
    }

    public bool SendControllerUpdate()
    {
        if (!IsConnected)
        {
            return false;
        }

        return SendFullControllerPacket(tickClock.CurrentTick);
    }

    public int Tick(double deltaSeconds)
    {
        if (!IsConnected || ownedAdvancer == null)
        {
            return 0;
        }

        ownedAdvancer.Advance(deltaSeconds);
        return ProcessPendingTicks();
    }

    public int ProcessPendingTicks()
    {
        if (!IsConnected)
        {
            return 0;
        }

        int sent = 0;
        while (tickClock.TryRequestTick(out int tick))
        {
            Controller.SetTick(tick);

            if (ProcessControllerTick(tick))
            {
                sent++;
            }
        }

        return sent;
    }

    public int ReceiveSnapshots()
    {
        int received = 0;

        while (transport.TryReceive(out var bytes))
        {
            try
            {
                latestSnapshot = SnapshotSerializer.DeserializePacket(bytes);
                hasLatestSnapshot = true;
                received++;
            }
            catch (InvalidDataException)
            {
            }
        }

        return received;
    }

    public bool TryGetLatestSnapshot(out SnapshotPacket snapshot)
    {
        snapshot = latestSnapshot;
        return hasLatestSnapshot;
    }

    public bool Disconnect()
    {
        if (!IsConnected)
        {
            return false;
        }

        transport.Send(CreateClientIdPacket(ClientPacketKind.Disconnect, ClientId));
        IsConnected = false;
        Controller = new PlayerController();
        lastSentController = null;
        tickClock.Reset();
        return true;
    }

    private bool ProcessControllerTick(int tick)
    {
        if (tick % FullControllerIntervalTicks == 0)
        {
            return SendFullControllerPacket(tick);
        }

        var changedActions = GetChangedActions(Controller, lastSentController);
        bool hasLookRotation = HasLookRotationChanged(Controller, lastSentController);
        if (changedActions.Count == 0 && !hasLookRotation)
        {
            return false;
        }

        var deltaController = new PlayerController(ClientId, tick, changedActions);
        if (hasLookRotation)
        {
            deltaController.SetLookRotation(Controller.LookYaw, Controller.LookPitch);
        }

        SendControllerCommand(new ClientCommandPacket(
            ClientCommandKind.Controller,
            ControllerPacketKind.Delta,
            hasLookRotation,
            deltaController));
        lastSentController = CopyController(Controller, tick);
        return true;
    }

    private bool SendFullControllerPacket(int tick)
    {
        var snapshot = CopyController(Controller, tick);
        SendControllerCommand(new ClientCommandPacket(
            ClientCommandKind.Controller,
            ControllerPacketKind.Full,
            hasLookRotation: true,
            snapshot));
        lastSentController = CopyController(Controller, tick);
        return true;
    }

    private void SendControllerCommand(ClientCommandPacket command)
    {
        var payload = ClientCommandSerializer.Serialize(command);
        transport.Send(CreatePayloadPacket(ClientPacketKind.Controller, payload));
    }

    private static byte[] CreateClientIdPacket(ClientPacketKind kind, ClientId clientId)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((int)kind);
        writer.Write(clientId.Value);
        return stream.ToArray();
    }

    private static byte[] CreatePayloadPacket(ClientPacketKind kind, byte[] payload)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((int)kind);
        writer.Write(payload.Length);
        writer.Write(payload);
        return stream.ToArray();
    }

    private static PlayerController CopyController(PlayerController source, int tick)
    {
        var copy = new PlayerController(source.PlayerId, tick, source.Actions);
        copy.SetLookRotation(source.LookYaw, source.LookPitch);
        return copy;
    }

    private static List<InputActionState> GetChangedActions(
        PlayerController current,
        PlayerController baseline)
    {
        var changedActions = new List<InputActionState>();
        var actionNames = new HashSet<string>();

        foreach (var action in current.Actions)
        {
            actionNames.Add(action.ActionName);
        }

        if (baseline != null)
        {
            foreach (var action in baseline.Actions)
            {
                actionNames.Add(action.ActionName);
            }
        }

        foreach (string actionName in actionNames)
        {
            float currentStrength = current.GetActionStrength(actionName);
            float baselineStrength = baseline?.GetActionStrength(actionName) ?? 0;

            if (currentStrength != baselineStrength)
            {
                changedActions.Add(new InputActionState(actionName, currentStrength));
            }
        }

        return changedActions;
    }

    private static bool HasLookRotationChanged(PlayerController current, PlayerController baseline)
    {
        if (baseline == null)
        {
            return true;
        }

        return current.LookYaw != baseline.LookYaw
            || current.LookPitch != baseline.LookPitch;
    }
}
