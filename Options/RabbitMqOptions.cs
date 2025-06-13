using RabbitMQClient.Exceptions;
using RabbitMQClient.Interfaces;
using RabbitMQ.Client;

namespace RabbitMQClient.Options;

public class RabbitMqOptions : IRabbitMqConfiguration, IRabbitMqQueueConfiguration 
{
    internal IConnectionFactory ConnectionFactory = null!;
    internal readonly List<string> Queues = [];
    internal readonly Dictionary<Type, string> MessageTypeQueueNames = [];
    
    private string _queueName = null!;

    public IRabbitMqConfiguration ConfigureConnection(IConnectionFactory connectionFactory)
    {
        if (ConnectionFactory != null)
        {
            throw new DuplicateRegistrationException("The connection factory has already been configured.");       
        }
        
        ConnectionFactory = connectionFactory;

        return this;
    }

    public IRabbitMqQueueConfiguration RegisterQueue(string queueName)
    {
        if (Queues.Contains(queueName))
        {
            throw new DuplicateRegistrationException($"The queue '{queueName}' is already registered.");
        }

        Queues.Add(queueName);
        
        _queueName = queueName;
        
        return this;
    }
    
    public IRabbitMqQueueConfiguration RegisterMessage<TMessage>() where TMessage : IMessage
    {
        var type = typeof(TMessage);
        
        if (type.FullName == null)
        {
            throw new ArgumentException(
                $"Cannot register message type '{type}' because its FullName is null. " +
                "This typically occurs with generic type parameters, array types, pointer types, or byref types.");
        }

        if (_queueName is null)
        {
            throw new InvalidOperationException($"Cannot register message type '{type}' because no queue has been registered.");
        }
        
        if (!MessageTypeQueueNames.TryAdd(type, _queueName))
        {
            throw new DuplicateRegistrationException($"The message type '{type.Name}' is already registered.");
        }
        
        return this;
    }
}

public interface IRabbitMqConfiguration
{
    IRabbitMqConfiguration ConfigureConnection(IConnectionFactory connectionFactory);
    IRabbitMqQueueConfiguration RegisterQueue(string queueName);
}

public interface IRabbitMqQueueConfiguration
{
    IRabbitMqQueueConfiguration RegisterMessage<TMessage>() where TMessage : IMessage;
}