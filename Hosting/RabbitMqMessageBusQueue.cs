using System.Threading.Channels;
using RabbitMQ_Client.Interfaces;

namespace RabbitMQ_Client.Hosting;

internal sealed class RabbitMqMessageBusQueue
{
    private readonly Channel<IMessage> _channelQueue = Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    public ChannelReader<IMessage> ChannelReader => _channelQueue.Reader;
    public ChannelWriter<IMessage> ChannelWriter => _channelQueue.Writer;
}