using RabbitMQ_Client.Exceptions;
using RabbitMQ_Client.Interfaces;
using RabbitMQ.Client;

namespace RabbitMQ_Client.Options;

public class RabbitMqOptions : IRabbitMqConfiguration
{
    internal IConnectionFactory ConnectionFactory = null!;
    internal readonly List<string> Queues = [];
    internal readonly Dictionary<Type, string> MessageTypeQueueNames = [];

    public IRabbitMqConfiguration ConfigureConnection(IConnectionFactory connectionFactory)
    {
        ConnectionFactory = connectionFactory;

        return this;
    }

    public IRabbitMqConfiguration RegisterQueue(string queueName)
    {
        if (Queues.Contains(queueName))
        {
            throw new DuplicateRegistrationException($"The queue '{queueName}' is already registered.");
        }

        Queues.Add(queueName);
        
        return this;
    }
    
    public IRabbitMqConfiguration RegisterMessage<TMessage>(string queueName) where TMessage : IMessage
    {
        var type = typeof(TMessage);
        
        if (type.FullName == null)
        {
            throw new ArgumentException(
                $"Cannot register message type '{type}' because its FullName is null. " +
                "This typically occurs with generic type parameters, array types, pointer types, or byref types.");
        }
        
        if (!MessageTypeQueueNames.TryAdd(type, queueName))
        {
            throw new DuplicateRegistrationException($"The message type '{type.Name}' is already registered.");
        }
        
        return this;
    }
}

public interface IRabbitMqConfiguration
{
    IRabbitMqConfiguration ConfigureConnection(IConnectionFactory connectionFactory);
    IRabbitMqConfiguration RegisterQueue(string queueName);
    IRabbitMqConfiguration RegisterMessage<TMessage>(string queueName) where TMessage : IMessage;
}