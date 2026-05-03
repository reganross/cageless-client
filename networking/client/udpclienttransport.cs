using System;
using System.Net;
using System.Net.Sockets;

public class UdpClientTransport : IClientTransport
{
    private readonly IPEndPoint serverEndPoint;
    private UdpClient udpClient;
    private bool disposed;

    public UdpClientTransport(string host, int serverPort, int localPort = 0)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Server host is required.", nameof(host));
        }

        if (serverPort <= 0 || serverPort > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(serverPort));
        }

        if (localPort < 0 || localPort > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(localPort));
        }

        serverEndPoint = new IPEndPoint(Dns.GetHostAddresses(host)[0], serverPort);
        udpClient = new UdpClient(localPort);
    }

    public IPEndPoint LocalEndPoint => (IPEndPoint)udpClient.Client.LocalEndPoint;

    public void Send(byte[] bytes)
    {
        ThrowIfDisposed();
        udpClient.Send(bytes, bytes.Length, serverEndPoint);
    }

    public bool TryReceive(out byte[] bytes)
    {
        ThrowIfDisposed();

        if (udpClient.Available == 0)
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        IPEndPoint remoteEndPoint = null;
        bytes = udpClient.Receive(ref remoteEndPoint);
        return true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        udpClient?.Dispose();
        udpClient = null;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(UdpClientTransport));
        }
    }
}
