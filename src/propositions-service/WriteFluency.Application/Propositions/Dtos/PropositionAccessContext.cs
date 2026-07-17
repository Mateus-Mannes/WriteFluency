namespace WriteFluency.Propositions;

public sealed record PropositionAccessContext(
    bool IsAuthenticated,
    bool IsPro,
    string? UserId,
    string? AnonymousFingerprintHash,
    string? AnonymousClientIpAddress);
