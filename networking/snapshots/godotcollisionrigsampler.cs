using Godot;
using System;
using System.Collections.Generic;

public sealed class GodotCollisionRigSampler : IDisposable
{
    private readonly Node3D sampleRoot;
    private readonly AnimationPlayer animationPlayer;
    private bool disposed;

    private GodotCollisionRigSampler(Node3D sampleRoot)
    {
        this.sampleRoot = sampleRoot ?? throw new ArgumentNullException(nameof(sampleRoot));
        animationPlayer = FindFirst<AnimationPlayer>(sampleRoot);
    }

    public static GodotCollisionRigSampler FromInstance(Node3D sourceRoot)
    {
        if (sourceRoot == null)
            throw new ArgumentNullException(nameof(sourceRoot));

        if (sourceRoot.Duplicate() is not Node3D duplicate)
            throw new InvalidOperationException($"Could not duplicate {sourceRoot.Name} as a Node3D collision sampler.");

        return new GodotCollisionRigSampler(duplicate);
    }

    public static GodotCollisionRigSampler FromScenePath(string scenePath)
    {
        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        if (scene == null)
            throw new InvalidOperationException($"Could not load collision sampler scene '{scenePath}'.");

        return new GodotCollisionRigSampler(scene.Instantiate<Node3D>());
    }

    public CollisionRigSnapshot Sample(EntityState state)
    {
        ThrowIfDisposed();
        sampleRoot.Transform = Transform3D.Identity;
        ApplyAnimationPose(state);

        var owners = new List<CollisionOwnerSnapshot>();
        CollectCollisionOwners(sampleRoot, sampleRoot, owners);

        return new CollisionRigSnapshot
        {
            Owners = owners.ToArray()
        };
    }

    public void Dispose()
    {
        if (disposed)
            return;

        sampleRoot.Free();
        disposed = true;
    }

    private void ApplyAnimationPose(EntityState state)
    {
        if (animationPlayer == null)
            return;

        if (animationPlayer.HasAnimation("RESET"))
        {
            animationPlayer.Play("RESET");
            animationPlayer.Seek(0d, update: true);
            animationPlayer.Advance(0d);
        }

        if (string.IsNullOrEmpty(state.AnimationName) || !animationPlayer.HasAnimation(state.AnimationName))
            return;

        animationPlayer.Play(state.AnimationName);
        animationPlayer.Seek(state.AnimationTime, update: true);
        animationPlayer.Advance(0d);
    }

    private static void CollectCollisionOwners(
        Node node,
        Node3D root,
        List<CollisionOwnerSnapshot> owners)
    {
        if (node is Node3D owner && TryGetOwnerKind(owner, out CollisionOwnerKind ownerKind))
            AddOwnerCollisionShapes(owner, root, ownerKind, owners);

        foreach (Node child in node.GetChildren())
            CollectCollisionOwners(child, root, owners);
    }

    private static void AddOwnerCollisionShapes(
        Node3D owner,
        Node3D root,
        CollisionOwnerKind ownerKind,
        List<CollisionOwnerSnapshot> owners)
    {
        foreach (Node child in owner.GetChildren())
        {
            if (child is not CollisionShape3D shapeNode || shapeNode.Shape == null)
                continue;

            if (!TryGetTransformRelativeTo(root, shapeNode, out Transform3D localTransform))
                continue;

            owners.Add(new CollisionOwnerSnapshot
            {
                OwnerKind = ownerKind,
                Shape = DuplicateShape(shapeNode.Shape),
                LocalTransform = localTransform,
                Enabled = IsCollisionOwnerEnabled(owner, ownerKind) && !shapeNode.Disabled
            });
        }
    }

    private static bool TryGetOwnerKind(Node3D owner, out CollisionOwnerKind ownerKind)
    {
        switch (owner)
        {
            case CharacterBody3D:
                ownerKind = CollisionOwnerKind.CharacterBody3D;
                return true;
            case RigidBody3D:
                ownerKind = CollisionOwnerKind.RigidBody3D;
                return true;
            case Area3D:
                ownerKind = CollisionOwnerKind.Area3D;
                return true;
            default:
                ownerKind = default;
                return false;
        }
    }

    private static bool IsCollisionOwnerEnabled(Node3D owner, CollisionOwnerKind ownerKind)
    {
        return ownerKind != CollisionOwnerKind.Area3D || ((Area3D)owner).Monitoring;
    }

    private static Shape3D DuplicateShape(Shape3D shape)
    {
        return shape.Duplicate() as Shape3D ?? shape;
    }

    private static bool TryGetTransformRelativeTo(Node3D root, Node3D node, out Transform3D transform)
    {
        var chain = new Stack<Node3D>();
        Node current = node;

        while (current != null && current != root)
        {
            if (current is Node3D current3D)
                chain.Push(current3D);

            current = current.GetParent();
        }

        if (current != root)
        {
            transform = Transform3D.Identity;
            return false;
        }

        transform = Transform3D.Identity;
        while (chain.Count > 0)
            transform *= chain.Pop().Transform;

        return true;
    }

    private static T FindFirst<T>(Node node)
        where T : Node
    {
        if (node is T match)
            return match;

        foreach (Node child in node.GetChildren())
        {
            T childMatch = FindFirst<T>(child);
            if (childMatch != null)
                return childMatch;
        }

        return null;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(GodotCollisionRigSampler));
    }
}
