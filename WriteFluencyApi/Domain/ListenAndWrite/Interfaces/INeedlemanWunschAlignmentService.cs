namespace WriteFluencyApi.ListenAndWrite.Domain;

public interface INeedlemanWunschAlignmentService
{
    (int[,], int[,]) NeedlemanWunschAlignment(List<string> seq1, List<string> seq2);
}
