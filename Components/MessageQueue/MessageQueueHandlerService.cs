using Core.Model.QueueMessages;

namespace Core.Components.MessageQueue;

public interface IMessageQueueHandlerService
{
    Task HandleMessageAsync<T>(QueueMessage<T> message);
}

public class MessageQueueHandlerService : IMessageQueueHandlerService
{


    private readonly Dictionary<MessageType, Func<object?, Task>> _handlers;

    public MessageQueueHandlerService()
    {
        _handlers = new Dictionary<MessageType, Func<object?, Task>>
        {
            [MessageType.eventUpdate] = async payload =>
            {
                if (payload is EventUpdatedEvent eventEvent)
                {
                    await eventService.ApplyEventUpdateAsync(eventEvent);
                }
            },
        };
    }

    public Task HandleMessageAsync<T>(QueueMessage<T> message)
    {
        if (_handlers.TryGetValue(message.Type, out var handler))
        {
            return handler(message.Payload);
        }

        throw new InvalidOperationException($"No handler registered for {message.Type}");
    }
}
