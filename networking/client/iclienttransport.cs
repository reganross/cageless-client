using System;

public interface IClientTransport : IDisposable
{
    void Send(byte[] bytes);
    bool TryReceive(out byte[] bytes);
}
