namespace Core.Model.QueueMessages;

public class QueueMessage<T>(MessageType type, T payload, int retry = 0)
{
    public MessageType Type { get; set; } = type;
    public T Payload { get; set; } = payload;
    public int Retry { get; set; } = retry;
}
