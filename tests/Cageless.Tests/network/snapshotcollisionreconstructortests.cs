using Godot;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cageless.Tests.network;

public class SnapshotCollisionReconstructorTests
{
    /*
     PURPOSE:
     Rebuild every entity collision zone exactly as it existed at a historical snapshot.

     DESIGN RULE:
     - Snapshot data stores dynamic pose and animation timing
     - Static scene collision rigs provide nodes, zones, and animation tracks
     - Animation name and animation time must drive animated collision-zone transforms
     - All entities in the snapshot must contribute their active collision zones

     FAILURE MEANS:
     - lag compensation validates hits against the wrong pose
     - melee hitboxes can disagree between client history and server rewind
    */
    [Fact]
    public void Reconstruct_UsesSnapshotAnimationTime_ForAnimatedCollisionZones()
    {
        var frame = new SnapshotFrame
        {
            Tick = 42,
            States = new Dictionary<int, EntityState>
            {
                [1] = new()
                {
                    Position = new Vector3(10f, 0f, 5f),
                    Rotation = Quaternion.Identity,
                    AnimationName = "Thrust",
                    AnimationTime = 0.4d,
                    IsAnimationPlaying = true
                },
                [2] = new()
                {
                    Position = new Vector3(-1f, 0f, 0f),
                    Rotation = Quaternion.Identity
                }
            }
        };
        var rigProvider = new TestCollisionRigProvider(new Dictionary<int, CollisionRigSnapshot>
        {
            [1] = Rig(
                [
                    Node("Body", string.Empty, Vector3.Zero),
                    Node("BodyCollider", "Body", new Vector3(0f, 0.25f, 0f)),
                    Node("WeaponPivot", "Body", Vector3.Zero),
                    Node("SpearTip", "WeaponPivot", new Vector3(0f, 0f, -0.2f))
                ],
                [
                    Zone("Body", "BodyCollider"),
                    Zone("SpearTip", "SpearTip")
                ],
                [
                    PositionTrack("Thrust", "WeaponPivot",
                        Key(0d, Vector3.Zero),
                        Key(0.1d, new Vector3(0f, 0f, 0.1f)),
                        Key(0.4d, new Vector3(0f, 0f, -0.3f)),
                        Key(1d, Vector3.Zero))
                ]),
            [2] = Rig(
                [
                    Node("BodyCollider", string.Empty, new Vector3(0f, 0.5f, 0f))
                ],
                [
                    Zone("Body", "BodyCollider")
                ])
        });

        IReadOnlyList<ReconstructedCollisionZone> zones = SnapshotCollisionReconstructor.Reconstruct(frame, rigProvider);

        Assert.Equal(3, zones.Count);
        AssertVector(new Vector3(10f, 0f, 4.5f), Zone(zones, 1, "SpearTip").GlobalTransform.Origin);
        AssertVector(new Vector3(10f, 0.25f, 5f), Zone(zones, 1, "Body").GlobalTransform.Origin);
        AssertVector(new Vector3(-1f, 0.5f, 0f), Zone(zones, 2, "Body").GlobalTransform.Origin);
    }

    /*
     PURPOSE:
     Ensure collision-zone reconstruction samples between animation keyframes.

     DESIGN RULE:
     - Snapshot animation time is authoritative
     - Static animation tracks come from the collision rig, not the per-tick snapshot
     - Reconstructed hitbox transforms use the same interpolated animation pose as the historical snapshot

     FAILURE MEANS:
     - rewound hitboxes snap to stale keyframes
     - fast attacks become inaccurate between authored animation keys
    */
    [Fact]
    public void Reconstruct_InterpolatesAnimationTrackAtSnapshotTime()
    {
        var frame = new SnapshotFrame
        {
            Tick = 43,
            States = new Dictionary<int, EntityState>
            {
                [1] = new()
                {
                    Rotation = Quaternion.Identity,
                    AnimationName = "Thrust",
                    AnimationTime = 0.25d
                }
            }
        };
        var rigProvider = new TestCollisionRigProvider(new Dictionary<int, CollisionRigSnapshot>
        {
            [1] = Rig(
                [
                    Node("WeaponPivot", string.Empty, Vector3.Zero),
                    Node("SpearTip", "WeaponPivot", new Vector3(0f, 0f, -0.2f))
                ],
                [
                    Zone("SpearTip", "SpearTip")
                ],
                [
                    PositionTrack("Thrust", "WeaponPivot",
                        Key(0.1d, new Vector3(0f, 0f, 0.1f)),
                        Key(0.4d, new Vector3(0f, 0f, -0.3f)))
                ])
        });

        ReconstructedCollisionZone spearTip = SnapshotCollisionReconstructor.Reconstruct(frame, rigProvider).Single();

        AssertVector(new Vector3(0f, 0f, -0.3f), spearTip.GlobalTransform.Origin);
    }

    private static CollisionRigSnapshot Rig(CollisionNodeSnapshot[] nodes, CollisionZoneSnapshot[] zones)
    {
        return Rig(nodes, zones, []);
    }

    private static CollisionRigSnapshot Rig(
        CollisionNodeSnapshot[] nodes,
        CollisionZoneSnapshot[] zones,
        CollisionAnimationTrackSnapshot[] animationTracks)
    {
        return new CollisionRigSnapshot
        {
            Nodes = nodes,
            Zones = zones,
            AnimationTracks = animationTracks
        };
    }

    private static CollisionNodeSnapshot Node(string name, string parentName, Vector3 localPosition)
    {
        return new CollisionNodeSnapshot
        {
            Name = name,
            ParentName = parentName,
            LocalTransform = new Transform3D(Basis.Identity, localPosition)
        };
    }

    private static CollisionZoneSnapshot Zone(string name, string nodeName)
    {
        return new CollisionZoneSnapshot
        {
            Name = name,
            NodeName = nodeName,
            Enabled = true
        };
    }

    private static CollisionAnimationTrackSnapshot PositionTrack(
        string animationName,
        string nodeName,
        params CollisionAnimationKeyframeSnapshot[] keyframes)
    {
        return new CollisionAnimationTrackSnapshot
        {
            AnimationName = animationName,
            NodeName = nodeName,
            Property = CollisionAnimationProperty.Position,
            Keyframes = keyframes
        };
    }

    private static CollisionAnimationKeyframeSnapshot Key(double time, Vector3 value)
    {
        return new CollisionAnimationKeyframeSnapshot
        {
            Time = time,
            Value = value
        };
    }

    private static ReconstructedCollisionZone Zone(
        IReadOnlyList<ReconstructedCollisionZone> zones,
        int entityId,
        string name)
    {
        return zones.Single(zone => zone.EntityId == entityId && zone.Name == name);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.True(
            expected.DistanceTo(actual) < 0.0001f,
            $"Expected {expected}, got {actual}");
    }

    private sealed class TestCollisionRigProvider : ICollisionRigProvider
    {
        private readonly IReadOnlyDictionary<int, CollisionRigSnapshot> _rigs;

        public TestCollisionRigProvider(IReadOnlyDictionary<int, CollisionRigSnapshot> rigs)
        {
            _rigs = rigs;
        }

        public bool TryGetCollisionRig(int entityId, EntityState state, out CollisionRigSnapshot rig)
        {
            return _rigs.TryGetValue(entityId, out rig);
        }
    }
}
