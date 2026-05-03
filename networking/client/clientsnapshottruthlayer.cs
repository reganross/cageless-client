using Godot;

public sealed class ClientSnapshotTruthLayer
{
    private readonly NetworkClient client;

    public ClientSnapshotTruthLayer(NetworkClient client)
    {
        this.client = client ?? throw new System.ArgumentNullException(nameof(client));
    }

    public bool TryApplyTruth(
        int entityId,
        CharacterBody3D body,
        double delta,
        float positionLerpSpeed,
        float rotationLerpSpeed)
    {
        if (!client.TryGetLatestEntityState(entityId, out var state))
        {
            return false;
        }

        float dt = (float)delta;
        float positionWeight = 1f - Mathf.Exp(-positionLerpSpeed * dt);
        float rotationWeight = 1f - Mathf.Exp(-rotationLerpSpeed * dt);
        var targetRotation = state.Rotation.GetEuler();

        body.GlobalPosition = body.GlobalPosition.Lerp(state.Position, positionWeight);
        body.Rotation = new Vector3(
            Mathf.LerpAngle(body.Rotation.X, targetRotation.X, rotationWeight),
            Mathf.LerpAngle(body.Rotation.Y, targetRotation.Y, rotationWeight),
            Mathf.LerpAngle(body.Rotation.Z, targetRotation.Z, rotationWeight));
        body.Velocity = state.Velocity;
        return true;
    }
}
