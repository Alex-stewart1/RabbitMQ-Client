using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ_Client.Interfaces;

namespace RabbitMQ_Client.Hosting;

internal sealed class RabbitMqHostedService(RabbitMqMessageBusQueue messageBusQueue, RabbitMqClient rabbitMqClient, ILogger<RabbitMqHostedService> logger) : BackgroundService
{
    private readonly RabbitMqMessageBusQueue _messageBusQueue = messageBusQueue;
    private readonly RabbitMqClient _rabbitMqClient = rabbitMqClient;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // -- Initialize --
        await _rabbitMqClient.InitializeAsync();
        
        // -- Handle message publishing --
        while (await _messageBusQueue.ChannelReader.WaitToReadAsync(stoppingToken))
        {
            try
            {
                var message = await _messageBusQueue.ChannelReader.ReadAsync(stoppingToken);

                await _rabbitMqClient.PublishDirectlyAsync(message);
            }
            catch (Exception ex)
            {
                //TODO: logg
            }
        }
    }
}