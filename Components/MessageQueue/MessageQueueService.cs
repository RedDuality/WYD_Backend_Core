using Core.Model.Notifications;
using Core.Model.QueueMessages;
using Core.Services.Notifications;

namespace Core.Components.MessageQueue;


public interface IMessageQueueService
{
    Task SendPropagationMessageAsync<T>(QueueMessage<T> message);
    Task SendNotificationAsync(Notification notification);
}

public class MessageQueueService(IMessageQueueHandlerService handlerService, BroadcastService broadcastService) : IMessageQueueService
{

    public async Task SendPropagationMessageAsync<T>(QueueMessage<T> message)
    {
        // In a real system, this would enqueue to Kafka/RabbitMQ/etc.
        // Here we simulate async delivery.
        await Task.Delay(1);
        await handlerService.HandleMessageAsync(message);
    }

    public async Task SendNotificationAsync(Notification notification)
    {
        await Task.Delay(1);
        _ = broadcastService.BroadcastUpdate(notification);
    }
}