namespace WriteFluency.TextComparisons;

public sealed record CorrectionOrchestrationRequest(
    string OriginalText,
    string UserText,
    bool IsAuthenticated,
    bool IsPro,
    string? UserId,
    string? AnonymousFingerprintHash,
    string? AnonymousClientIpAddress,
    bool EnableFreeReviewTeaser);
