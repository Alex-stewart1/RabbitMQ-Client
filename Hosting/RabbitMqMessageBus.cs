using RabbitMQ_Client.Interfaces;

namespace RabbitMQ_Client.Hosting;

internal sealed class RabbitMqMessageBus(RabbitMqMessageBusQueue queue) : IMessageBus
{
    private readonly RabbitMqMessageBusQueue _eventBusQueue = queue;

    public ValueTask EnqueueAsync<TMessage>(TMessage @event, CancellationToken ct = default) where TMessage : IMessage
    {
        return _eventBusQueue.ChannelWriter.WriteAsync(@event, ct);
    }
}