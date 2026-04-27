using System.Collections.Generic;
using Godot;

public class PlayerController
{
    private readonly Dictionary<string, float> actionStrengths = new();
    private int sequence;

    public PlayerController(int sequence = 0)
    {
        this.sequence = sequence;
        HasPlayerId = false;
    }

    public PlayerController(ClientId playerId, int sequence)
        : this(playerId, sequence, new List<InputActionState>())
    {
    }

    public PlayerController(
        ClientId playerId,
        int sequence,
        IEnumerable<InputActionState> actions)
    {
        PlayerId = playerId;
        this.sequence = sequence;
        HasPlayerId = true;

        foreach (var action in actions)
        {
            SetActionStrength(action.ActionName, action.Strength);
        }
    }

    public bool HasPlayerId { get; }
    public ClientId PlayerId { get; }
    public int Sequence => sequence;
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

    public void ApplySnapshot(PlayerController snapshot)
    {
        sequence = snapshot.Sequence;
        actionStrengths.Clear();

        foreach (var action in snapshot.Actions)
        {
            SetActionStrength(action.ActionName, action.Strength);
        }

        SetLookRotation(snapshot.LookYaw, snapshot.LookPitch);
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
            this);
    }
}
