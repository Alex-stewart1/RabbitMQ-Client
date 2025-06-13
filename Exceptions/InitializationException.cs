namespace RabbitMQ_Client.Exceptions;

public class InitializationException : InvalidOperationException
{
    public InitializationException(string message) : base(message) { }
    public InitializationException(string message, Exception innerException) : base(message, innerException) { }   
}