namespace Core.Model.Util.Extensions;

public static class DateTimeOffsetExtension {
  extension(DateTimeOffset value) {
    public DateTimeOffset TruncateToSeconds() {
      return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
    }
  }
}
