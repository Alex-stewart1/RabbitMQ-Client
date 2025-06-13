using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQClient.Interfaces;

namespace RabbitMQClient.Dispatcher;

internal sealed class MessageDispatcher(IServiceScopeFactory serviceScopeFactory)
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    
    private static readonly ConcurrentDictionary<Type, Type> s_handlerTypes = new();

    public Task DispatchAsync(IMessage message)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        if (!s_handlerTypes.TryGetValue(message.GetType(), out var handlerType))
        {
            handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType());
            s_handlerTypes.TryAdd(message.GetType(), handlerType);
        }
        
        var handler = (IMessageHandler)scope.ServiceProvider.GetRequiredService(handlerType);
        
        return handler.HandleAsync(message);
    } 
}