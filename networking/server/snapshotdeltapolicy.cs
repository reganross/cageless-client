using System.Collections.Generic;
using Godot;

public class SnapshotDeltaPolicy
{
    public SnapshotDeltaPolicy(
        float positionThreshold = 0.01f,
        float rotationThreshold = 0.001f,
        float velocityThreshold = 0.01f)
    {
        PositionThreshold = positionThreshold;
        RotationThreshold = rotationThreshold;
        VelocityThreshold = velocityThreshold;
    }

    public float PositionThreshold { get; }
    public float RotationThreshold { get; }
    public float VelocityThreshold { get; }

    public SnapshotPacket CreatePacket(
        SnapshotFrame current,
        Dictionary<int, EntityState> lastSentStates,
        bool forceFull)
    {
        if (forceFull)
        {
            foreach (var kv in current.States)
            {
                lastSentStates[kv.Key] = kv.Value;
            }

            return new SnapshotPacket(SnapshotPacketKind.Full, current);
        }

        var changedStates = new Dictionary<int, EntityState>();
        foreach (var kv in current.States)
        {
            if (!lastSentStates.TryGetValue(kv.Key, out var previous)
                || HasMeaningfulChange(previous, kv.Value))
            {
                changedStates[kv.Key] = kv.Value;
                lastSentStates[kv.Key] = kv.Value;
            }
        }

        return new SnapshotPacket(
            SnapshotPacketKind.Delta,
            new SnapshotFrame
            {
                Tick = current.Tick,
                States = changedStates
            });
    }

    private bool HasMeaningfulChange(EntityState previous, EntityState current)
    {
        return previous.StateFlags != current.StateFlags
            || previous.Position.DistanceTo(current.Position) > PositionThreshold
            || previous.Velocity.DistanceTo(current.Velocity) > VelocityThreshold
            || QuaternionDistance(previous.Rotation, current.Rotation) > RotationThreshold;
    }

    private static float QuaternionDistance(Quaternion previous, Quaternion current)
    {
        return Mathf.Abs(previous.X - current.X)
            + Mathf.Abs(previous.Y - current.Y)
            + Mathf.Abs(previous.Z - current.Z)
            + Mathf.Abs(previous.W - current.W);
    }
}
