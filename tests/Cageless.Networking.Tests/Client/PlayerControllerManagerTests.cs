using Xunit;

public class PlayerControllerManagerTests
{
    /*
     PURPOSE:
     Ensure controllers are created for known player ids.

     DESIGN RULE:
     - Each connected player has one controller object
     - Controller ownership is keyed by player id

     FAILURE MEANS:
     - Server may not have a controller for a connected player
     - Player input may not have an authoritative storage location
    */
    [Fact]
    public void GetOrCreate_ShouldCreateControllerForPlayerId()
    {
        var manager = new PlayerControllerManager();
        var playerId = new ClientId(1);

        var controller = manager.GetOrCreate(playerId);

        Assert.True(controller.HasPlayerId);
        Assert.Equal(playerId, controller.PlayerId);
    }

    /*
     PURPOSE:
     Ensure repeated lookups return the same controller instance.

     DESIGN RULE:
     - Player characters can hold controller references
     - Network updates mutate the existing controller instance

     FAILURE MEANS:
     - Spawned player characters may keep stale controller references
     - Incoming packets may replace state objects invisibly
    */
    [Fact]
    public void GetOrCreate_ShouldReturnSameControllerInstance()
    {
        var manager = new PlayerControllerManager();
        var playerId = new ClientId(1);

        var first = manager.GetOrCreate(playerId);
        var second = manager.GetOrCreate(playerId);

        Assert.Same(first, second);
    }

    /*
     PURPOSE:
     Ensure incoming controller snapshots update stored controllers.

     DESIGN RULE:
     - Packet state is copied into the managed controller
     - Stored controller instance is not replaced

     FAILURE MEANS:
     - Server player simulation may read stale input
     - Non-local player characters may not reflect received packets
    */
    [Fact]
    public void Apply_ShouldUpdateExistingControllerInPlace()
    {
        var manager = new PlayerControllerManager();
        var playerId = new ClientId(1);
        var stored = manager.GetOrCreate(playerId);
        var incoming = new PlayerController(
            playerId,
            tick: 2,
            new[]
            {
                new InputActionState("forward", 1)
            });
        incoming.SetLookRotation(1.5f, -0.25f);

        var applied = manager.Apply(incoming);

        Assert.True(applied);
        Assert.Same(stored, manager.GetOrCreate(playerId));
        Assert.Equal(2, stored.Tick);
        Assert.Equal(1, stored.GetActionStrength("forward"));
        Assert.Equal(1.5f, stored.LookYaw);
        Assert.Equal(-0.25f, stored.LookPitch);
    }

    /*
     PURPOSE:
     Ensure controller updates are isolated per player.

     DESIGN RULE:
     - Updating one player controller does not mutate another
     - Player ids define separate controller state

     FAILURE MEANS:
     - One player's input may affect another player
     - Multiplayer simulation may apply input to the wrong character
    */
    [Fact]
    public void Apply_ShouldNotUpdateOtherPlayerControllers()
    {
        var manager = new PlayerControllerManager();
        var first = manager.GetOrCreate(new ClientId(1));
        var second = manager.GetOrCreate(new ClientId(2));

        manager.Apply(new PlayerController(
            new ClientId(1),
            tick: 1,
            new[]
            {
                new InputActionState("right", 1)
            }));

        Assert.Equal(1, first.GetActionStrength("right"));
        Assert.Equal(0, second.GetActionStrength("right"));
    }

    /*
     PURPOSE:
     Ensure unknown player updates fail predictably.

     DESIGN RULE:
     - Apply only updates controllers that already exist
     - Connection flow owns controller creation

     FAILURE MEANS:
     - Spoofed packets may create player controllers
     - Unknown players may enter simulation state implicitly
    */
    [Fact]
    public void Apply_ShouldRejectUnknownPlayer()
    {
        var manager = new PlayerControllerManager();
        var incoming = new PlayerController(
            new ClientId(99),
            tick: 1,
            new[]
            {
                new InputActionState("forward", 1)
            });

        Assert.False(manager.Apply(incoming));
        Assert.False(manager.TryGet(new ClientId(99), out _));
    }

    /*
     PURPOSE:
     Ensure delta controller packets mutate only included fields.

     DESIGN RULE:
     - Delta packets update changed action and look fields in place
     - Unchanged controller fields remain available from the previous baseline

     FAILURE MEANS:
     - Delta packets may erase controller state that was not sent
     - Non-local player characters may lose held inputs between full packets
    */
    [Fact]
    public void Apply_ShouldMergeDeltaCommandIntoExistingController()
    {
        var manager = new PlayerControllerManager();
        var playerId = new ClientId(1);
        var stored = manager.GetOrCreate(playerId);
        stored.SetActionStrength("forward", 1);
        stored.SetLookRotation(0.5f, 0.25f);

        var delta = new PlayerController(
            playerId,
            tick: 2,
            new[]
            {
                new InputActionState("right", 1)
            });
        var command = new ClientCommandPacket(
            ClientCommandKind.Controller,
            ControllerPacketKind.Delta,
            hasLookRotation: false,
            delta);

        Assert.True(manager.Apply(command));
        Assert.Equal(2, stored.Tick);
        Assert.Equal(1, stored.GetActionStrength("forward"));
        Assert.Equal(1, stored.GetActionStrength("right"));
        Assert.Equal(0.5f, stored.LookYaw);
        Assert.Equal(0.25f, stored.LookPitch);
    }
}
