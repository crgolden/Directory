namespace Directory.Moderation;

using Directory.Enums;

public readonly record struct CorrectionsRequest(
    CorrectionStatus? Status,
    int Page,
    int PageSize);
