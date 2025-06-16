using RabbitMQClient.Exceptions;
using RabbitMQClient.Interfaces;
using RabbitMQ.Client;

namespace RabbitMQClient.Options;

public class RabbitMqOptions : IRabbitMqConfiguration
{
    internal IConnectionFactory ConnectionFactory = null!;
    internal readonly List<QueueOptions> Queues = [];
    internal readonly List<ExchangeOptions> Exchanges = [];
    internal readonly Dictionary<Type, MessageOptions> MessageTypes = [];
    public IRabbitMqConfiguration ConfigureConnection(IConnectionFactory connectionFactory)
    {
        if (ConnectionFactory != null)
        {
            throw new DuplicateRegistrationException("The connection factory has already been configured.");       
        }
        
        ConnectionFactory = connectionFactory;

        return this;
    }

    public IRabbitMqConfiguration RegisterExchange(ExchangeOptions options)
    {
        if (Exchanges.Any(x => x.ExchangeName == options.ExchangeName))
        {
            throw new DuplicateRegistrationException($"The exchange '{options.ExchangeName}' is already registered.");
        }
        
        Exchanges.Add(options);
        
        return this;       
    }

    
    public IRabbitMqConfiguration RegisterQueue(QueueOptions options)
    {
        if (Queues.Any(x => x.QueueName == options.QueueName && x.ExchangeName == options.ExchangeName))
        {
            throw new DuplicateRegistrationException($"The queue '{options.QueueName}' is already registered with the exchange '{options.ExchangeName}'.");
        }

        Queues.Add(options);
        
        return this;
    }
    
    public IRabbitMqConfiguration RegisterMessage<TMessage>(MessageOptions options) where TMessage : IMessage
    {
        var type = typeof(TMessage);
        
        if (type.FullName == null)
        {
            throw new ArgumentException(
                $"Cannot register message type '{type}' because its FullName is null. " +
                "This typically occurs with generic type parameters, array types, pointer types, or byref types.");
        }

        if (!MessageTypes.TryAdd(type, options))
        {
            throw new DuplicateRegistrationException($"The message type '{type.Name}' is already registered.");
        }
        
        return this;
    }
}

public interface IRabbitMqConfiguration
{
    IRabbitMqConfiguration ConfigureConnection(IConnectionFactory connectionFactory);
    IRabbitMqConfiguration RegisterExchange(ExchangeOptions options);
    IRabbitMqConfiguration RegisterQueue(QueueOptions options);
    IRabbitMqConfiguration RegisterMessage<TMessage>(MessageOptions options) where TMessage : IMessage;
}