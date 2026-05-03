using System.IO;
using System.Linq;
using Godot;
using Xunit;

public class PlayerControllerSerializerTests
{
    /*
     PURPOSE:
     Ensure player controller state can be sent over the network.

     DESIGN RULE:
     - Controller state is tied to the player's client id
     - Controller state carries input intent, not authoritative position

     FAILURE MEANS:
     - Server may not know which player owns an input controller
     - Controller packets may drift toward client-authoritative transforms
    */
    [Fact]
    public void Serialize_ShouldRoundTripPlayerController()
    {
        var controller = new PlayerController(
            new ClientId(3),
                tick: 12,
            new[]
            {
                new InputActionState("left", 0),
                new InputActionState("right", 1),
                new InputActionState("forward", 1),
                new InputActionState("back", 0),
                new InputActionState("ui_accept", 1)
            });
        controller.SetLookRotation(yaw: 0.25f, pitch: -0.5f);

        var bytes = PlayerControllerSerializer.Serialize(controller);
        var deserialized = PlayerControllerSerializer.Deserialize(bytes);

        Assert.Equal(new ClientId(3), deserialized.PlayerId);
        Assert.Equal(12, deserialized.Tick);
        Assert.Equal(5, deserialized.Actions.Count);
        Assert.Equal(1, deserialized.GetActionStrength("right"));
        Assert.Equal(1, deserialized.GetActionStrength("forward"));
        Assert.Equal(1, deserialized.GetActionStrength("ui_accept"));
        Assert.Equal(0.25f, deserialized.LookYaw);
        Assert.Equal(-0.5f, deserialized.LookPitch);
    }

    /*
     PURPOSE:
     Ensure controller packets preserve player ownership.

     DESIGN RULE:
     - Each controller is associated with one player id
     - Player id survives network serialization

     FAILURE MEANS:
     - Input may be applied to the wrong player
     - Server command queues may receive ambiguous controller data
    */
    [Fact]
    public void Serialize_ShouldPreservePlayerId()
    {
        var controller = new PlayerController(
            new ClientId(9),
                tick: 44,
            new[]
            {
                new InputActionState("forward", 1)
            });

        var deserialized = PlayerControllerSerializer.Deserialize(
            PlayerControllerSerializer.Serialize(controller));

        Assert.Equal(new ClientId(9), deserialized.PlayerId);
    }

    /*
     PURPOSE:
     Ensure controller state does not expose authoritative position.

     DESIGN RULE:
     - Controller state represents player input intent
     - Position remains owned by server simulation

     FAILURE MEANS:
     - Clients may send authoritative transforms through controller packets
     - Cheating and desync become easier
    */
    [Fact]
    public void PlayerController_ShouldNotExposePositionFields()
    {
        var memberNames = typeof(PlayerController)
            .GetMembers()
            .Select(member => member.Name);

        Assert.DoesNotContain(memberNames, name => name.Contains("Position"));
    }

    /*
     PURPOSE:
     Ensure controller state can become a server command packet.

     DESIGN RULE:
     - Server command queue consumes controller commands
     - Controller conversion keeps player id and tick

     FAILURE MEANS:
     - Controller input cannot enter authoritative simulation queue
     - Player ownership may be lost during command conversion
    */
    [Fact]
    public void ToCommandPacket_ShouldCreateControllerCommand()
    {
        var controller = new PlayerController(
            new ClientId(3),
            tick: 12,
            new[]
            {
                new InputActionState("left", 0),
                new InputActionState("right", 1),
                new InputActionState("forward", 1),
                new InputActionState("back", 0),
                new InputActionState("ui_accept", 1)
            });

        var command = controller.ToCommandPacket();

        Assert.Equal(new ClientId(3), command.ClientId);
        Assert.Equal(12, command.Tick);
        Assert.Equal(ClientCommandKind.Controller, command.Kind);
        Assert.Equal(1, command.Controller.GetActionStrength("right"));
        Assert.Equal(1, command.Controller.GetActionStrength("forward"));
        Assert.Equal(1, command.Controller.GetActionStrength("ui_accept"));
    }

    /*
     PURPOSE:
     Ensure controller state reflects action names from the input map.

     DESIGN RULE:
     - Controller state stores input action strengths by action name
     - Movement commands are derived from action state, not stored directly

     FAILURE MEANS:
     - Controller snapshots may stop matching Godot input map state
     - Rebinding or adding input actions becomes harder to support
    */
    [Fact]
    public void PlayerController_ShouldExposeInputActionStates()
    {
        var controller = new PlayerController(
            new ClientId(3),
            tick: 12,
            new[]
            {
                new InputActionState("forward", 1),
                new InputActionState("back", 0)
            });

        Assert.Equal(1, controller.GetActionStrength("forward"));
        Assert.Equal(0, controller.GetActionStrength("back"));
        Assert.Equal(0, controller.GetActionStrength("missing"));
    }

