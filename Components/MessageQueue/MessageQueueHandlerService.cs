using Core.Model.QueueMessages;
using Core.Services.Events;
using Core.Services.Profiles;

namespace Core.Components.MessageQueue;

public interface IMessageQueueHandlerService
{
    Task HandleMessageAsync<T>(QueueMessage<T> message);
}

public class MessageQueueHandlerService : IMessageQueueHandlerService
{
    private readonly Dictionary<MessageType, Func<object?, Task>> _handlers;
    private readonly EventUpdatePropagationService _eventService;
    private readonly ProfileUpdatePropagationService _profileService;

    public MessageQueueHandlerService(EventUpdatePropagationService eventService, ProfileUpdatePropagationService profileService)
    {
        _eventService = eventService;
        _profileService = profileService;

        _handlers = new Dictionary<MessageType, Func<object?, Task>>
        {
            [MessageType.eventUpdate] = WrapHandler<UpdateEventPayload>(async p =>
            {
                await _eventService.PropagateUpdateEffects(p.Event, p.Type, p.ActorId);
            }),
/*
            [MessageType.profileUpdate] = WrapHandler<UpdateProfilePayload>(async p =>
            {
                _profileService.PropagateUpdateEffects(p.ProfileId, p.Type, p.ActorId);
            }),
*/        };
    }

    private Func<object?, Task> WrapHandler<T>(Func<T, Task> handlerLogic) where T : class
    {
        return async message =>
        {
            if (message is QueueMessage<T> m)
            {
                try
                {
                    await handlerLogic(m.Payload);
                }
                catch (Exception ex)
                {


                    // Increase retry count
                    m.Retry += 1;

                    // Exponential backoff (min 5s, max 2min)
                    //var delay = Math.Min(120_000, (int)Math.Pow(2, m.Retry) * 1000);
                    //await Task.Delay(delay);
                    Console.WriteLine("There was an error while propagating effects, retrying in N seconds, Exception: \n " + ex.Message);

                    // Re‑enqueue
                    //await messageService.SendPropagationMessageAsync(message);

                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Handler expected payload type '{typeof(T).Name}', but received message of unexpected type '{message?.GetType().Name ?? "null"}'.");
            }
        };
    }

    public Task HandleMessageAsync<T>(QueueMessage<T> message)
    {
        if (_handlers.TryGetValue(message.Type, out var handler))
        {
            return handler(message);
        }

        throw new InvalidOperationException($"No handler registered for {message.Type}");
    }
}