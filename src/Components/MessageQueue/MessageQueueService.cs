using Core.Model.Notifications;
using Core.Model.QueueMessages;
using Core.Services.Notifications;

namespace Core.Components.MessageQueue;

public class MessageQueueService(IMessageQueueHandlerService handlerService, BroadcastService broadcastService)
{

    public async Task SendPropagationMessageAsync<T>(QueueMessage<T> message)
    {
        // In a real system, this would enqueue to Kafka/RabbitMQ/etc.
        // Here we simulate the time to add the message to the queue delivery.
        await Task.Delay(1);
        _ = handlerService.HandleMessageAsync(message);
    }

    public async Task SendNotificationAsync(Notification notification)
    {
        await Task.Delay(1);
        _ = broadcastService.BroadcastUpdate(notification);
    }
}