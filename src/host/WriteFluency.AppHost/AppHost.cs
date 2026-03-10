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
    .WaitForCompletion(usersDbMigrator)
    .WithHttpHealthCheck("health")
    .WithHttpEndpoint(port: 5100, name: "usershttp", isProxied: false)
    .WithHttpsEndpoint(port: 5101, name: "usershttps", isProxied: false);
usersApi.WithEnvironment("RESOURCE_NAME", usersApi.Resource.Name);

builder.AddNpmApp("wf-webapp", "../../webapp")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();


builder.Build().Run();
