public class SyncTests
{
    /*
     PURPOSE:
     Ensure client and server remain synchronized.

     DESIGN RULE:
     - Server is source of truth
     - Client must match after updates

     FAILURE MEANS:
     - Different game states per player
    */
    [Fact]
    public void ClientAndServer_ShouldStayInSync()
    {
        var server = new WorldGenerator();
        var client = new ClientProxy(server);

        for (int i = 0; i < 50; i++)
        {
            server.Tick();
            client.Sync(server.GetState());
        }

        Assert.Equal(
            server.GetStateHash(),
            client.GetStateHash()
        );
    }
}