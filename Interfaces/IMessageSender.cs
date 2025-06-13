namespace RabbitMQ_Client.Interfaces;

public interface IMessageSender
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken token = default) where TMessage : IMessage;
}