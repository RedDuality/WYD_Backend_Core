namespace Core.Model.QueueMessages;

public class QueueMessage<T>(MessageType type, T? payload)
{
    public MessageType Type { get; set; } = type;

    public T? Payload { get; set; } = payload;
}
