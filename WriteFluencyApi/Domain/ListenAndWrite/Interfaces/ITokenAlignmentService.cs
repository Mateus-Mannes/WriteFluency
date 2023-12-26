namespace WriteFluencyApi.ListenAndWrite.Domain;

public interface ITokenAlignmentService
{
    List<AlignedTokensDto> GetAlignedTokens(List<TextTokenDto> seq1, List<TextTokenDto> seq2, int[,] tracebackMatrix);
}
