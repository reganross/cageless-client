public class ClientRequestTests
{
    /*
     PURPOSE:
     Ensure clients do NOT directly mutate world state.

     DESIGN RULE:
     - Clients send requests
     - Server executes logic

     FAILURE MEANS:
     - Desync
     - Potential exploits
    */
    [Fact]
    public void Client_ShouldSendRequest_NotExecuteAction()
    {
        var client = new ClientProxy();

        var request = client.RequestSpawnEnemy();

        Assert.True(request.IsSent);
        Assert.False(client.HasSpawnedEnemy);
    }
}