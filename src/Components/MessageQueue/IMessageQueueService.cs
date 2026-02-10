using Core.Model.Notifications;
using Core.Model.QueueMessages;

namespace Core.Components.MessageQueue;

public interface IMessageQueueService
{
    public Task SendPropagationMessageAsync<T>(QueueMessage<T> message);

    public Task SendNotificationAsync(Notification notification);
}