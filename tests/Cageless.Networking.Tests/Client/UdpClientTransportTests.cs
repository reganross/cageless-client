using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

public class UdpClientTransportTests
{
    /*
     PURPOSE:
     Ensure client transport sends bytes to the configured server endpoint.

     DESIGN RULE:
     - UdpClientTransport sends exactly the bytes provided by NetworkClient
     - Packet framing remains owned by NetworkClient, not the UDP transport

     FAILURE MEANS:
     - Connect/controller/disconnect bytes may not reach the server
     - UDP transport may mutate packet payloads
    */
    [Fact]
    public async Task Send_ShouldEmitBytesToServerEndpoint()
    {
        using var listener = new UdpClient(0);
        var serverPort = GetLocalPort(listener);
        using var transport = new UdpClientTransport("127.0.0.1", serverPort);
        byte[] expected = { 1, 2, 3, 4 };

        transport.Send(expected);
        var received = await ReceiveWithTimeout(listener);

        Assert.Equal(expected, received.Buffer);
    }

    /*
     PURPOSE:
     Ensure receive polling never blocks the game loop.

     DESIGN RULE:
     - TryReceive returns false immediately when no datagram is available
     - Snapshot polling can happen from frame code without stalling

     FAILURE MEANS:
     - Client frame updates may hang waiting for UDP data
     - Snapshot receive may block gameplay simulation
    */
    [Fact]
    public void TryReceive_ShouldReturnFalseWhenNoDatagramIsAvailable()
    {
        using var transport = new UdpClientTransport("127.0.0.1", GetUnusedUdpPort());

        Assert.False(transport.TryReceive(out var bytes));
        Assert.Empty(bytes);
    }

    /*
     PURPOSE:
     Ensure client transport can receive server snapshot datagrams.

     DESIGN RULE:
     - UDP datagrams sent to the client local endpoint are exposed as bytes
     - Snapshot deserialization remains owned by NetworkClient

     FAILURE MEANS:
     - Client may never receive authoritative server snapshots
     - Transport may hide available UDP datagrams
    */
    [Fact]
    public void TryReceive_ShouldReturnReceivedDatagramBytes()
    {
        using var transport = new UdpClientTransport("127.0.0.1", GetUnusedUdpPort());
        using var sender = new UdpClient();
        byte[] expected = { 9, 8, 7 };

        sender.Send(expected, expected.Length, new IPEndPoint(IPAddress.Loopback, transport.LocalEndPoint.Port));

        Assert.True(WaitUntilReceive(transport, out var received));
        Assert.Equal(expected, received);
    }

    /*
     PURPOSE:
     Ensure disposed UDP transports fail predictably.

     DESIGN RULE:
     - Disposing closes the UDP socket
     - Sends after disposal are rejected clearly

     FAILURE MEANS:
     - Despawned or disconnected clients may keep using stale sockets
     - Socket lifetime bugs may fail silently
    */
    [Fact]
    public void Send_ShouldRejectDisposedTransport()
    {
        var transport = new UdpClientTransport("127.0.0.1", GetUnusedUdpPort());

        transport.Dispose();

        Assert.Throws<ObjectDisposedException>(() => transport.Send(new byte[] { 1 }));
    }

    private static async Task<UdpReceiveResult> ReceiveWithTimeout(UdpClient listener)
    {
        var receiveTask = listener.ReceiveAsync();
        var completed = await Task.WhenAny(receiveTask, Task.Delay(1000));

        Assert.Same(receiveTask, completed);
        return await receiveTask;
    }

    private static bool WaitUntilReceive(
        UdpClientTransport transport,
        out byte[] bytes)
    {
        var deadline = DateTime.UtcNow.AddSeconds(1);
        do
        {
            if (transport.TryReceive(out bytes))
            {
                return true;
            }
        }
        while (DateTime.UtcNow < deadline);

        bytes = Array.Empty<byte>();
        return false;
    }

    private static int GetUnusedUdpPort()
    {
        using var udpClient = new UdpClient(0);
        return GetLocalPort(udpClient);
    }

    private static int GetLocalPort(UdpClient udpClient)
    {
        return ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
    }
}