    /*
     PURPOSE:
     Ensure controller state can be updated from process-time input polling.

     DESIGN RULE:
     - Controller stores the latest action strength for each action
     - Repeated updates replace previous action state

     FAILURE MEANS:
     - Physics simulation may read stale input
     - Server may receive old controller state
    */
    [Fact]
    public void SetActionStrength_ShouldUpdateCurrentActionState()
    {
        var controller = new PlayerController(new ClientId(3), tick: 12);

        controller.SetActionStrength("forward", 1);
        controller.SetActionStrength("forward", 0);
        controller.SetActionStrength("right", 1);

        Assert.Equal(0, controller.GetActionStrength("forward"));
        Assert.Equal(1, controller.GetActionStrength("right"));
    }

    /*
     PURPOSE:
     Ensure controller state includes current look rotation.

     DESIGN RULE:
     - _Input updates controller look yaw and pitch
     - Physics simulation turns the body toward controller yaw

     FAILURE MEANS:
     - Server cannot reproduce the player's intended facing direction
     - Local and server character rotation may diverge
    */
    [Fact]
    public void SetLookRotation_ShouldUpdateCurrentLookState()
    {
        var controller = new PlayerController(new ClientId(3), tick: 12);

        controller.SetLookRotation(yaw: 1.5f, pitch: -0.25f);

        Assert.Equal(1.5f, controller.LookYaw);
        Assert.Equal(-0.25f, controller.LookPitch);
    }

    /*
     PURPOSE:
     Ensure serialized controller packets include look rotation.

     DESIGN RULE:
     - Network controller snapshots include current camera-facing intent
     - Server simulation receives the same look target as local physics

     FAILURE MEANS:
     - Remote/server player facing may not match local camera intent
     - Rotation replay may be impossible from controller packets
    */
    [Fact]
    public void Serialize_ShouldUseLatestLookState()
    {
        var controller = new PlayerController(new ClientId(3), tick: 12);

        controller.SetLookRotation(yaw: 1.5f, pitch: -0.25f);

        var deserialized = PlayerControllerSerializer.Deserialize(
            PlayerControllerSerializer.Serialize(controller));

        Assert.Equal(1.5f, deserialized.LookYaw);
        Assert.Equal(-0.25f, deserialized.LookPitch);
    }

    /*
     PURPOSE:
     Ensure serialized controller packets reflect the latest mutable state.

     DESIGN RULE:
     - _Process updates the controller object
     - Network serialization sends a snapshot of current controller state

     FAILURE MEANS:
     - Server may receive a controller snapshot that does not match current input
     - Local and server simulation may use different input state
    */
    [Fact]
    public void Serialize_ShouldUseLatestActionState()
    {
        var controller = new PlayerController(new ClientId(3), tick: 12);

        controller.SetActionStrength("forward", 1);
        controller.SetActionStrength("ui_accept", 1);

        var deserialized = PlayerControllerSerializer.Deserialize(
            PlayerControllerSerializer.Serialize(controller));

        Assert.Equal(1, deserialized.GetActionStrength("forward"));
        Assert.Equal(1, deserialized.GetActionStrength("ui_accept"));
    }

    /*
     PURPOSE:
     Ensure physics code can consume controller movement consistently.

     DESIGN RULE:
     - Movement vector is derived from current action strengths
     - Local and server simulation can use the same controller helper

     FAILURE MEANS:
     - Client prediction and server simulation may interpret input differently
     - Directional input may be duplicated across systems
    */
    [Fact]
    public void GetMoveDirection_ShouldDeriveDirectionFromActionState()
    {
        var controller = new PlayerController(new ClientId(3), tick: 12);

        controller.SetActionStrength("right", 1);
        controller.SetActionStrength("forward", 1);

        Assert.Equal(new Vector2(1, -1), controller.GetMoveDirection());
    }

    /*
     PURPOSE:
     Ensure local player controllers do not require a network id.

     DESIGN RULE:
     - Local input can drive local physics before network ownership exists
     - Network serialization still requires a player id

     FAILURE MEANS:
     - Local player simulation may be blocked on network identity
     - Anonymous controller state may be sent to the server
    */
    [Fact]
    public void LocalController_ShouldNotRequirePlayerIdUntilSerialized()
    {
        var controller = new PlayerController();

        controller.SetActionStrength("forward", 1);

        Assert.False(controller.HasPlayerId);
        Assert.Equal(new Vector2(0, -1), controller.GetMoveDirection());
        Assert.Throws<InvalidDataException>(() => PlayerControllerSerializer.Serialize(controller));
    }

    /*
     PURPOSE:
     Ensure corrupted controller packets fail predictably.

     DESIGN RULE:
     - Truncated packets are rejected
     - Deserialization reports invalid packet format

     FAILURE MEANS:
     - Server may accept malformed controller input
     - Network corruption may crash outside the serializer boundary
    */
    [Fact]
    public void Deserialize_ShouldRejectTruncatedPacket()
    {
        Assert.Throws<InvalidDataException>(() =>
            PlayerControllerSerializer.Deserialize(new byte[] { 1, 2, 3 }));
    }
}
