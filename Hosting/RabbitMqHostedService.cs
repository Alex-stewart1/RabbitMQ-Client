using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQClient.Interfaces;

namespace RabbitMQClient.Hosting;

internal sealed class RabbitMqHostedService(RabbitMqClient rabbitMqClient) : IHostedService, IDisposable, IAsyncDisposable
{
    private readonly RabbitMqClient _rabbitMqClient = rabbitMqClient;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _rabbitMqClient.InitializeAsync();
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _rabbitMqClient.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _rabbitMqClient.DisposeAsync();
    }
}