namespace RabbitMQ_Client.Interfaces;

public interface IMessageBus
{
    ValueTask EnqueueAsync<TMessage>(TMessage @event, CancellationToken ct = default) where TMessage : IMessage;
}