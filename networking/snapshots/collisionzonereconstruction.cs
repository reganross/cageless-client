using Godot;
using System;
using System.Collections.Generic;

public enum CollisionOwnerKind
{
    CharacterBody3D,
    RigidBody3D,
    Area3D
}

public struct CollisionOwnerSnapshot
{
    public CollisionOwnerKind OwnerKind;
    public Shape3D Shape;
    public Transform3D LocalTransform;
    public bool Enabled;
}

public struct CollisionRigSnapshot
{
    public CollisionOwnerSnapshot[] Owners;
}

public interface ICollisionRigProvider
{
    bool TryGetCollisionRig(int entityId, EntityState state, out CollisionRigSnapshot rig);
}

public sealed class EntityRegistryCollisionRigProvider : ICollisionRigProvider
{
    private readonly EntityRegistry registry;

    public EntityRegistryCollisionRigProvider(EntityRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public bool TryGetCollisionRig(int entityId, EntityState state, out CollisionRigSnapshot rig)
    {
        if (registry.All.TryGetValue(entityId, out INetworkEntity entity))
        {
            rig = entity.GetCollisionRig(state);
            return true;
        }

        rig = default;
        return false;
    }
}

public readonly struct ReconstructedCollisionOwner
{
    public ReconstructedCollisionOwner(
        int entityId,
        CollisionOwnerKind ownerKind,
        Shape3D shape,
        bool enabled,
        Transform3D globalTransform)
    {
        EntityId = entityId;
        OwnerKind = ownerKind;
        Shape = shape;
        Enabled = enabled;
        GlobalTransform = globalTransform;
    }

    public int EntityId { get; }
    public CollisionOwnerKind OwnerKind { get; }
    public Shape3D Shape { get; }
    public bool Enabled { get; }
    public Transform3D GlobalTransform { get; }
}

public static class SnapshotCollisionReconstructor
{
    public static IReadOnlyList<ReconstructedCollisionOwner> Reconstruct(
        SnapshotFrame frame,
        ICollisionRigProvider rigProvider)
    {
        if (rigProvider == null)
            throw new ArgumentNullException(nameof(rigProvider));

        var owners = new List<ReconstructedCollisionOwner>();
        if (frame.States == null)
            return owners;

        foreach (var kv in frame.States)
        {
            if (rigProvider.TryGetCollisionRig(kv.Key, kv.Value, out CollisionRigSnapshot rig))
                ReconstructEntity(kv.Key, kv.Value, rig, owners);
        }

        return owners;
    }

    private static void ReconstructEntity(
        int entityId,
        EntityState state,
        CollisionRigSnapshot rig,
        List<ReconstructedCollisionOwner> owners)
    {
        if (rig.Owners == null)
            return;

        var entityTransform = new Transform3D(GetEntityBasis(state.Rotation), state.Position);

        foreach (var owner in rig.Owners)
        {
            if (owner.Shape == null)
                continue;

            owners.Add(new ReconstructedCollisionOwner(
                entityId,
                owner.OwnerKind,
                owner.Shape,
                owner.Enabled,
                entityTransform * owner.LocalTransform));
        }
    }

    private static Basis GetEntityBasis(Quaternion rotation)
    {
        if (rotation.X == 0f && rotation.Y == 0f && rotation.Z == 0f && rotation.W == 0f)
            return Basis.Identity;

        return new Basis(rotation);
    }
}
