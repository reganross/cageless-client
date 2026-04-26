using System.Collections.Generic;
using System.IO;
using Godot;
using Xunit;

public class SnapshotSerializerTests
{
    /*
     PURPOSE:
     Ensure empty snapshots serialize to a predictable packet.

     DESIGN RULE:
     - Snapshot packets include the server tick
     - Snapshot packets include the number of entity states

     FAILURE MEANS:
     - Clients cannot identify the snapshot tick
     - Empty world updates may be ambiguous on the wire
    */
    [Fact]
    public void Serialize_ShouldWriteTickAndStateCount()
    {
        var frame = new SnapshotFrame
        {
            Tick = 42,
            States = new Dictionary<int, EntityState>()
        };

        var bytes = SnapshotSerializer.Serialize(frame);

        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal(42, reader.ReadInt64());
        Assert.Equal(0, reader.ReadInt32());
    }

    /*
     PURPOSE:
     Ensure entity state snapshots serialize the data clients need.

     DESIGN RULE:
     - Entity id is written before its state
     - Position, rotation, velocity, and flags are included

     FAILURE MEANS:
     - Clients cannot reconstruct authoritative entity state
     - Snapshot packets may omit fields needed for reconciliation
    */
    [Fact]
    public void Serialize_ShouldWriteEntityState()
    {
        var frame = new SnapshotFrame
        {
            Tick = 42,
            States = new Dictionary<int, EntityState>
            {
                [7] = new EntityState
                {
                    Position = new Vector3(1, 2, 3),
                    Rotation = new Quaternion(4, 5, 6, 7),
                    Velocity = new Vector3(8, 9, 10),
                    StateFlags = 11
                }
            }
        };

        var bytes = SnapshotSerializer.Serialize(frame);

        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal(42, reader.ReadInt64());
        Assert.Equal(1, reader.ReadInt32());
        Assert.Equal(7, reader.ReadInt32());
        Assert.Equal(1, reader.ReadSingle());
        Assert.Equal(2, reader.ReadSingle());
        Assert.Equal(3, reader.ReadSingle());
        Assert.Equal(4, reader.ReadSingle());
        Assert.Equal(5, reader.ReadSingle());
        Assert.Equal(6, reader.ReadSingle());
        Assert.Equal(7, reader.ReadSingle());
        Assert.Equal(8, reader.ReadSingle());
        Assert.Equal(9, reader.ReadSingle());
        Assert.Equal(10, reader.ReadSingle());
        Assert.Equal(11, reader.ReadInt32());
    }

    /*
     PURPOSE:
     Ensure clients can deserialize empty snapshot packets.

     DESIGN RULE:
     - Deserialized snapshots preserve the server tick
     - Empty snapshots produce an empty state dictionary

     FAILURE MEANS:
     - Clients cannot apply empty authoritative updates
     - Snapshot packets may deserialize with missing state containers
    */
    [Fact]
    public void Deserialize_ShouldReadTickAndEmptyStates()
    {
        var frame = new SnapshotFrame
        {
            Tick = 42,
            States = new Dictionary<int, EntityState>()
        };
        var bytes = SnapshotSerializer.Serialize(frame);

        var deserialized = SnapshotSerializer.Deserialize(bytes);

        Assert.Equal(42, deserialized.Tick);
        Assert.Empty(deserialized.States);
    }

    /*
     PURPOSE:
     Ensure clients can deserialize entity state snapshots.

     DESIGN RULE:
     - Entity ids are restored as state dictionary keys
     - Position, rotation, velocity, and flags round-trip through the packet

     FAILURE MEANS:
     - Clients cannot reconstruct authoritative entity state
     - Reconciliation may compare against corrupted snapshot data
    */
    [Fact]
    public void Deserialize_ShouldReadEntityState()
    {
        var frame = new SnapshotFrame
        {
            Tick = 42,
            States = new Dictionary<int, EntityState>
            {
                [7] = new EntityState
                {
                    Position = new Vector3(1, 2, 3),
                    Rotation = new Quaternion(4, 5, 6, 7),
                    Velocity = new Vector3(8, 9, 10),
                    StateFlags = 11
                }
            }
        };
        var bytes = SnapshotSerializer.Serialize(frame);

        var deserialized = SnapshotSerializer.Deserialize(bytes);

        Assert.Equal(42, deserialized.Tick);
        Assert.True(deserialized.States.ContainsKey(7));
        Assert.Equal(new Vector3(1, 2, 3), deserialized.States[7].Position);
        Assert.Equal(new Quaternion(4, 5, 6, 7), deserialized.States[7].Rotation);
        Assert.Equal(new Vector3(8, 9, 10), deserialized.States[7].Velocity);
        Assert.Equal(11, deserialized.States[7].StateFlags);
    }

