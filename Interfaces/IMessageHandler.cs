namespace RabbitMQClient.Interfaces;

public interface IMessageHandler
{
    Task HandleAsync(IMessage message, CancellationToken cancellationToken = default);
}
public interface IMessageHandler<in T> where T : IMessage
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
public abstract class MessageHandler<T> : IMessageHandler, IMessageHandler<T> where T : IMessage
{
    public Task HandleAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        return HandleAsync((T)message, cancellationToken);
    }
    public abstract Task HandleAsync(T message, CancellationToken cancellationToken = default);
}