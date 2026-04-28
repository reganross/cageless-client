using Xunit;

public class NetworkTickClockTests
{
    /*
     PURPOSE:
     Ensure sub-tick time does not advance the network tick.

     DESIGN RULE:
     - The network input clock runs at 20Hz
     - Less than 50ms is accumulated but not emitted as a completed tick

     FAILURE MEANS:
     - Client input ticks may run faster than intended
     - Controller packets may be sent too frequently
    */
    [Fact]
    public void Advance_ShouldNotCompleteTickBeforeInterval()
    {
        var clock = new NetworkTickClock();
        var advancer = clock.CreateAdvancer();

        var completed = advancer.Advance(0.049);

        Assert.Equal(0, completed);
        Assert.Equal(0, clock.CurrentTick);
        Assert.Equal(0, clock.PendingTicks);
    }

    /*
     PURPOSE:
     Ensure one 50ms interval advances one network tick.

     DESIGN RULE:
     - 20Hz input ticks complete every 50ms
     - Completed ticks remain pending until a client consumes them

     FAILURE MEANS:
     - Client and server input tick numbers may drift
     - NetworkClient may miss completed input ticks
    */
    [Fact]
    public void Advance_ShouldCompleteOneTickAtInterval()
    {
        var clock = new NetworkTickClock();
        var advancer = clock.CreateAdvancer();

        var completed = advancer.Advance(0.05);

        Assert.Equal(1, completed);
        Assert.Equal(1, clock.CurrentTick);
        Assert.Equal(1, clock.PendingTicks);
    }

    /*
     PURPOSE:
     Ensure small physics deltas accumulate into network ticks.

     DESIGN RULE:
     - Clock accumulation survives across advance calls
     - Completed ticks are based on total elapsed time

     FAILURE MEANS:
     - High-framerate physics may never emit input ticks
     - Controller send cadence may depend on frame rate
    */
    [Fact]
    public void Advance_ShouldAccumulateSmallDeltas()
    {
        var clock = new NetworkTickClock();
        var advancer = clock.CreateAdvancer();

        Assert.Equal(0, advancer.Advance(0.02));
        Assert.Equal(0, advancer.Advance(0.02));
        Assert.Equal(1, advancer.Advance(0.01));

        Assert.Equal(1, clock.CurrentTick);
        Assert.Equal(1, clock.PendingTicks);
    }

    /*
     PURPOSE:
     Ensure large physics deltas do not drop network ticks.

     DESIGN RULE:
     - One advance call can complete multiple ticks
     - Pending ticks preserve each tick for later processing

     FAILURE MEANS:
     - Frame hitches may skip input ticks
     - Full controller refresh cadence may become inconsistent
    */
    [Fact]
    public void Advance_ShouldCompleteMultipleTicksFromLargeDelta()
    {
        var clock = new NetworkTickClock();
        var advancer = clock.CreateAdvancer();

        var completed = advancer.Advance(0.16);

        Assert.Equal(3, completed);
        Assert.Equal(3, clock.CurrentTick);
        Assert.Equal(3, clock.PendingTicks);
    }

    /*
     PURPOSE:
     Ensure pending ticks are consumed in order.

     DESIGN RULE:
     - NetworkClient processes completed ticks separately from clock advancement
     - Tick numbers are monotonically ordered

     FAILURE MEANS:
     - Controller packets may be sent with skipped or reversed tick numbers
     - Server tick validation may reject valid input
    */
    [Fact]
    public void TryRequestTick_ShouldReturnPendingTicksInOrder()
    {
        var clock = new NetworkTickClock();
        var advancer = clock.CreateAdvancer();
        advancer.Advance(0.15);

        Assert.True(clock.TryRequestTick(out var first));
        Assert.True(clock.TryRequestTick(out var second));
        Assert.True(clock.TryRequestTick(out var third));
        Assert.False(clock.TryRequestTick(out _));

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(3, third);
        Assert.Equal(0, clock.PendingTicks);
    }

    /*
     PURPOSE:
     Ensure multiple systems can observe the same externally advanced clock.

     DESIGN RULE:
     - Tick cursors track consumption independently per system
     - One consumer cannot drain ticks before another system sees them

     FAILURE MEANS:
     - Client and server cannot share one scene-level network tick driver
     - Processing order may decide which systems receive ticks
    */
    [Fact]
    public void TickCursor_ShouldAllowIndependentConsumers()
    {
        var clock = new NetworkTickClock(tickIntervalSeconds: 0.05);
        var firstConsumer = clock.CreateCursor();
        var secondConsumer = clock.CreateCursor();
        var advancer = clock.CreateAdvancer();

        advancer.Advance(0.1);

        Assert.True(firstConsumer.TryRequestTick(out var firstTick));
        Assert.True(firstConsumer.TryRequestTick(out var secondTick));
        Assert.False(firstConsumer.TryRequestTick(out _));

        Assert.True(secondConsumer.TryRequestTick(out var mirroredFirstTick));
        Assert.True(secondConsumer.TryRequestTick(out var mirroredSecondTick));
        Assert.False(secondConsumer.TryRequestTick(out _));

        Assert.Equal(1, firstTick);
        Assert.Equal(2, secondTick);
        Assert.Equal(1, mirroredFirstTick);
        Assert.Equal(2, mirroredSecondTick);
    }

    /*
     PURPOSE:
     Ensure only one object can own clock advancement.

     DESIGN RULE:
     - The clock exposes exactly one advance capability
     - Additional advance owners are rejected predictably

     FAILURE MEANS:
     - Multiple game objects may advance the same network clock
     - Client and server tick counts may drift from double advancement
    */
    [Fact]
    public void CreateAdvancer_ShouldRejectSecondAdvancer()
    {
        var clock = new NetworkTickClock();

        Assert.NotNull(clock.CreateAdvancer());
        Assert.Throws<System.InvalidOperationException>(() => clock.CreateAdvancer());
    }

    /*
     PURPOSE:
     Ensure clock advancement ownership can move to another object.

     DESIGN RULE:
     - Disposing an advancer releases the clock's advance capability
     - Disposed advancers cannot keep advancing the clock

     FAILURE MEANS:
     - Clock ownership may get stuck on a despawned object
     - Multiple stale objects may advance network time
    */
    [Fact]
    public void Dispose_ShouldReleaseAdvancerOwnership()
    {
        var clock = new NetworkTickClock();
        var first = clock.CreateAdvancer();

        first.Dispose();
        var second = clock.CreateAdvancer();

        Assert.NotNull(second);
        Assert.Throws<System.ObjectDisposedException>(() => first.Advance(0.05));
    }
}
