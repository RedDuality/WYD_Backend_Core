using Core.Model.Util.Extensions;
using System.Globalization;
using Core.Model.Events;
using Core.Model.Events.Recurrence;

namespace Core.DTO.EventAPI;

public enum RecurrentUpdateType {
  ThisInstance,
  AllTheSequence,
  ThisAndAllFollowing
}

public class UpdateRecurrentEventRequestDto {
  public required RecurrentUpdateType UpdateType { get; set; }
  public required string InstanceId { get; set; } // recurrencyId if generated, eventId if detached
  public required string MasterEventId { get; set; }
  public string? Title { get; set; }
  public string? Description { get; set; }
  public string? RecurrenceRule { get; set; }
  public DateTimeOffset? StartTime { get; set => field = value?.TruncateToSeconds();}
  public DateTimeOffset? EndTime { get; set => field = value?.TruncateToSeconds(); }

  // Parameterless constructor for deserialization
  public UpdateRecurrentEventRequestDto() { }

  public bool IsGeneratedInstance() {
    return InstanceId.Contains(MasterEventId + "_");
  }

  public bool IsDetachedInstance () {
    return !IsGeneratedInstance();
  }

  #region validate
  
  public void Validate() {
    if(Title == null && Description == null && RecurrenceRule == null && !StartTime.HasValue && !EndTime.HasValue)
      throw new ArgumentException("No updates were made");

    if(Title == "")
      throw new ArgumentException("Title cannot be empty");

    ValidateDates();
  }

  private void ValidateDates() {
    if (!StartTime.HasValue && !EndTime.HasValue)
      return;

    if ((StartTime.HasValue && !EndTime.HasValue) || (!StartTime.HasValue && EndTime.HasValue))
      throw new ArgumentException("Both dates must be set");

    if (EndTime!.Value <= StartTime!.Value)
      throw new ArgumentException("End time must be after start time");

    if (EndTime!.Value.Subtract(StartTime!.Value) < TimeSpan.FromMinutes(5))
      throw new ArgumentException("Event duration must be at least 5 minutes");

    if (StartTime!.Value.Offset != TimeSpan.Zero || EndTime!.Value.Offset != TimeSpan.Zero)
      throw new ArgumentException("Dates must be in UTC");
  }
  
  public void ValidateForSingleInstance(RecurrentEvent master, EventDetails masterDetails) {
    if (UpdateType == RecurrentUpdateType.ThisInstance && RecurrenceRule != null && RecurrenceRule.Length > 0)
      throw new ArgumentException("Cannot edit the recurrency rule for a single instance");

    if( 
      (Title == null || Title == master.Title) && 
      (Description == null || Description == masterDetails.Description) && 
      (RecurrenceRule == null || RecurrenceRule == master.RecurrenceRule) &&
      !HasDatesChanged(master)
    )
      throw new ArgumentException("No updates were made");
  }

  private bool HasDatesChanged(RecurrentEvent master) {
    if(IsDetachedInstance())
      return StartTime.HasValue && EndTime.HasValue;
    
    int lastUnderscoreIndex = InstanceId.LastIndexOf('_');
    if(lastUnderscoreIndex < 0)
      throw new ArgumentException("InstanceId is malformed");
    DateTimeOffset generatedStartTime = DateTimeOffset.ParseExact(InstanceId[(lastUnderscoreIndex + 1)..], "yyyyMMdd'T'HHmmssZ", CultureInfo.InvariantCulture).TruncateToSeconds();
    DateTimeOffset generatedEndTime = generatedStartTime.Add(master.EndTime.Subtract(master.StartTime));

    bool startTimeChanged = StartTime.HasValue && StartTime.Value.ToUnixTimeSeconds() != generatedStartTime.ToUnixTimeSeconds();
    bool endTimeChanged = EndTime.HasValue && EndTime.Value.ToUnixTimeSeconds() != generatedEndTime.ToUnixTimeSeconds();

    return startTimeChanged || endTimeChanged;
  }
  
  #endregion
}
