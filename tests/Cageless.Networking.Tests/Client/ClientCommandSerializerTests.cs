using System.IO;
using System.Linq;
using Godot;
using Xunit;

public class ClientCommandSerializerTests
{
    /*
     PURPOSE:
     Ensure movement intent commands serialize and deserialize correctly.

     DESIGN RULE:
     - Clients send input intent, not authoritative transforms
     - Movement command carries move direction and jump state

     FAILURE MEANS:
     - Server may not reconstruct client input intent
     - Client packets may drift toward position authority
    */
    [Fact]
    public void Serialize_ShouldRoundTripMovementCommand()
    {
        var packet = new ClientCommandPacket(
            new ClientId(3),
            sequence: 12,
            ClientCommandKind.Movement,
            new MovementCommand(new Vector2(1, -1), jumpPressed: true));

        var bytes = ClientCommandSerializer.Serialize(packet);
        var deserialized = ClientCommandSerializer.Deserialize(bytes);

        Assert.Equal(new ClientId(3), deserialized.ClientId);
        Assert.Equal(12, deserialized.Sequence);
        Assert.Equal(ClientCommandKind.Movement, deserialized.Kind);
        Assert.Equal(new Vector2(1, -1), deserialized.Movement.MoveDirection);
        Assert.True(deserialized.Movement.JumpPressed);
    }

    /*
     PURPOSE:
     Ensure command packets preserve ordering metadata.

     DESIGN RULE:
     - Sequence numbers are included for UDP deduplication
     - Server can reject duplicate or older commands

     FAILURE MEANS:
     - Server may process commands out of order
     - Duplicate UDP packets may be applied more than once
    */
    [Fact]
    public void Serialize_ShouldPreserveClientIdAndSequence()
    {
        var packet = new ClientCommandPacket(
            new ClientId(9),
            sequence: 44,
            ClientCommandKind.Movement,
            new MovementCommand(Vector2.Zero, jumpPressed: false));

        var deserialized = ClientCommandSerializer.Deserialize(
            ClientCommandSerializer.Serialize(packet));

        Assert.Equal(new ClientId(9), deserialized.ClientId);
        Assert.Equal(44, deserialized.Sequence);
    }

    /*
     PURPOSE:
     Ensure client commands do not carry authoritative position fields.

     DESIGN RULE:
     - Movement commands are player intent only
     - Position remains server-authoritative

     FAILURE MEANS:
     - Clients may be able to submit authoritative transforms
     - Cheating and desync become easier
    */
    [Fact]
    public void MovementCommand_ShouldNotExposePositionFields()
    {
        var memberNames = typeof(MovementCommand)
            .GetMembers()
            .Select(member => member.Name);

        Assert.DoesNotContain(memberNames, name => name.Contains("Position"));
    }

    /*
     PURPOSE:
     Ensure corrupted client command packets fail predictably.

     DESIGN RULE:
     - Truncated packets are rejected
     - Deserialization reports invalid packet format

     FAILURE MEANS:
     - Server may accept malformed input packets
     - Network corruption may crash outside the command serializer boundary
    */
    [Fact]
    public void Deserialize_ShouldRejectTruncatedPacket()
    {
        Assert.Throws<InvalidDataException>(() =>
            ClientCommandSerializer.Deserialize(new byte[] { 1, 2, 3 }));
    }
}
