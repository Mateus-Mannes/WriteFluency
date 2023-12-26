namespace WriteFluencyApi.ListenAndWrite.Domain;

public interface ILevenshteinDistanceService
{
    int ComputeDistance(string s1, string s2);
}
