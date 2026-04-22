public class TestWorldBuilder
{
    public WorldGenerator CreateGenerator()
    {
        return new WorldGenerator();
    }

    public EnemyGroup GetGroup(int seed, int depth, int players)
    {
        var gen = CreateGenerator();
        return gen.GetEnemyGroup(seed, depth, players);
    }

    public Segment GetSegment(int seed, int depth, int players)
    {
        var gen = CreateGenerator();
        return gen.GenerateSegment(seed, depth, players);
    }
}