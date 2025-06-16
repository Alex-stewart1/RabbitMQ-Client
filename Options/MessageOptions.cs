namespace RabbitMQClient.Options;

public class MessageOptions
{
    public required string ExchangeName { get; init; } 
    public required string RoutingKey { get; init; }
    public bool Mandatory { get; init; } = true;
    public bool Persistent { get; init; } = true;
}