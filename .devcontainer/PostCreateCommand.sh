dotnet tool install --global dotnet-ef --version 7.0.5
dotnet restore aspnet-core/WriteFluency/WriteFluency.csproj
dotnet ef database update --project aspnet-core/WriteFluency/WriteFluency.csproj
dotnet dev-certs https
cd WriteFluencyApp
npm install