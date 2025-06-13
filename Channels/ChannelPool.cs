using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace RabbitMQ_Client.Pooling;

internal sealed class ChannelPool(IConnection connection) : IDisposable, IAsyncDisposable
{
    private readonly IConnection _connection = connection;
    
    private readonly ConcurrentQueue<IChannel> _channels = new();

    private const int PoolSize = 8;
    
    private volatile bool _disposed = false;
    private readonly Lock _disposeLock = new();

    public async ValueTask<IChannel> RentAsync()
    {
        // Fast path - no locking needed for the common case
        ObjectDisposedException.ThrowIf(_disposed, nameof(ChannelPool));
 
        if (_channels.TryDequeue(out var channel))
        {
            if (channel.IsOpen)
            {
                return channel;
            }

            channel.Dispose();
        }

        return await _connection.CreateChannelAsync();
    }

    public void Return(IChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        
        // Fast path - no locking needed
        if (_disposed || !channel.IsOpen || _channels.Count >= PoolSize) 
        {
            channel.Dispose();
        }
        else
        {
            _channels.Enqueue(channel);
        }
    }

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }
            
            _disposed = true;
        }
        
        while (_channels.TryDequeue(out var channel))
        {
            channel.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }
            
            _disposed = true;
        }
       
        while (_channels.TryDequeue(out var channel))
        {
           await channel.DisposeAsync();
        }       
    }
}