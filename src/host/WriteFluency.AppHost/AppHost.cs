var builder = DistributedApplication.CreateBuilder(args);

var minioPassword = builder.AddParameter("wf-minio-password", "admin123", secret: true);
var minio = builder.AddMinioContainer("wf-minio", port: 9000, rootPassword: minioPassword)
    .WithImage("minio/minio", "RELEASE.2025-06-13T11-33-47Z")
    .WithDataVolume("writefluency-minio-data");

var postgresPassword = builder.AddParameter("wf-postgres-password", "postgres", secret: true);
var postgres = builder.AddPostgres("wf-postgres")
    .WithPassword(postgresPassword)
    .WithImage("postgres:14.3")
    .WithDataVolume("writefluency-postgres-data");
var postgresdb = postgres.AddDatabase("wf-postgresdb");

var dbMigrator = builder.AddProject<Projects.WriteFluency_DbMigrator>("wf-db-migrator")
    .WithReference(postgresdb)
    .WaitFor(postgresdb);
dbMigrator.WithEnvironment("RESOURCE_NAME", dbMigrator.Resource.Name);

var api = builder.AddProject<Projects.WriteFluency_WebApi>("wf-api")
    .WithReference(postgresdb).WaitFor(postgresdb)
    .WithReference(minio).WaitFor(minio)
    .WaitForCompletion(dbMigrator)
    .WithHttpHealthCheck("health");
api.WithEnvironment("RESOURCE_NAME", api.Resource.Name);

var newsWorker = builder.AddProject<Projects.WriteFluency_NewsWorker>("wf-news-worker")
    .WithReference(postgresdb).WaitFor(postgresdb)
    .WithReference(minio).WaitFor(minio)
    .WaitForCompletion(dbMigrator)
    .WithHttpsEndpoint()
    .WithHttpHealthCheck("health");
newsWorker.WithEnvironment("RESOURCE_NAME", newsWorker.Resource.Name);

builder.AddNpmApp("wf-webapp", "../../webapp")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();


builder.Build().Run();
