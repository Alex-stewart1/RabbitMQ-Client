using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQClient.Dispatcher;
using RabbitMQClient.Exceptions;
using RabbitMQClient.Interfaces;
using RabbitMQClient.Options;
using RabbitMQClient.Pooling;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQClient;

public sealed class RabbitMqClient(RabbitMqOptions options, IServiceScopeFactory scopeFactory) : IMessageSender, IDisposable, IAsyncDisposable
{
    private readonly IConnectionFactory _connectionFactory = options.ConnectionFactory;
    private readonly MessageDispatcher _messageDispatcher = new(scopeFactory);
    private readonly IReadOnlyList<string> _queues = options.Queues.AsReadOnly();
    private readonly Dictionary<string, Type> _messageTypes = options.MessageTypeQueueNames.Keys.ToDictionary(x => x.FullName!, x => x);
    private readonly Dictionary<Type, string> _messageQueues = options.MessageTypeQueueNames.ToDictionary(x => x.Key, x => x.Value);

    private IConnection _connection = null!;
    private ChannelPool _channelPool = null!;
    private IChannel _consumerChannel = null!;

    internal async ValueTask InitializeAsync()
    {
        _connection = await _connectionFactory.CreateConnectionAsync();
        _channelPool = new ChannelPool(_connection);
        
        await InitializeQueuesAsync();
    }
    
    private async ValueTask InitializeQueuesAsync()
    {
        _consumerChannel = await _channelPool.RentAsync();
        
        try
        {
            foreach (var queue in _queues)
            {
                await _consumerChannel.QueueDeclareAsync(queue: queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
                await _consumerChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);
                
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
                consumer.ReceivedAsync += ConsumerOnReceivedAsync;
                await _consumerChannel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer);
            }
        }
        catch (Exception ex)
        {
            //TODO: Log
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
                await _messageDispatcher.DispatchAsync(message);
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

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken token = default) where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_connection is null || _channelPool is null)
        {
            throw new InitializationException(
                $"{nameof(RabbitMqClient)} has not been initialized. Call {nameof(InitializeAsync)} before calling {nameof(PublishAsync)}");
        }
        
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
                body: body, 
                cancellationToken: token);
        }
        finally
        {
            _channelPool.Return(channel);
        }
    }

    public void Dispose()
    {
        _channelPool.Return(_consumerChannel);
        _channelPool.Dispose();
        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _channelPool.Return(_consumerChannel);
        await _channelPool.DisposeAsync();
        await _connection.DisposeAsync();
    }
}