using Godot;

public sealed class GodotSceneReplica : IClientSceneReplica
{
    private readonly Node3D node;

    private GodotSceneReplica(int entityId, Node3D node)
    {
        EntityId = entityId;
        this.node = node;
    }

    public int EntityId { get; }

    public static GodotSceneReplica Spawn(
        PackedScene scene,
        Node parent,
        int entityId,
        EntityState state,
        ClientSnapshotTruthLayer truthLayer,
        SceneNodeSpawner spawner,
        PackedScene weaponScene = null)
    {
        var transform = new Transform3D(new Basis(state.Rotation), state.Position);
        if (spawner == null
            || !spawner.TryAddNode(
                scene,
                parent,
                transform,
                SceneNodeSpawnKind.Entity,
                SceneNodeSpawnSource.ServerSnapshot,
                out Node3D node))
        {
            return null;
        }

        if (node is Playercharacter player)
        {
            player.WeaponScene = weaponScene;
            player.UseController(new PlayerController(), usesLocalInput: false);
            if (truthLayer != null)
            {
                player.UseTruthLayer(entityId, truthLayer);
            }
        }
        else if (node is Spearman spearman && truthLayer != null)
        {
            spearman.UseTruthLayer(entityId, truthLayer);
        }

        return new GodotSceneReplica(entityId, node);
    }

    public void ApplyState(EntityState state)
    {
        if (node is not Playercharacter)
        {
            node.GlobalPosition = state.Position;
            node.Rotation = state.Rotation.GetEuler();
        }
    }

    public void Despawn()
    {
        node.QueueFree();
    }
}
