
namespace Core.DTO.EventAPI;

public record RetrieveMultipleEventsResponseDto(
    List<RetrieveEventResponseDto> Events,
    List<RetrieveRecurrentEventResponseDto> Masters
);
