using Godot;
using Xunit;

public class ServerPlayerEntityTests
{
    /*
     PURPOSE:
     Ensure server-side player movement uses the player's facing direction.

     DESIGN RULE:
     - Controller movement intent is local to the character's yaw
     - Server truth must match client-side local prediction direction

     FAILURE MEANS:
     - Server correction pulls players along world axes instead of facing-relative axes
     - Controls feel wrong once network truth is applied
    */
    [Fact]
    public void Simulate_ShouldMoveRelativeToLookYaw()
    {
        var player = new ServerPlayerEntity(new ClientId(1));
        var controller = new PlayerController(new ClientId(1), tick: 1);
        controller.SetActionStrength("forward", 1);
        controller.SetLookRotation(Mathf.Pi / 2f, 0);

        player.Simulate(controller);

        var state = player.CaptureState();
        Assert.True(state.Velocity.X < -0.1f);
        Assert.True(Mathf.Abs(state.Velocity.Z) < 0.001f);
    }
}
