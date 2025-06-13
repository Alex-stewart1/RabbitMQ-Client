using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQClient.Hosting;
using RabbitMQClient.Interfaces;
using RabbitMQClient.Options;

namespace RabbitMQClient.Registration;

public static class RabbitMqClientRegistrationExtensions
{
    public static IServiceCollection AddRabbitMqClient(this IServiceCollection services, Action<IRabbitMqConfiguration> configureOptions)
    {
        var options = new RabbitMqOptions();
        configureOptions(options);
        
        services.AddSingleton(provider => new RabbitMqClient(options, provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddHostedService<RabbitMqHostedService>();
        
        services.AddSingleton<IMessageSender>(provider => provider.GetRequiredService<RabbitMqClient>());

        return services;       
    }
    public static IServiceCollection AddMessageHandlersFromAssemblyContaining<T>(this IServiceCollection services)
    {
        return services.AddMessageHandlers(typeof(T).Assembly);
    }
    
    private static IServiceCollection AddMessageHandlers(this IServiceCollection services, Assembly assembly)
    {
        // Get all types from the assembly
        var types = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false });

        foreach (var type in types)
        {
            // Find all interfaces that implement IMessageHandler<T>
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && 
                            i.GetGenericTypeDefinition() == typeof(IMessageHandler<>));

            foreach (var handlerInterface in handlerInterfaces)
            {
                // Register the handler with its interface
                services.AddScoped(handlerInterface, type);
            }
        }

        return services;
    }
}