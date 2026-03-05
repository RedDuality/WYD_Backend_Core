using Core.Model.Events.Recurrence;
using MongoDB.Bson;

namespace Core.Model.Util;

public record EventWithProfile(RecurrentEvent Event, ObjectId ProfileId);
