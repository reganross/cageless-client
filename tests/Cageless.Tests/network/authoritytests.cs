public class AuthorityTests
{
    /*
     PURPOSE:
     Enforce server-authoritative model.

     DESIGN RULE:
     - Only server/host can mutate world state
     - Clients may ONLY send requests

     THIS INCLUDES:
     - enemy spawning
     - world generation
     - segment creation

     FAILURE MEANS:
     - cheating becomes possible
     - desync between players
    */
    [Fact]
    public void Client_ShouldNotMutateWorldState()
    {
        var server = new WorldGenerator();
        var client = new ClientProxy(server);

        client.TrySpawnEnemy();

        Assert.True(
            server.EnemyCount == 0,
            "Client incorrectly modified server state"
        );
    }
}