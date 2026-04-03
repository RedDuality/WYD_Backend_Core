using Core.Model.Events.Recurrence;
using MongoDB.Bson;

namespace Core.Model.Util.EventsQuery;

public record RecurrentEventWithProfile(RecurrentEvent Event, ObjectId ProfileId);
