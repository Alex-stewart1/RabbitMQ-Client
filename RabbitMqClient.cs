using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQClient.Dispatcher;
using RabbitMQClient.Exceptions;
using RabbitMQClient.Interfaces;
using RabbitMQClient.Options;
using RabbitMQClient.Pooling;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQClient;

public sealed class RabbitMqClient(RabbitMqOptions options, IServiceScopeFactory scopeFactory, ILogger<RabbitMqClient>? logger = null) : IMessageSender, IDisposable, IAsyncDisposable
{
    private readonly IConnectionFactory _connectionFactory = options.ConnectionFactory;
    private readonly MessageDispatcher _messageDispatcher = new(scopeFactory);
    private readonly ILogger<RabbitMqClient>? _logger = logger;
    private readonly IReadOnlyList<QueueOptions> _queues = options.Queues.AsReadOnly();
    private readonly IReadOnlyList<ExchangeOptions> _exchanges = options.Exchanges.AsReadOnly();
    private readonly Dictionary<string, Type> _messageTypes = options.MessageTypes.Keys.ToDictionary(x => x.FullName!, x => x);
    private readonly Dictionary<Type, MessageOptions> _messageQueues = options.MessageTypes.ToDictionary(x => x.Key, x => x.Value);

    private IConnection _connection = null!;
    private ChannelPool _channelPool = null!;
    private IChannel _consumerChannel = null!;

    internal async ValueTask InitializeAsync()
    {
        _connection = await _connectionFactory.CreateConnectionAsync();
        _channelPool = new ChannelPool(_connection);
        
        _consumerChannel = await _channelPool.RentAsync();
        _consumerChannel.ChannelShutdownAsync += OnConsumerChannelShutdown;
        
        await InitializeExchangesAsync(_consumerChannel);
        await InitializeQueuesAsync(_consumerChannel);
    }

    private async Task InitializeExchangesAsync(IChannel channel)
    {
        try
        {
            foreach (var exchange in _exchanges)
            {
                _logger?.LogDebug("Declaring exchange {ExchangeName} of type {ExchangeType}", 
                    exchange.ExchangeName, exchange.ExchangeType);
                
                await channel.ExchangeDeclareAsync(
                      exchange: exchange.ExchangeName,
                      type: exchange.ExchangeType,
                      durable: exchange.Durable,
                      autoDelete: exchange.AutoDelete);
                
                _logger?.LogInformation("Successfully declared exchange {ExchangeName}", exchange.ExchangeName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize exchanges");
            throw;
        }
    }

    private async Task InitializeQueuesAsync(IChannel channel)
    {
        try
        {
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);
            
            foreach (var queue in _queues)
            {
                _logger?.LogDebug("Declaring queue {QueueName} and binding to exchange {ExchangeName}", 
                    queue.QueueName, queue.ExchangeName);
                
                await channel.QueueDeclareAsync(
                    queue: queue.QueueName,
                    durable: queue.Durable,
                    exclusive: queue.Exclusive,
                    autoDelete: queue.AutoDelete
                );
                
                await channel.QueueBindAsync(queue.QueueName, queue.ExchangeName, "");
                
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
                consumer.ReceivedAsync += ConsumerOnReceivedAsync;
                await channel.BasicConsumeAsync(queue: queue.QueueName, autoAck: false, consumer: consumer);
                
                _logger?.LogInformation("Successfully configured queue {QueueName} with consumer", queue.QueueName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize queues");
            throw;
        } 
    }
    
    private async Task OnConsumerChannelShutdown(object sender, ShutdownEventArgs @event)
    {
        if (@event.Initiator == ShutdownInitiator.Application)
        {
            _logger?.LogDebug("Consumer channel shutdown initiated by application");
            return;
        }
    
        _logger?.LogWarning("Consumer channel shutdown detected. Reason: {ReplyText}, Code: {ReplyCode}", 
            @event.ReplyText, @event.ReplyCode);
    
        try
        {
            _consumerChannel.ChannelShutdownAsync -= OnConsumerChannelShutdown;
            _channelPool.Return(_consumerChannel);
            
            _consumerChannel = await _channelPool.RentAsync();
            _consumerChannel.ChannelShutdownAsync += OnConsumerChannelShutdown;
            
            await InitializeExchangesAsync(_consumerChannel);
            await InitializeQueuesAsync(_consumerChannel);
            
            _logger?.LogInformation("Successfully recovered consumer channel after shutdown");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to recover consumer channel after shutdown");
            throw;
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
                _logger?.LogWarning("Received message without type property. DeliveryTag: {DeliveryTag}, Exchange: {Exchange}, RoutingKey: {RoutingKey}", 
                    @event.DeliveryTag, @event.Exchange, @event.RoutingKey);
                return;
            }
            
            if (_messageTypes.TryGetValue(type, out var messageType))
            {
                _logger?.LogDebug("Processing message of type {MessageType}, DeliveryTag: {DeliveryTag}", 
                    type, @event.DeliveryTag);
                
                var message = (IMessage)JsonSerializer.Deserialize(@event.Body.Span, messageType)!;
                await _messageDispatcher.DispatchAsync(message);
                await channel.BasicAckAsync(@event.DeliveryTag, false);

                _logger?.LogDebug("Successfully processed message of type {MessageType}, DeliveryTag: {DeliveryTag}", 
                    type, @event.DeliveryTag);
                return;
            }
            else
            {
                _logger?.LogError("Unknown message type {MessageType}. DeliveryTag: {DeliveryTag}, Exchange: {Exchange}, RoutingKey: {RoutingKey}. Message will be rejected and moved to dead letter queue if configured.", 
                    type, @event.DeliveryTag, @event.Exchange, @event.RoutingKey);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to process message of type {MessageType}, DeliveryTag: {DeliveryTag}. Message will be rejected and moved to dead letter queue if configured.", 
                @event.BasicProperties.Type, @event.DeliveryTag);
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

        if (!_messageQueues.TryGetValue(type, out var messageOptions))
        {
            throw new UnknownQueueException($"{type.Name} has not been registered.");
        }
        
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        
        var channel = await _channelPool.RentAsync();

        try
        {
            _logger?.LogDebug("Publishing message of type {MessageType} to exchange {ExchangeName} with routing key {RoutingKey}", 
                type.Name, messageOptions.ExchangeName, messageOptions.RoutingKey);
            
            var basicProperties = new BasicProperties { Type = type.FullName!, Persistent = messageOptions.Persistent };
            
            await channel.BasicPublishAsync(
                exchange: messageOptions.ExchangeName,
                routingKey: messageOptions.RoutingKey,
                mandatory: messageOptions.Mandatory,
                basicProperties: basicProperties,
                body: body, 
                cancellationToken: token);
            
            _logger?.LogDebug("Successfully published message of type {MessageType}", type.Name);
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