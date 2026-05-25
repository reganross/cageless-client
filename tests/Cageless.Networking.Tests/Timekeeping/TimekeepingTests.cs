using Xunit;

public class TimekeepingTests
{
    /*
     PURPOSE:
     Ensure normalized time cannot leave the animation sampling range.

     DESIGN RULE:
     - NormalizedTime clamps values to the inclusive 0..1 range
     - Callers can safely use the value for interpolation percentages

     FAILURE MEANS:
     - Animation and lag-compensation sampling may extrapolate past authored poses
     - Invalid timing data may leak into reconstruction code
    */
    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1.5f, 1f)]
    public void NormalizedTime_ShouldClampToUnitRange(float input, float expected)
    {
        var time = new NormalizedTime(input);

        Assert.Equal(expected, time.Value);
    }

    /*
     PURPOSE:
     Ensure milliseconds convert to whole network ticks at the configured rate.

     DESIGN RULE:
     - Tick conversion is explicit at timekeeping boundaries
     - Fractional ticks round to the nearest whole Tick value

     FAILURE MEANS:
     - Snapshot history windows may keep the wrong number of frames
     - Client and server timing code may disagree about elapsed ticks
    */
    [Fact]
    public void ToTicks_ShouldConvertMillisecondsUsingTickRate()
    {
        var ticks = TimeConverter.ToTicks(new Milliseconds(240f), tickRate: 30f);

        Assert.Equal(new Tick(7), ticks);
    }

    /*
     PURPOSE:
     Ensure ticks convert back to elapsed milliseconds.

     DESIGN RULE:
     - Time conversion remains reversible enough for network timing configuration
     - Tick values carry intent instead of raw integers at the call site

     FAILURE MEANS:
     - Lag-compensation windows may be displayed or configured with incorrect time
     - Tests may hide accidental seconds/milliseconds mixups
    */
    [Fact]
    public void ToMilliseconds_ShouldConvertTicksUsingTickRate()
    {
        var milliseconds = TimeConverter.ToMilliseconds(new Tick(6), tickRate: 30f);

        Assert.Equal(200f, milliseconds.Value);
    }

    /*
     PURPOSE:
     Ensure tick arithmetic keeps network tick values explicit.

     DESIGN RULE:
     - Tick addition and subtraction return Tick values
     - Tick comparisons use the wrapped tick value

     FAILURE MEANS:
     - Network code may fall back to ambiguous integer timing
     - Command ordering and snapshot ordering may compare the wrong units
    */
    [Fact]
    public void Tick_ShouldSupportArithmeticAndOrdering()
    {
        var first = new Tick(10);
        var second = new Tick(3);

        Assert.Equal(new Tick(13), first + second);
        Assert.Equal(new Tick(7), first - second);
        Assert.True(first > second);
        Assert.True(second < first);
    }

    /*
     PURPOSE:
     Ensure tick values can advance with standard increment syntax.

     DESIGN RULE:
     - Increment and decrement preserve the Tick wrapper
     - Call sites can advance network ticks without falling back to raw integers

     FAILURE MEANS:
     - Network tick code may become noisy or reintroduce primitive timing values
     - Tick advancement may lose the type-safety signal at important boundaries
    */
    [Fact]
    public void Tick_ShouldSupportIncrementAndDecrement()
    {
        var tick = new Tick(10);

        tick++;
        Assert.Equal(new Tick(11), tick);

        tick--;
        Assert.Equal(new Tick(10), tick);
    }
}
