var builder = DistributedApplication.CreateBuilder(args);

var minioUser = builder.AddParameter("minio-user", "admin");
var minioPassword = builder.AddParameter("minio-password", "admin123", secret: true);
var minio = builder.AddMinioContainer("minio", port: 9000, rootUser: minioUser, rootPassword: minioPassword)
    .WithImage("minio/minio", "RELEASE.2025-06-13T11-33-47Z")
    .WithDataVolume("writefluency-minio-data");

var postgresPassword = builder.AddParameter("postgres-password", "postgres", secret: true);
var postgres = builder.AddPostgres("postgres")
    .WithPassword(postgresPassword)
    .WithImage("postgres:14.3")
    .WithDataVolume("writefluency-postgres-data");
var postgresdb = postgres.AddDatabase("postgresdb");

var dbMigrator = builder.AddProject<Projects.WriteFluency_DbMigrator>("db-migrator")
    .WithReference(postgresdb)
    .WaitFor(postgresdb);

var api = builder.AddProject<Projects.WriteFluency_WebApi>("api")
    .WithReference(postgresdb).WaitFor(postgresdb)
    .WithReference(minio).WaitFor(minio)
    .WaitForCompletion(dbMigrator)
    .WithHttpHealthCheck("health");

var newsWorker = builder.AddProject<Projects.WriteFluency_NewsWorker>("news-worker")
    .WithReference(postgresdb).WaitFor(postgresdb)
    .WithReference(minio).WaitFor(minio)
    .WaitForCompletion(dbMigrator)
    .WithHttpsEndpoint()
    .WithHttpHealthCheck("health");

builder.Build().Run();