    /*
     PURPOSE:
     Ensure clients can deserialize snapshots containing multiple entities.

     DESIGN RULE:
     - Every serialized entity state is restored
     - Entity states remain keyed by their original ids

     FAILURE MEANS:
     - Clients may only apply one entity from a snapshot
     - Multiplayer state may desync when snapshots contain several entities
    */
    [Fact]
    public void Deserialize_ShouldReadMultipleEntityStates()
    {
        var frame = new SnapshotFrame
        {
            Tick = 42,
            States = new Dictionary<int, EntityState>
            {
                [7] = new EntityState
                {
                    Position = new Vector3(1, 2, 3),
                    Rotation = new Quaternion(4, 5, 6, 7),
                    Velocity = new Vector3(8, 9, 10),
                    StateFlags = 11
                },
                [12] = new EntityState
                {
                    Position = new Vector3(13, 14, 15),
                    Rotation = new Quaternion(16, 17, 18, 19),
                    Velocity = new Vector3(20, 21, 22),
                    StateFlags = 23
                }
            }
        };
        var bytes = SnapshotSerializer.Serialize(frame);

        var deserialized = SnapshotSerializer.Deserialize(bytes);

        Assert.Equal(2, deserialized.States.Count);
        Assert.Equal(new Vector3(1, 2, 3), deserialized.States[7].Position);
        Assert.Equal(new Vector3(13, 14, 15), deserialized.States[12].Position);
        Assert.Equal(23, deserialized.States[12].StateFlags);
    }

    /*
     PURPOSE:
     Ensure snapshot packet kind survives serialization.

     DESIGN RULE:
     - Packet kind identifies full versus delta payloads
     - Packet frame data still round-trips through serialization

     FAILURE MEANS:
     - Clients cannot know whether to replace or patch state
     - Delta snapshots may be interpreted as full snapshots
    */
    [Fact]
    public void SerializePacket_ShouldRoundTripPacketKindAndFrame()
    {
        var packet = new SnapshotPacket(
            SnapshotPacketKind.Delta,
            new SnapshotFrame
            {
                Tick = 42,
                States = new Dictionary<int, EntityState>
                {
                    [7] = new EntityState
                    {
                        Position = new Vector3(1, 2, 3),
                        Rotation = Quaternion.Identity,
                        Velocity = Vector3.Zero,
                        StateFlags = 11
                    },
                    [12] = new EntityState
                    {
                        Position = new Vector3(4, 5, 6),
                        Rotation = Quaternion.Identity,
                        Velocity = Vector3.Zero,
                        StateFlags = 13
                    }
                }
            });

        var bytes = SnapshotSerializer.Serialize(packet);
        var deserialized = SnapshotSerializer.DeserializePacket(bytes);

        Assert.Equal(SnapshotPacketKind.Delta, deserialized.Kind);
        Assert.Equal(42, deserialized.Frame.Tick);
        Assert.Equal(2, deserialized.Frame.States.Count);
        Assert.Equal(new Vector3(4, 5, 6), deserialized.Frame.States[12].Position);
    }

    /*
     PURPOSE:
     Ensure corrupted snapshot packets fail predictably.

     DESIGN RULE:
     - Truncated packets are rejected
     - Deserialization reports invalid packet format

     FAILURE MEANS:
     - Clients may apply partial corrupted snapshots
     - Network corruption may crash outside the serializer boundary
    */
    [Fact]
    public void Deserialize_ShouldRejectTruncatedPacket()
    {
        Assert.Throws<InvalidDataException>(() =>
            SnapshotSerializer.Deserialize(new byte[] { 1, 2, 3 }));
    }

    /*
     PURPOSE:
     Ensure impossible state counts are rejected.

     DESIGN RULE:
     - State count cannot be negative
     - Packet headers are validated before reading states

     FAILURE MEANS:
     - Corrupted packets may be accepted as valid snapshots
     - Clients may hide malformed network data
    */
    [Fact]
    public void Deserialize_ShouldRejectNegativeStateCount()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(42L);
        writer.Write(-1);

        Assert.Throws<InvalidDataException>(() =>
            SnapshotSerializer.Deserialize(stream.ToArray()));
    }
}
