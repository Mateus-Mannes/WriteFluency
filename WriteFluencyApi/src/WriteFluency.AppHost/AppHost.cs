var builder = DistributedApplication.CreateBuilder(args);

// TODO: finish configuring minio vars
// TODO: configure correctly the data volume names
var minioUser = builder.AddParameter("minio-user", "admin");
var minioPassword = builder.AddParameter("minio-password", "admin123", secret: true);
var minio = builder.AddContainer("minio", "minio/minio:RELEASE.2025-06-13T11-33-47Z")
    .WithVolume("minio-data", "/data")
    .WithEnvironment("MINIO_ROOT_USER", minioUser)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioPassword)
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEndpoint(port: 9000, name: "api", targetPort: 9000)
    .WithEndpoint(port: 9001, name: "console", targetPort: 9001);

var postgresPassword = builder.AddParameter("postgres-password", "postgres", secret: true);
var postgres = builder.AddPostgres("postgres")
    .WithPassword(postgresPassword)
    .WithImage("postgres:14.3")
    .WithDataVolume();
var postgresdb = postgres.AddDatabase("postgresdb");

var dbMigrator = builder.AddProject<Projects.WriteFluency_DbMigrator>("db-migrator")
    .WithReference(postgresdb)
    .WaitFor(postgresdb);

var api = builder.AddProject<Projects.WriteFluency_WebApi>("api")
    .WithReference(postgresdb)
    .WithReference(minio.GetEndpoint("api"))
    .WithEnvironment("minio-user", minioUser)
    .WithEnvironment("minio-password", minioPassword)
    .WaitFor(minio)
    .WaitForCompletion(dbMigrator);

var newsWorker = builder.AddProject<Projects.WriteFluency_NewsWorker>("news-worker")
    .WithReference(postgresdb)
    .WithReference(minio.GetEndpoint("api"))
    .WithEnvironment("minio-user", minioUser)
    .WithEnvironment("minio-password", minioPassword)
    .WaitFor(minio)
    .WaitForCompletion(dbMigrator);

builder.Build().Run();
