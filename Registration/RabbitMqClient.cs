using Microsoft.Extensions.DependencyInjection;
using RabbitMQ_Client.Hosting;
using RabbitMQ_Client.Options;

namespace RabbitMQ_Client.Registration;

public static class RabbitMqClientRegistrationExtensions
{
    public static IServiceCollection AddRabbitMqClient(this IServiceCollection services, Action<RabbitMqOptions> configureOptions)
    {
        var options = new RabbitMqOptions();
        configureOptions(options);
        
        services.AddSingleton(provider => new RabbitMqClient(options, provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddHostedService<RabbitMqHostedService>();

        return services;       
    }
}