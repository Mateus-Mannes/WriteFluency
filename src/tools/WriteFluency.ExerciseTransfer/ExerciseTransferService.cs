using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Npgsql;
using WriteFluency.Data;
using WriteFluency.Propositions;

namespace WriteFluency.ExerciseTransfer;

internal sealed class ExerciseTransferService(TransferOptions options)
{
    private const string ProductionPostgresSecretKey = "ConnectionStrings__wf-propositions-postgresdb";
    private const string ProductionMinioSecretKey = "ConnectionStrings__wf-infra-minio";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var kubectl = new KubectlClient(options.KubernetesContext, options.KubernetesNamespace);
        var context = await kubectl.GetCurrentContextAsync(cancellationToken);

        Console.WriteLine($"Source Kubernetes context: {context}");
        Console.WriteLine($"Source namespace: {options.KubernetesNamespace}");
        Console.WriteLine($"Destination PostgreSQL: {DescribePostgres(options.LocalPostgresConnectionString)}");
        Console.WriteLine($"Destination MinIO: {options.LocalMinio.Endpoint}");

        await ValidateLocalDestinationAsync(cancellationToken);

        var secret = await kubectl.GetSecretAsync(options.KubernetesSecretName, cancellationToken);
        var productionPostgres = GetRequiredSecret(secret, ProductionPostgresSecretKey);
        var productionMinio = MinioConnectionOptions.Parse(
            GetRequiredSecret(secret, ProductionMinioSecretKey));

        Console.WriteLine("Opening temporary read-only production tunnels...");
        await using var postgresForward = await kubectl.StartPortForwardAsync(
            "wf-infra-postgres",
            5432,
            cancellationToken);
        await using var minioForward = await kubectl.StartPortForwardAsync(
            "wf-infra-minio",
            9000,
            cancellationToken);

        var sourcePostgres = RedirectPostgres(productionPostgres, postgresForward.LocalPort);
        var sourceMinio = productionMinio.WithEndpoint("127.0.0.1", minioForward.LocalPort);

