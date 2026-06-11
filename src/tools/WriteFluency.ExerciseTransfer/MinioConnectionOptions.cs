namespace WriteFluency.ExerciseTransfer;

internal sealed record MinioConnectionOptions(Uri Endpoint, string AccessKey, string SecretKey)
{
    public static MinioConnectionOptions Parse(string connectionString)
    {
        var values = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(part => part.Length == 2)
            .ToDictionary(part => part[0], part => part[1], StringComparer.OrdinalIgnoreCase);

        if (!values.TryGetValue("Endpoint", out var endpointValue)
            || !values.TryGetValue("AccessKey", out var accessKey)
            || !values.TryGetValue("SecretKey", out var secretKey))
        {
            throw new ArgumentException(
                "MinIO connection strings require Endpoint, AccessKey, and SecretKey.");
        }

        if (!endpointValue.Contains("://", StringComparison.Ordinal))
        {
            endpointValue = $"http://{endpointValue}";
        }

        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint))
        {
            throw new ArgumentException("The MinIO Endpoint is not a valid absolute URI.");
        }

        return new MinioConnectionOptions(endpoint, accessKey, secretKey);
    }

    public MinioConnectionOptions WithEndpoint(string host, int port)
    {
        return this with
        {
            Endpoint = new UriBuilder(Endpoint)
            {
                Host = host,
                Port = port
            }.Uri
        };
    }
}
