namespace RabbitMQClient.Options;


public class ExchangeOptions
{
    public required string ExchangeName { get; init; }
    public required string ExchangeType { get; init; }
    public required bool Durable { get; init; }
    public required bool AutoDelete { get; init; }
}