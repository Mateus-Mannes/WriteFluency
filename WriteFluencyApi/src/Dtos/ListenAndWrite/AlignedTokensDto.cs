namespace WriteFluencyApi.ListenAndWrite;

public record AlignedTokensDto(
    TextTokenDto? OriginalToken,
    TextTokenDto? UserToken
);