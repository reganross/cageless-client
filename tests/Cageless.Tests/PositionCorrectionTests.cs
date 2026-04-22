using Godot;
using Xunit;

namespace Cageless.Tests;

public class PositionCorrectionTests
{
	[Fact]
	public void Apply_WhenDistanceAboveLargeThreshold_SnapsToTarget()
	{
		var current = Vector3.Zero;
		var target = new Vector3(2f, 0f, 0f);

		var result = PositionCorrection.Apply(current, target, deltaTime: 0.016f);

		Assert.Equal(target, result);
	}

	[Fact]
	public void Apply_WhenDistanceBelowLargeThreshold_StepsTowardTarget()
	{
		var current = Vector3.Zero;
		var target = new Vector3(0.04f, 0f, 0f);

		var result = PositionCorrection.Apply(current, target, deltaTime: 0.01f);

		Assert.True(result.Length() > 0f);
		Assert.True(result.Length() < target.Length());
	}
}
