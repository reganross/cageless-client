using System.Collections.Generic;
using Godot;

public class PlayerController
{
    private readonly Dictionary<string, float> actionStrengths = new();
    private int tick;

    public PlayerController(int tick = 0)
    {
        this.tick = tick;
        HasPlayerId = false;
    }

    public PlayerController(ClientId playerId, int tick)
        : this(playerId, tick, new List<InputActionState>())
    {
    }

    public PlayerController(
        ClientId playerId,
        int tick,
        IEnumerable<InputActionState> actions)
    {
        PlayerId = playerId;
        this.tick = tick;
        HasPlayerId = true;

        foreach (var action in actions)
        {
            SetActionStrength(action.ActionName, action.Strength);
        }
    }

    public bool HasPlayerId { get; }
    public ClientId PlayerId { get; }
    public int Tick => tick;
    public float LookYaw { get; private set; }
    public float LookPitch { get; private set; }
    public IReadOnlyList<InputActionState> Actions
    {
        get
        {
            var actions = new List<InputActionState>();
            foreach (var kv in actionStrengths)
            {
                actions.Add(new InputActionState(kv.Key, kv.Value));
            }

            return actions;
        }
    }

    public void SetActionStrength(string actionName, float strength)
    {
        actionStrengths[actionName] = strength;
    }

    public void SetLookRotation(float yaw, float pitch)
    {
        LookYaw = yaw;
        LookPitch = pitch;
    }

    public void SetTick(int tick)
    {
        this.tick = tick;
    }

    public void ApplySnapshot(PlayerController snapshot)
    {
        tick = snapshot.Tick;
        actionStrengths.Clear();

        foreach (var action in snapshot.Actions)
        {
            SetActionStrength(action.ActionName, action.Strength);
        }

        SetLookRotation(snapshot.LookYaw, snapshot.LookPitch);
    }

    public void ApplyDelta(PlayerController delta, bool hasLookRotation)
    {
        tick = delta.Tick;

        foreach (var action in delta.Actions)
        {
            SetActionStrength(action.ActionName, action.Strength);
        }

        if (hasLookRotation)
        {
            SetLookRotation(delta.LookYaw, delta.LookPitch);
        }
    }

    public float GetActionStrength(string actionName)
    {
        return actionStrengths.TryGetValue(actionName, out var strength)
            ? strength
            : 0;
    }

    public Vector2 GetMoveDirection()
    {
        return new Vector2(
            GetActionStrength("right") - GetActionStrength("left"),
            GetActionStrength("back") - GetActionStrength("forward"));
    }

    public ClientCommandPacket ToCommandPacket()
    {
        if (!HasPlayerId)
        {
            throw new System.InvalidOperationException("Local controller must be assigned a player id before network send.");
        }

        return new ClientCommandPacket(
            ClientCommandKind.Controller,
            ControllerPacketKind.Full,
            hasLookRotation: true,
            this);
    }
}
