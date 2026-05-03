using Godot;

public sealed class SceneNodeSpawner
{
    private readonly System.Func<NetworkSessionMode> getMode;

    public SceneNodeSpawner(System.Func<NetworkSessionMode> getMode = null)
    {
        this.getMode = getMode ?? (() => NetworkSession.Mode);
    }

    public bool TryAddNode<TNode>(
        PackedScene scene,
        Node parent,
        Transform3D transform,
        SceneNodeSpawnKind kind,
        SceneNodeSpawnSource source,
        out TNode node)
        where TNode : Node
    {
        node = null;
        if (!SceneNodeSpawnPolicy.CanSpawn(kind, source, getMode()))
        {
            return false;
        }

        if (scene == null || parent == null)
        {
            return false;
        }

        node = scene.Instantiate<TNode>();
        if (node is Node3D node3D)
        {
            node3D.GlobalTransform = transform;
        }

        parent.AddChild(node);
        return true;
    }

    public bool TryAddNode(
        PackedScene scene,
        Node parent,
        Transform3D transform,
        SceneNodeSpawnKind kind,
        SceneNodeSpawnSource source,
        out Node node)
    {
        return TryAddNode<Node>(
            scene,
            parent,
            transform,
            kind,
            source,
            out node);
    }
}