        await TransferAsync(sourcePostgres, sourceMinio, cancellationToken);
    }

    private async Task TransferAsync(
        string sourcePostgres,
        MinioConnectionOptions sourceMinio,
        CancellationToken cancellationToken)
    {
        await using var sourceContext = CreateDbContext(sourcePostgres);
        await using var destinationContext = CreateDbContext(options.LocalPostgresConnectionString);

        var source = await sourceContext.Propositions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(proposition => proposition.Id == options.ExerciseId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Exercise {options.ExerciseId} was not found in production.");

        if (source.IsDeleted)
        {
            throw new InvalidOperationException(
                $"Exercise {options.ExerciseId} is deleted in production and will not be copied.");
        }

        var existing = await destinationContext.Propositions
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(proposition => proposition.Id == options.ExerciseId, cancellationToken);

        if (existing is not null && !options.Replace)
        {
            throw new InvalidOperationException(
                $"Exercise {options.ExerciseId} already exists locally. Pass --replace to overwrite it.");
        }

        Console.WriteLine($"Downloading exercise {source.Id}: {source.Title}");
        var sourceMinioClient = CreateMinioClient(sourceMinio);
        var audio = await DownloadObjectAsync(
            sourceMinioClient,
            Proposition.AudioBucketName,
            source.AudioFileId,
            cancellationToken);

        StoredObject? image = null;
        if (!string.IsNullOrWhiteSpace(source.ImageFileId))
        {
            image = await DownloadObjectAsync(
                sourceMinioClient,
                Proposition.ImageBucketName,
                source.ImageFileId,
                cancellationToken);
        }

        var destinationMinioClient = CreateMinioClient(options.LocalMinio);
        await UploadObjectAsync(
            destinationMinioClient,
            Proposition.AudioBucketName,
            source.AudioFileId,
            audio,
            cancellationToken);

        if (image is not null)
        {
            await UploadObjectAsync(
                destinationMinioClient,
                Proposition.ImageBucketName,
                source.ImageFileId!,
                image,
                cancellationToken);
        }

        await using var transaction = await destinationContext.Database.BeginTransactionAsync(cancellationToken);
        if (existing is null)
        {
            destinationContext.Propositions.Add(Clone(source));
        }
        else
        {
            Copy(source, existing);
        }

        await destinationContext.SaveChangesAsync(cancellationToken);
        await destinationContext.Database.ExecuteSqlRawAsync(
            """
            SELECT setval(
                pg_get_serial_sequence('"Propositions"', 'Id'),
                GREATEST((SELECT COALESCE(MAX("Id"), 1) FROM "Propositions"), 1),
                true);
            """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        Console.WriteLine(
            $"Exercise {source.Id} copied successfully with audio"
            + (image is null ? "." : " and image."));
        Console.WriteLine($"Open http://localhost:4200/english-writing-exercise/{source.Id}");
    }

    private async Task ValidateLocalDestinationAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Checking local Aspire services...");

        await using var destinationContext = CreateDbContext(options.LocalPostgresConnectionString);
        if (!await destinationContext.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The local propositions PostgreSQL database is unavailable. Start the Aspire AppHost first.");
        }

        var destinationMinio = CreateMinioClient(options.LocalMinio);
        await destinationMinio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(Proposition.AudioBucketName),
            cancellationToken);
    }

    private static AppDbContext CreateDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }

    private static IMinioClient CreateMinioClient(MinioConnectionOptions connection)
    {
        return new MinioClient()
            .WithEndpoint(connection.Endpoint.Host, connection.Endpoint.Port)
            .WithCredentials(connection.AccessKey, connection.SecretKey)
            .WithSSL(connection.Endpoint.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            .Build();
    }

    private static async Task<StoredObject> DownloadObjectAsync(
        IMinioClient client,
        string bucket,
        string objectName,
        CancellationToken cancellationToken)
    {
        var metadata = await client.StatObjectAsync(
            new StatObjectArgs().WithBucket(bucket).WithObject(objectName),
            cancellationToken);

        using var stream = new MemoryStream();
        await client.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithCallbackStream(source => source.CopyTo(stream)),
            cancellationToken);

        var contentType = string.IsNullOrWhiteSpace(metadata.ContentType)
            ? InferContentType(objectName)
            : metadata.ContentType;

        return new StoredObject(stream.ToArray(), contentType);
    }

    private static async Task UploadObjectAsync(
        IMinioClient client,
        string bucket,
        string objectName,
        StoredObject storedObject,
        CancellationToken cancellationToken)
    {
        var bucketExists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucket),
            cancellationToken);
        if (!bucketExists)
        {
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken);
        }

        using var stream = new MemoryStream(storedObject.Content);
        await client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(storedObject.ContentType),
            cancellationToken);
    }

    private static Proposition Clone(Proposition source)
    {
        var destination = new Proposition
        {
            Id = source.Id,
            PublishedOn = source.PublishedOn,
            SubjectId = source.SubjectId,
            ComplexityId = source.ComplexityId,
            AudioFileId = source.AudioFileId,
            Voice = source.Voice,
            AudioDurationSeconds = source.AudioDurationSeconds,
            Text = source.Text,
            TextLength = source.TextLength,
            Title = source.Title,
            ImageFileId = source.ImageFileId,
            CreatedAt = source.CreatedAt,
            IsDeleted = false,
            DeletedAt = null,
            PropositionGenerationLogId = null,
            NewsInfo = Clone(source.NewsInfo)
        };

        return destination;
    }

    private static void Copy(Proposition source, Proposition destination)
    {
        destination.PublishedOn = source.PublishedOn;
        destination.SubjectId = source.SubjectId;
        destination.ComplexityId = source.ComplexityId;
        destination.AudioFileId = source.AudioFileId;
        destination.Voice = source.Voice;
        destination.AudioDurationSeconds = source.AudioDurationSeconds;
        destination.Text = source.Text;
        destination.TextLength = source.TextLength;
        destination.Title = source.Title;
        destination.ImageFileId = source.ImageFileId;
        destination.CreatedAt = source.CreatedAt;
        destination.IsDeleted = false;
        destination.DeletedAt = null;
        destination.PropositionGenerationLogId = null;
        destination.NewsInfo = Clone(source.NewsInfo);
    }

    private static NewsInfo Clone(NewsInfo source)
    {
        return new NewsInfo
        {
            Id = source.Id,
            Title = source.Title,
            Description = source.Description,
            Url = source.Url,
            ImageUrl = source.ImageUrl,
            Text = source.Text,
            TextLength = source.TextLength
        };
    }

    private static string RedirectPostgres(string connectionString, int localPort)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Host = "127.0.0.1",
            Port = localPort,
            Pooling = false,
            Options = "-c default_transaction_read_only=on"
        };

        return builder.ConnectionString;
    }

    private static string DescribePostgres(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return $"{builder.Host}:{builder.Port}/{builder.Database}";
    }

    private static string GetRequiredSecret(
        IReadOnlyDictionary<string, string> secret,
        string key)
    {
        if (!secret.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Kubernetes secret is missing required key '{key}'.");
        }

        return value;
    }

    private static string InferContentType(string objectName)
    {
        return Path.GetExtension(objectName).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }

    private sealed record StoredObject(byte[] Content, string ContentType);
}