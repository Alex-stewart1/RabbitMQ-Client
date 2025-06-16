namespace RabbitMQClient.Options;

public class QueueOptions
{
    public required string QueueName { get; init; } 
    public required string ExchangeName { get; init; } 
    public required bool Durable { get; init; } 
    public required bool AutoDelete { get; init; } 
    public bool Exclusive { get; init; } = false;
}