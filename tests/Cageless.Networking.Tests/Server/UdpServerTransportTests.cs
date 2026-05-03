using System.Net;
using Xunit;

public class UdpServerTransportTests
{
    /*
     PURPOSE:
     Ensure the UDP server transport can bind and shut down cleanly.

     DESIGN RULE:
     - Transport owns the UDP socket lifecycle
     - Disposing transport is safe even if called more than once

     FAILURE MEANS:
     - Server nodes may leak UDP ports
     - Scene shutdown may crash during cleanup
    */
    [Fact]
    public void Start_ShouldBindUdpPortAndDisposeCleanly()
    {
        using var transport = new UdpServerTransport();

        transport.Start(port: 0);
        transport.Dispose();
        transport.Dispose();
    }

    /*
     PURPOSE:
     Ensure UDP transport tracks known client endpoints.

     DESIGN RULE:
     - Registered endpoints become connected snapshot clients
     - Client ids are exposed for server queue flushing

     FAILURE MEANS:
     - Server may not know where to send snapshots
     - Connected clients may not receive outbound updates
    */
    [Fact]
    public void RegisterClientEndpoint_ShouldExposeConnectedClient()
    {
        using var transport = new UdpServerTransport();
        var clientId = new ClientId(3);

        transport.RegisterClientEndpoint(clientId, new IPEndPoint(IPAddress.Loopback, 9999));

        Assert.Contains(clientId, transport.ConnectedClients);
    }
}
