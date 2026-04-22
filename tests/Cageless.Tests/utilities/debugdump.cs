public static class DebugDump
{
    public static string DumpEnemyGroup(EnemyGroup g)
    {
        return
            $"EnemyGroup\n" +
            $"Id: {g.Id}\n" +
            $"Difficulty: {g.Difficulty}\n" +
            $"Tags: {string.Join(",", g.Tags)}";
    }
}