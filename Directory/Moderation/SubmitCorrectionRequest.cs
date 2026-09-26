namespace Directory.Moderation;

public record SubmitCorrectionRequest(Guid ChurchId, string Field, string? OldValue, string NewValue);
