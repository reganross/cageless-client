using System.IO;
using Godot;
using Xunit;

public class ClientCommandSerializerTests
{
    /*
     PURPOSE:
     Ensure controller commands serialize and deserialize correctly.

     DESIGN RULE:
     - Clients send input intent, not authoritative transforms
     - Controller command carries the current input map state

     FAILURE MEANS:
     - Server may not reconstruct client input intent
     - Client packets may drift toward position authority
    */
    [Fact]
    public void Serialize_ShouldRoundTripControllerCommand()
    {
        var packet = new ClientCommandPacket(
            ClientCommandKind.Controller,
            ControllerPacketKind.Full,
            hasLookRotation: true,
            new PlayerController(
                new ClientId(3),
                tick: 12,
                new[]
                {
                    new InputActionState("right", 1),
                    new InputActionState("forward", 1),
                    new InputActionState("ui_accept", 1)
                }));

        var bytes = ClientCommandSerializer.Serialize(packet);
        var deserialized = ClientCommandSerializer.Deserialize(bytes);

        Assert.Equal(new ClientId(3), deserialized.ClientId);
        Assert.Equal(12, deserialized.Tick);
        Assert.Equal(ControllerPacketKind.Full, deserialized.ControllerPacketKind);
        Assert.Equal(ClientCommandKind.Controller, deserialized.Kind);
        Assert.Equal(1, deserialized.Controller.GetActionStrength("right"));
        Assert.Equal(1, deserialized.Controller.GetActionStrength("forward"));
        Assert.Equal(1, deserialized.Controller.GetActionStrength("ui_accept"));
    }

    /*
     PURPOSE:
     Ensure command packets preserve tick metadata.

     DESIGN RULE:
     - Controller tick numbers are included for UDP deduplication
     - Server can reject duplicate or older controller ticks

     FAILURE MEANS:
     - Server may process controller ticks out of order
     - Duplicate UDP packets may be applied more than once
    */
    [Fact]
    public void Serialize_ShouldPreserveClientIdAndTick()
    {
        var packet = new ClientCommandPacket(
            ClientCommandKind.Controller,
            ControllerPacketKind.Full,
            hasLookRotation: true,
            new PlayerController(
                new ClientId(9),
                tick: 44,
                new[]
                {
                    new InputActionState("forward", 1)
                }));

        var deserialized = ClientCommandSerializer.Deserialize(
            ClientCommandSerializer.Serialize(packet));

        Assert.Equal(new ClientId(9), deserialized.ClientId);
        Assert.Equal(44, deserialized.Tick);
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
