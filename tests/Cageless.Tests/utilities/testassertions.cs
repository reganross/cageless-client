public static class TestAssertions
{
    public static void AssertDifficultyInRange(
        EnemyGroup group,
        float target,
        float tolerance,
        string context)
    {
        if (group.Difficulty < target - tolerance || group.Difficulty > target + tolerance)
        {
            throw new Exception(
                $"[Difficulty Out of Range]\n" +
                $"{context}\n" +
                $"Expected: {target} ± {tolerance}\n" +
                $"Actual: {group.Difficulty}\n" +
                $"GroupId: {group.Id}"
            );
        }
    }
}