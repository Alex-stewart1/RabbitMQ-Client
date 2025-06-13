using System.Diagnostics;
using System.Text.Json;
using RabbitMQ_Client.Exceptions;
using RabbitMQ_Client.Interfaces;
using RabbitMQ_Client.Options;
using RabbitMQ_Client.Pooling;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ_Client;

public class RabbitMqClient(RabbitMqOptions options)
{
    private readonly IConnectionFactory _connectionFactory = options.ConnectionFactory;
    private readonly IReadOnlyList<string> _queues = options.Queues.AsReadOnly();
    private readonly Dictionary<string, Type> _messageTypes = options.MessageTypeQueueNames.Keys.ToDictionary(x => x.FullName!, x => x);
    private readonly Dictionary<Type, string> _messageQueues = options.MessageTypeQueueNames.ToDictionary(x => x.Key, x => x.Value);

    private ChannelPool _channelPool = null!;

    public async ValueTask InitializeAsync()
    {
        var connection = await _connectionFactory.CreateConnectionAsync();
        _channelPool = new ChannelPool(connection);
        
        await InitializeQueuesAsync();
    }
    
    private async ValueTask InitializeQueuesAsync()
    {
        var channel = await _channelPool.RentAsync();
        
        try
        {
            foreach (var queue in _queues)
            {
                await channel.QueueDeclareAsync(queue: queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);
                
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += ConsumerOnReceivedAsync;
                await channel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer);
            }
        }
        catch (Exception ex)
        {
            //TODO: Log
        }
        finally
        {
            _channelPool.Return(channel);
        }
    }

    private async Task ConsumerOnReceivedAsync(object sender, BasicDeliverEventArgs @event)
    {
        var channel = ((AsyncEventingBasicConsumer)sender).Channel;
        
        try
        {
            var type = @event.BasicProperties.Type;
            if (type is null)
            {
                //TODO: log
                return;
            }
            
            if (_messageTypes.TryGetValue(type, out var messageType))
            {
                var message = (IMessage)JsonSerializer.Deserialize(@event.Body.Span, messageType)!;
                
                // -- Dispatch message --
                
                
                await channel.BasicAckAsync(@event.DeliveryTag, false);

                return;
            }
            else
            {
                //TODO: Log missing type and add dead letter
            }
        }
        catch (Exception ex)
        {
            //TODO: Log missing type and add dead letter
            Debugger.Break();
        }
        
        // -- Reject failed message --
        await channel.BasicRejectAsync(@event.DeliveryTag, requeue: false);
    }

    public async ValueTask PublishDirectlyAsync<TMessage>(TMessage message) where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);
        
        var type = typeof(TMessage);

        if (!_messageQueues.TryGetValue(type, out var queue))
        {
            throw new UnknownQueueException($"{type.Name} has not been registered.");
        }
        
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        
        var channel = await _channelPool.RentAsync();

        try
        {
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queue,
                mandatory: true,
                basicProperties: new BasicProperties { Type = type.FullName!, Persistent = true },
                body: body);
        }
        finally
        {
            _channelPool.Return(channel);
        }
    }
}