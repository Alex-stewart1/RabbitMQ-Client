namespace RabbitMQClient.Exceptions;

public class UnknownQueueException : Exception
{
    public UnknownQueueException(string message) : base(message) { }
    public UnknownQueueException(string message, Exception innerException) : base(message, innerException) { }
}