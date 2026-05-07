using System;
using System.Collections.Generic;

/// <summary>
/// In-memory client transport paired with <see cref="LoopbackServerTransport"/>.
/// </summary>
public sealed class LoopbackClientTransport : IClientTransport
{
    private readonly Queue<byte[]> serverIngress;
    private readonly Queue<byte[]> clientIngress;
    private bool disposed;

    public LoopbackClientTransport(Queue<byte[]> serverIngress, Queue<byte[]> clientIngress)
    {
        this.serverIngress = serverIngress;
        this.clientIngress = clientIngress;
    }

    public void Send(byte[] bytes)
    {
        ThrowIfDisposed();
        serverIngress.Enqueue(bytes);
    }

    public bool TryReceive(out byte[] bytes)
    {
        ThrowIfDisposed();

        if (clientIngress.Count == 0)
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        bytes = clientIngress.Dequeue();
        return true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        serverIngress.Clear();
        clientIngress.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(LoopbackClientTransport));
        }
    }
}
