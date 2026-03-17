var builder = DistributedApplication.CreateBuilder(args);

var minioUser = builder.AddParameter("wf-infra-minio-user", "minioadmin");
var minioPassword = builder.AddParameter("wf-infra-minio-password", "admin123", secret: true);

var minio = builder.AddMinioContainer("wf-infra-minio", port: 9000, rootPassword: minioPassword, rootUser: minioUser)
    .WithImage("minio/minio", "RELEASE.2025-06-13T11-33-47Z")
    .WithDataVolume("writefluency-minio-data");

// Create buckets with public download policies
var minioInit = builder.AddContainer("wf-propositions-minio-init", "wf-propositions-minio-init")
    .WithDockerfile("docker/minio-init")
    .WaitFor(minio)
    .WithEnvironment("MINIO_ROOT_USER", minioUser)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioPassword);

var postgresPassword = builder.AddParameter("wf-infra-postgres-password", "postgres", secret: true);
var postgres = builder.AddPostgres("wf-infra-postgres")
    .WithPassword(postgresPassword)
    .WithImage("postgres:14.3")
    .WithHostPort(5432)
    .WithDataVolume("writefluency-postgres-data");
var postgresdb = postgres.AddDatabase("wf-propositions-postgresdb");
var usersdb = postgres.AddDatabase("wf-users-postgresdb");

var redisPassword = builder.AddParameter("wf-infra-redis-password", "admin123", secret: true);
var redis = builder.AddRedis("wf-infra-redis", port: 6379)
    .WithPassword(redisPassword)
    .WithArgs("--maxmemory", "500mb", "--maxmemory-policy", "allkeys-lru")
    .WithDataVolume("writefluency-redis-data");

var smtp = builder.AddContainer("wf-infra-smtp", "boky/postfix", "latest")
    .WithEnvironment("ALLOWED_SENDER_DOMAINS", "writefluency.com")
    .WithEnvironment("POSTFIX_myhostname", "wf-infra-smtp.writefluency.svc.cluster.local")
    .WithEnvironment("POSTFIX_smtp_tls_security_level", "may")
    .WithEndpoint(2525, 25, name: "smtp", isProxied: false);

var localMailpit = builder.ExecutionContext.IsRunMode
    ? builder.AddContainer("wf-local-mailpit", "axllent/mailpit", "latest")
        .WithEndpoint(1025, 1025, name: "smtp", isProxied: false)
        .WithEndpoint(8025, 8025, name: "ui", isProxied: false)
    : null;

var dbMigrator = builder.AddProject<Projects.WriteFluency_DbMigrator>("wf-propositions-db-migrator")
    .WithReference(postgresdb)
    .WaitFor(postgresdb);
dbMigrator.WithEnvironment("RESOURCE_NAME", dbMigrator.Resource.Name);

var usersDbMigrator = builder.AddProject<Projects.WriteFluency_Users_DbMigrator>("wf-users-db-migrator")
    .WithReference(usersdb)
    .WaitFor(usersdb);
usersDbMigrator.WithEnvironment("RESOURCE_NAME", usersDbMigrator.Resource.Name);

var api = builder.AddProject<Projects.WriteFluency_WebApi>("wf-propositions-api")
    .WithReference(postgresdb).WaitFor(postgresdb)
    .WithReference(minio).WaitFor(minio)
    .WaitForCompletion(dbMigrator)
    .WithHttpHealthCheck("health")
    .WithHttpEndpoint(port: 5000, name: "apihttp", isProxied: false)
    .WithHttpsEndpoint(port: 5001, name: "apihttps", isProxied: false);
api.WithEnvironment("RESOURCE_NAME", api.Resource.Name);

var newsWorker = builder.AddProject<Projects.WriteFluency_NewsWorker>("wf-propositions-news-worker")
    .WithReference(postgresdb).WaitFor(postgresdb)
    .WithReference(minio).WaitFor(minio)
    .WaitForCompletion(dbMigrator)
    .WithHttpsEndpoint()
    .WithHttpHealthCheck("health");
newsWorker.WithEnvironment("RESOURCE_NAME", newsWorker.Resource.Name);

var usersApi = builder.AddProject<Projects.WriteFluency_Users_WebApi>("wf-users-api")
    .WithReference(usersdb).WaitFor(usersdb)
    .WithReference(redis)
    .WaitFor(redis)
    .WaitForCompletion(usersDbMigrator)
    .WithHttpHealthCheck("health")
    .WithHttpEndpoint(port: 5100, name: "usershttp", isProxied: false)
    .WithHttpsEndpoint(port: 5101, name: "usershttps", isProxied: false)
    .WithEnvironment("Smtp__FromEmail", "noreply@writefluency.com")
    .WithEnvironment("Smtp__FromName", "WriteFluency");

if (builder.ExecutionContext.IsRunMode)
{
    usersApi
        .WaitFor(localMailpit!)
        .WithEnvironment("Smtp__Host", "localhost")
        .WithEnvironment("Smtp__Port", "1025");
}
else
{
    usersApi
        .WaitFor(smtp)
        .WithEnvironment("Smtp__Host", "wf-infra-smtp")
        .WithEnvironment("Smtp__Port", "2525");
}

usersApi.WithEnvironment("RESOURCE_NAME", usersApi.Resource.Name);

builder.AddNpmApp("wf-webapp", "../../webapp")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();


builder.Build().Run();
